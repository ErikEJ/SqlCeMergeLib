using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlServerCe;
using System.Configuration;
using System.Data;
using ErikEJ.SqlCeScripting;

namespace ErikEJ.SqlCeMergeLib
{
    /// <summary>
    /// Merge Replication helper class
    /// </summary>
    public class MergeReplication
    {
        private string _hostName;
        private int _additionalId;
        private string _additionalInfo;
        private SqlCeConnection _connection;
        private string _configPrefix = string.Empty;

        /// <summary>
        /// Event occurs when synchronization has completed or failed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="ca"></param>
        public delegate void CompletedHandler(object sender, SyncArgs ca);

        /// <summary>
        /// Event occurs when synchronization has completed or failed
        /// </summary>
        public event CompletedHandler Completed;

        /// <summary>
        /// Event occurs during sync
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="ca"></param>
        public delegate void ProgressHandler(object sender, SyncArgs ca);
        
        /// <summary>
        /// Event during sync
        /// </summary>
        public event ProgressHandler Progress; 
        
        private struct ReplicationProperties
        {
            public string SubscriberConnectionString { get; set; }
            public bool UseNT { get; set; }
            public string Publisher { get; set; }
            public string PublisherLogin { get; set; }
            public string PublisherPassword { get; set; }
            public string PublisherDatabase { get; set; }
            public string Publication { get; set; }
            public string InternetUrl { get; set; }
            public string InternetLogin { get; set; }
            public string InternetPassword { get; set; }
            public string Subscriber { get; set; }
            public string HostName { get; set; }
        }

        /// <summary>
        /// Initiate a synchronization with the Web Agent based on the settings in app.config
        /// </summary>
        /// <param name="connection">A SqlCeConnection that point to the local database. Preferably closed.</param>
        /// <param name="hostName">The parameter used to filter the Publication</param>
        /// <param name="additionalId">Additional identification</param>
        /// <param name="additionalInfo">Additional information</param>
        public void Synchronize(SqlCeConnection connection, string hostName, int additionalId, string additionalInfo)
        {
            Synchronize(connection, hostName, additionalId, additionalInfo, ReinitializeOption.None);
        }

        /// <summary>
        /// Sets an optional configuration key prefix, allowing multiple groups of settings in app.config.
        /// This will allow you to have settings for both test and production in the same .config file.
        /// </summary>
        public string ConfigurationKeyPrefix
        {
            set
            {
                if (!string.IsNullOrEmpty(value))
                    _configPrefix = value;
            }
        }

        /// <summary>
        /// Initiate a synchronization with the Web Agent based on the settings in app.config
        /// </summary>
        /// <param name="connection">A SqlCeConnection that point to the local database. Preferably closed.</param>
        /// <param name="hostName">The parameter used to filter the Publication</param>
        /// <param name="additionalId">Additional identification</param>
        /// <param name="additionalInfo">Additional information</param>
        /// <param name="option">ReinitializeOption</param>
        public void Synchronize(SqlCeConnection connection, string hostName, int additionalId, string additionalInfo, ReinitializeOption option)
        {
            _hostName = hostName;
            _additionalId = additionalId;
            _additionalInfo = additionalInfo;
            _connection = connection;
            CreateDatabaseIfNotExists(connection);

            if (connection.State == System.Data.ConnectionState.Open)
            {
                connection.Close();
            }

            ReplicationProperties props = GetPropertiesFromSettings();

            SqlCeReplication repl = new SqlCeReplication();

            repl.Subscriber = hostName.ToString();
            repl.SubscriberConnectionString = connection.ConnectionString;
            repl.HostName = hostName.ToString();
            repl.PostSyncCleanup = 2;
            props = SetProperties(props, repl);

            InsertSyncLog(connection, hostName, additionalId, "Attempt", additionalInfo);
            if (option == ReinitializeOption.ReinitializeNoUpload)
            {
                repl.ReinitializeSubscription(false);
            }
            if (option == ReinitializeOption.ReinitializeUploadSubscriberChanges)
            {
                repl.ReinitializeSubscription(true);
            }

            IAsyncResult ar = repl.BeginSynchronize(
                new AsyncCallback(this.SyncCompletedCallback),
                new OnStartTableUpload(this.OnStartTableUploadCallback),
                new OnStartTableDownload(this.OnStartTableDownloadCallback),
                new OnSynchronization(this.OnSynchronizationCallback),
            repl);
        }

        private void SyncCompletedCallback(IAsyncResult ar)
        {
            SqlCeReplication repl = (SqlCeReplication)ar.AsyncState;
            try
            {
                repl.EndSynchronize(ar);
                repl.SaveProperties();
                string result = "Successfully completed sync" + Environment.NewLine;
                result += string.Format("Number of changes downloaded: {0}{1}", repl.PublisherChanges.ToString(), Environment.NewLine);
                result += string.Format("Number of changes uploaded: {0}{1}", repl.SubscriberChanges.ToString(), Environment.NewLine);
                result += string.Format("Number of conflicts at Publisher:   {0}{1}", repl.PublisherConflicts.ToString(), Environment.NewLine);
                SyncArgs args = new SyncArgs(result, null, 100, SyncStatus.SyncComplete, null);
                Completed(this, args);
                InsertSyncLog(_connection, _hostName, _additionalId, "Success", _additionalInfo);
            }
            catch (SqlCeException e)
            {
                InsertSyncLog(_connection, _hostName, _additionalId, "Error", _additionalInfo + Environment.NewLine + ShowErrors(e));
                SyncArgs args;
                if (e.NativeError == 29006)
                {
                    args = new SyncArgs("Publication may have expired, or the snapshot is invalid", new PublicationMayHaveExpiredException("Publication may have expired, or the snapshot is invalid", e), 100, SyncStatus.SyncFailed, null);
                }
                else
                {
                    args = new SyncArgs("Errors occured during sync", e, 100, SyncStatus.SyncFailed, null);
                }
                Completed(this, args);
            }
            finally
            {
                if (repl != null)
                    repl.Dispose();
            }
        }

        private void OnStartTableUploadCallback(IAsyncResult ar, string tableName)
        {
            var args = new SyncArgs("Began uploading table : " + tableName, null, 0, SyncStatus.BeginUpload, tableName);
            Progress(this, args);
        }

        private void OnSynchronizationCallback(IAsyncResult ar, int percentComplete)
        {
            var args = new SyncArgs("Sync with SQL Server is " + percentComplete.ToString() + "% complete.", null, percentComplete, SyncStatus.PercentComplete, null);
            Progress(this, args);
        }

        private void OnStartTableDownloadCallback(IAsyncResult ar, string tableName)
        {
            var args = new SyncArgs("Began downloading table : " + tableName, null, 0, SyncStatus.BeginDownload, tableName);
            Progress(this, args);
        }

        /// <summary>
        /// Initiate a synchronization with the Web Agent based on the settings in app.config
        /// </summary>
        /// <param name="connection">A SqlCeConnection that point to the local database. Preferably closed.</param>
        /// <param name="hostName">The parameter used to filter the Publication</param>
        public void Synchronize(SqlCeConnection connection, int hostName)
        {
            Synchronize(connection, hostName.ToString(), -1, string.Empty);
        }

        /// <summary>
        /// Initiate a synchronization with the Web Agent based on the settings in app.config
        /// </summary>
        /// <param name="connection">A SqlCeConnection that point to the local database. Preferably closed.</param>
        /// <param name="hostName">The parameter used to filter the Publication</param>
        /// <param name="option">ReinitializeOption</param>
        public void Synchronize(SqlCeConnection connection, int hostName, ReinitializeOption option)
        {
            Synchronize(connection, hostName.ToString(), -1, string.Empty, option);
        }
       
        /// <summary>
        /// Get the local Datetime for last succesful synchronization
        /// If no Synchronization has happened, will return DateTime.MinValue
        /// </summary>
        /// <param name="connection">An open connection to the local database</param>
        /// <returns>The date and time for the last succesful sync</returns>
        public DateTime GetLastSuccessfulSyncTime(SqlCeConnection connection)
        {
            if (!System.IO.File.Exists(connection.Database))
                return DateTime.MinValue;

            if (connection.State != System.Data.ConnectionState.Open)
            {
                connection.Open();
            }
            
            var props = GetPropertiesFromSettings();

            using (SqlCeCommand cmd = connection.CreateCommand())
            {
                cmd.Connection = connection;

                cmd.CommandText = "SELECT table_name FROM information_schema.tables WHERE TABLE_NAME = @table";
                cmd.Parameters.Add("@table", SqlDbType.NVarChar, 4000);
                cmd.Parameters["@table"].Value = "__sysMergeSubscriptions";
                object obj = cmd.ExecuteScalar();

                if (obj == null)
                    return DateTime.MinValue;
                cmd.Parameters.Clear();

                cmd.CommandText = "SELECT LastSuccessfulSync FROM __sysMergeSubscriptions " +
                    "WHERE Publisher=@publisher AND PublisherDatabase=@database AND Publication=@publication";

                cmd.Parameters.Add("@publisher", SqlDbType.NVarChar, 4000);
                cmd.Parameters["@publisher"].Value = props.Publisher;

                cmd.Parameters.Add("@database", SqlDbType.NVarChar, 4000);
                cmd.Parameters["@database"].Value = props.PublisherDatabase;

                cmd.Parameters.Add("@publication", SqlDbType.NVarChar, 4000);
                cmd.Parameters["@publication"].Value = props.Publication;

                obj = cmd.ExecuteScalar();
                if (obj == null)
                    return DateTime.MinValue;
                else
                    return ((DateTime)obj);
            }
        }

        /// <summary>
        /// Generates a INSERT script for the suppplied list of tables, in that order
        /// </summary>
        /// <param name="connection">The SqlCeConnection for the local database</param>
        /// <param name="tableNames">A list of tables</param>
        /// <returns>A string with the generated script</returns>
        public string GenerateInsertScripts(SqlCeConnection connection, List<string> tableNames)
        {
            using (IRepository repository = new DBRepository(connection.ConnectionString))
            {
                Generator generator = new Generator(repository);
                foreach (string tableName in tableNames)
                {
                    generator.GenerateTableData(tableName, false);    
                }
                return generator.GeneratedScript;
            }
        }

        /// <summary>
        /// Validates that the local database is properly Merge Replicated 
        /// - Only works for a single publication, and expects writeable tables in the Publication
        /// </summary>
        /// <param name="connection">The SqlCeConnection to the local database</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public bool Validate(SqlCeConnection connection)
        {
            return Validate(connection, false);
        }

        /// <summary>
        /// Validates that the local database is properly Merge Replicated 
        /// - Only works for a single publication
        /// </summary>
        /// <param name="connection">The SqlCeConnection to the local database</param>
        /// <param name="isReadOnly">Set to True if entire publication is read only</param>
        /// <returns></returns>
        public bool Validate(SqlCeConnection connection, bool isReadOnly)
        {
            bool isValid = true;

            if (!System.IO.File.Exists(connection.Database))
            {
                return false;
            }

            if (System.IO.File.Exists(connection.Database))
            {
                System.IO.FileInfo fi = new System.IO.FileInfo(connection.Database);
                if (fi.Length < 20481)
                {
                    return false;
                }
            }

            if (connection.State != System.Data.ConnectionState.Open)
            {
                connection.Open();
            }

            var mergeTablesReadOnly = new List<string> { "__sysMergeSubscriptions", "__sysMergeArticles" };
            var mergeTablesReadWrite = new List<string> { "__sysTrackedObjects", "__sysRowTrack", "__sysDeletedRows" };
            if (!isReadOnly)
            {
                mergeTablesReadOnly = mergeTablesReadOnly.Union(mergeTablesReadWrite).ToList();
            }

            using (SqlCeCommand cmd = connection.CreateCommand())
            {
                cmd.Connection = connection;

                foreach (string mergeTable in mergeTablesReadOnly)
                {
                    cmd.CommandText = "SELECT table_name FROM information_schema.tables WHERE TABLE_NAME = @table";
                    cmd.Parameters.Add("@table", SqlDbType.NVarChar, 4000);
                    cmd.Parameters["@table"].Value = mergeTable;
                    object val = cmd.ExecuteScalar();
                    if (val == null)
                    {
                        return false;
                    }
                    cmd.Parameters.Clear();
                }

                string testPartner = "SELECT * FROM __sysTrackedObjects AS sto JOIN __sysMergeSubscriptions AS sms ON sto.N = sms.SyncPartnerId AND sto.T='P' AND sto.SV = sms.Publication";
                cmd.CommandText = testPartner;
                object retval = cmd.ExecuteScalar();
                if (retval == null)
                {
                    return false;
                }
            }

            return isValid;
        }

        /// <summary>
        /// Format a SqlCeException as a String
        /// </summary>
        /// <param name="e"></param>
        /// <returns>A formatted error string</returns>
        public string ShowErrors(System.Data.SqlServerCe.SqlCeException e)
        {
            System.Data.SqlServerCe.SqlCeErrorCollection errorCollection = e.Errors;

            StringBuilder bld = new StringBuilder();
            Exception inner = e.InnerException;

            if (!string.IsNullOrEmpty(e.HelpLink))
            {
                bld.Append("\nCommand text: ");
                bld.Append(e.HelpLink);
            }

            if (null != inner)
            {
                bld.Append("\nInner Exception: " + inner.ToString());
            }
            // Enumerate the errors to a message box.
            foreach (System.Data.SqlServerCe.SqlCeError err in errorCollection)
            {
                bld.Append("\n Error Code: " + err.HResult.ToString("X", System.Globalization.CultureInfo.InvariantCulture));
                bld.Append("\n Message   : " + err.Message);
                bld.Append("\n Minor Err.: " + err.NativeError);
                bld.Append("\n Source    : " + err.Source);

                // Enumerate each numeric parameter for the error.
                foreach (int numPar in err.NumericErrorParameters)
                {
                    if (0 != numPar) bld.Append("\n Num. Par. : " + numPar);
                }

                // Enumerate each string parameter for the error.
                foreach (string errPar in err.ErrorParameters)
                {
                    if (!string.IsNullOrEmpty(errPar)) bld.Append("\n Err. Par. : " + errPar);
                }
            }
            return bld.ToString();
        }

        #region Private Methods

        private static void CreateDatabaseIfNotExists(SqlCeConnection connection)
        {
            if (!System.IO.File.Exists(connection.Database))
            {
                using (SqlCeEngine engine = new SqlCeEngine(connection.ConnectionString))
                {
                    engine.CreateDatabase();
                }
            }
        }

        private static ReplicationProperties SetProperties(ReplicationProperties props, SqlCeReplication repl)
        {
            if (props.UseNT)
            {
                repl.PublisherSecurityMode = SecurityType.NTAuthentication;
            }
            else
            {
                repl.PublisherSecurityMode = SecurityType.DBAuthentication;
            }
            repl.Publisher = props.Publisher;
            repl.PublisherLogin = props.PublisherLogin;
            repl.PublisherPassword = props.PublisherPassword;
            repl.PublisherDatabase = props.PublisherDatabase;
            repl.Publication = props.Publication;
            repl.InternetUrl = props.InternetUrl;
            repl.InternetLogin = props.InternetLogin;
            repl.InternetPassword = props.InternetPassword;
            return props;
        }

        private ReplicationProperties GetPropertiesFromSettings()
        {
            var props = new ReplicationProperties();

            props.InternetLogin = ConfigurationManager.AppSettings[_configPrefix + "InternetLogin"];
            props.InternetPassword = ConfigurationManager.AppSettings[_configPrefix + "InternetPassword"];
            props.InternetUrl = ConfigurationManager.AppSettings[_configPrefix + "InternetUrl"];
            props.Publication = ConfigurationManager.AppSettings[_configPrefix + "Publication"];
            props.Publisher = ConfigurationManager.AppSettings[_configPrefix + "Publisher"];
            props.PublisherDatabase = ConfigurationManager.AppSettings[_configPrefix + "PublisherDatabase"];
            props.PublisherLogin = ConfigurationManager.AppSettings[_configPrefix + "PublisherLogin"];
            props.PublisherPassword = ConfigurationManager.AppSettings[_configPrefix + "PublisherPassword"];
            props.UseNT = Convert.ToBoolean(ConfigurationManager.AppSettings[_configPrefix + "UseNT"]);
            return props;
        }

        private void InsertSyncLog(SqlCeConnection connection, string hostName, int otherId, string status, string syncInfo)
        {
            //TODO Add a SyncLog table to the publication to collect Sync status messages, if desired
            try
            {
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    connection.Open();
                }

                using (SqlCeCommand cmd = connection.CreateCommand())
                {
                    cmd.Connection = connection;
                    cmd.CommandText = @"
                INSERT INTO [SyncLog] 
                ([SyncLogPK] ,[HostName] ,[OtherID] ,[SyncTime] ,[SyncEvent], [SyncInfo]) 
                VALUES  (NEWID(), @p0, @p1, @p2, @p3, @p4 )";

                    cmd.Parameters.Add(new SqlCeParameter("@p0", SqlDbType.NVarChar, 50));
                    cmd.Parameters["@p0"].Value = hostName;

                    cmd.Parameters.Add(new SqlCeParameter("@p1", SqlDbType.BigInt));
                    cmd.Parameters["@p1"].Value = otherId;

                    cmd.Parameters.Add(new SqlCeParameter("@p2", SqlDbType.DateTime));
                    cmd.Parameters["@p2"].Value = DateTime.UtcNow;

                    cmd.Parameters.Add(new SqlCeParameter("@p3", SqlDbType.NVarChar, 50));
                    cmd.Parameters["@p3"].Value = status;

                    cmd.Parameters.Add(new SqlCeParameter("@p4", SqlDbType.NText));
                    cmd.Parameters["@p4"].Value = syncInfo;

                    cmd.ExecuteNonQuery();

                }
            }
            // Ignore if the table does not exist or this somehow fails anyway
            catch { }
        }

        #endregion

    }
    /// <summary>
    /// Occurs when the Merge Publiation Subscription has expired
    /// </summary>
    public class PublicationMayHaveExpiredException : System.Exception
    {
        // The default constructor needs to be defined
        // explicitly now since it would be gone otherwise.
        /// <summary>
        /// Default constructor
        /// </summary>
        public PublicationMayHaveExpiredException()
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message"></param>
        public PublicationMayHaveExpiredException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public PublicationMayHaveExpiredException(string message,
        SqlCeException innerException): base(message, innerException)
        {
        }

    }

    /// <summary>
    /// Sync args
    /// </summary>
    public class SyncArgs : System.EventArgs
    {
        private int percentComplete;
        private string message;
        private Exception exception;
        private string tableName;
        private SyncStatus status;
        
        /// <summary>
        /// Construct a new instacne of SyncArgs
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        /// <param name="percentComplete"></param>
        /// <param name="status"></param>
        /// <param name="tableName"></param>
        public SyncArgs(string message, Exception ex, int percentComplete, SyncStatus status, string tableName)
        {
            this.message = message;
            this.exception = ex;
            this.percentComplete = percentComplete;
            this.status = status;
            this.tableName = tableName;
        }

        /// <summary>
        /// Message
        /// </summary>
        public string Message
        {
            get { return message; }
        }

        /// <summary>
        /// Exception
        /// </summary>
        public Exception Exception
        {
            get { return exception; }
        }

        /// <summary>
        /// Percentage complete
        /// </summary>
        public int PercentComplete
        {
            get { return percentComplete; }
        }

        /// <summary>
        /// Status
        /// </summary>
        public SyncStatus SyncStatus
        {
            get { return status; }
        }

        /// <summary>
        /// The table name
        /// </summary>
        public string TableName
        {
            get { return tableName; }
        }

    }

    /// <summary>
    /// How will the subscription be reinitialize
    /// </summary>
    public enum ReinitializeOption
    { 
        /// <summary>
        /// Reinitialize and upload subscriber changes first
        /// </summary>
        ReinitializeUploadSubscriberChanges,
        /// <summary>
        /// Reinitialize do not upload subscriber changes
        /// </summary>
        ReinitializeNoUpload,
        /// <summary>
        /// Do not reinitialize 
        /// </summary>
        None
    }

    /// <summary>
    /// The status of the sync event
    /// </summary>
    public enum SyncStatus
    {
        /// <summary>
        /// Progress percentage
        /// </summary>
        PercentComplete,
        /// <summary>
        /// Table upload
        /// </summary>
        BeginUpload,
        /// <summary>
        /// Table download
        /// </summary>
        BeginDownload,
        /// <summary>
        /// Sync completed
        /// </summary>
        SyncComplete,
        /// <summary>
        /// Sync completed with errors
        /// </summary>
        SyncFailed
    }


}
