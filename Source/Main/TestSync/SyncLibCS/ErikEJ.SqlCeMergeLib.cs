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
        private string _dbPassword = string.Empty;

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
        /// Sets an optional database password.
        /// </summary>
        public string DatabasePassword
        {
            set
            {
                if (!string.IsNullOrEmpty(value))
                    _dbPassword = value;
            }
        }

        /// <summary>
        /// Allows you to specify replication properties in code.
        /// </summary>
        public ReplicationProperties ReplicationProperties { get; set; }
        
        /// <summary>
        /// Initiate a synchronization with the Web Agent based on the settings in app.config
        /// </summary>
        /// <param name="connection">A SqlCeConnection that point to the local database. Preferably closed.</param>
        /// <param name="hostName">The parameter used to filter the Publication (not required)</param>
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

            if (connection.State == ConnectionState.Open)
            {
                connection.Close();
            }

            var props = GetProperties();

            var repl = new SqlCeReplication();

            repl.Subscriber = hostName;
            repl.SubscriberConnectionString = GetSubscriberConnectionString(connection);
            if (!string.IsNullOrWhiteSpace(hostName))
            {
                repl.HostName = hostName;
                repl.Subscriber = hostName;
            }
            if (!string.IsNullOrWhiteSpace(props.Subscriber))
            {
                repl.Subscriber = props.Subscriber;
            }

            repl.PostSyncCleanup = 2;
            SetProperties(props, repl);

            InsertSyncLog(connection, hostName, additionalId, "Attempt", additionalInfo);
            if (option == ReinitializeOption.ReinitializeNoUpload)
            {
                repl.ReinitializeSubscription(false);
            }
            if (option == ReinitializeOption.ReinitializeUploadSubscriberChanges)
            {
                repl.ReinitializeSubscription(true);
            }

            repl.BeginSynchronize(
                SyncCompletedCallback,
                OnStartTableUploadCallback,
                OnStartTableDownloadCallback,
                OnSynchronizationCallback,
            repl);
        }

        private void SyncCompletedCallback(IAsyncResult ar)
        {
            SqlCeReplication repl = (SqlCeReplication)ar.AsyncState;
            try
            {
                repl.EndSynchronize(ar);
                repl.SaveProperties();
                var result = "Successfully completed sync" + Environment.NewLine;
                result += string.Format("Number of changes downloaded: {0}{1}", repl.PublisherChanges.ToString(), Environment.NewLine);
                result += string.Format("Number of changes uploaded: {0}{1}", repl.SubscriberChanges.ToString(), Environment.NewLine);
                result += string.Format("Number of conflicts at Publisher:   {0}{1}", repl.PublisherConflicts.ToString(), Environment.NewLine);
                var args = new SyncArgs(result, null, 100, SyncStatus.SyncComplete, null);
                Completed?.Invoke(this, args);
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
                Completed?.Invoke(this, args);
            }
            finally
            {
                if (repl != null)
                    repl.Dispose();
                if (_connection != null)
                {
                    _connection.Close();
                    _connection.Dispose();
                }
            }
        }

        private void OnStartTableUploadCallback(IAsyncResult ar, string tableName)
        {
            var args = new SyncArgs("Began uploading table : " + tableName, null, 0, SyncStatus.BeginUpload, tableName);
            Progress?.Invoke(this, args);
        }

        private void OnSynchronizationCallback(IAsyncResult ar, int percentComplete)
        {
            var args = new SyncArgs("Sync with SQL Server is " + percentComplete.ToString() + "% complete.", null, percentComplete, SyncStatus.PercentComplete, null);
            Progress?.Invoke(this, args);
        }

        private void OnStartTableDownloadCallback(IAsyncResult ar, string tableName)
        {
            var args = new SyncArgs("Began downloading table : " + tableName, null, 0, SyncStatus.BeginDownload, tableName);
            Progress?.Invoke(this, args);
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

            if (connection.State != ConnectionState.Open)
            {
                connection.ConnectionString = GetSubscriberConnectionString(connection);
                connection.Open();
            }
            
            var props = GetProperties();

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
            if (connection.State != ConnectionState.Open)
            {
                connection.ConnectionString = GetSubscriberConnectionString(connection);
            }

            using (IRepository repository = new DBRepository(connection.ConnectionString))
            {
                var generator = new Generator(repository);
                foreach (var tableName in tableNames)
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
            if (!System.IO.File.Exists(connection.Database))
            {
                return false;
            }

            if (System.IO.File.Exists(connection.Database))
            {
                var fi = new System.IO.FileInfo(connection.Database);
                if (fi.Length < 20481)
                {
                    return false;
                }
            }

            if (connection.State != ConnectionState.Open)
            {
                connection.ConnectionString = GetSubscriberConnectionString(connection);
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

            return true;
        }

        /// <summary>
        /// Format a SqlCeException as a String
        /// </summary>
        /// <param name="e"></param>
        /// <returns>A formatted error string</returns>
        public string ShowErrors(SqlCeException e)
        {
            var errorCollection = e.Errors;

            var bld = new StringBuilder();
            var inner = e.InnerException;

            if (!string.IsNullOrEmpty(e.HelpLink))
            {
                bld.Append("\nCommand text: ");
                bld.Append(e.HelpLink);
            }

            if (null != inner)
            {
                bld.Append("\nInner Exception: " + inner);
            }
            // Enumerate the errors to a message box.
            foreach (SqlCeError err in errorCollection)
            {
                bld.Append("\n Error Code: 0x" + err.HResult.ToString("X", System.Globalization.CultureInfo.InvariantCulture));
                bld.Append("\n Message   : " + err.Message);
                bld.Append("\n Minor Err.: " + err.NativeError);
                bld.Append("\n Source    : " + err.Source);

                // Enumerate each numeric parameter for the error.
                foreach (var numPar in err.NumericErrorParameters)
                {
                    if (0 != numPar) bld.Append("\n Num. Par. : " + numPar);
                }

                // Enumerate each string parameter for the error.
                foreach (var errPar in err.ErrorParameters)
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

        private static void SetProperties(ReplicationProperties props, SqlCeReplication repl)
        {
            if (props.UseNT)
            {
                repl.PublisherSecurityMode = SecurityType.NTAuthentication;
            }
            else
            {
                repl.PublisherSecurityMode = SecurityType.DBAuthentication;
            }
            if (props.UseProxy)
            {
                repl.InternetProxyLogin = props.InternetProxyLogin;
                repl.InternetProxyPassword = props.InternetProxyPassword;
                repl.InternetProxyServer = props.InternetProxyServer;
            }
            repl.Publisher = props.Publisher;
            repl.PublisherLogin = props.PublisherLogin;
            repl.PublisherPassword = props.PublisherPassword;
            repl.PublisherDatabase = props.PublisherDatabase;

            if (!string.IsNullOrEmpty(props.Distributor))
            {
                if (props.UseNT)
                {
                    repl.DistributorSecurityMode = SecurityType.NTAuthentication;
                }
                else
                {
                    repl.DistributorSecurityMode = SecurityType.DBAuthentication;
                }
                repl.Distributor = props.Distributor;
                repl.DistributorLogin = props.DistributorLogin;
                repl.DistributorPassword = props.DistributorPassword;
            }

            repl.Publication = props.Publication;
            repl.InternetUrl = props.InternetUrl;
            repl.InternetLogin = props.InternetLogin;
            repl.InternetPassword = props.InternetPassword;
            if (props.CompressionLevel != 1)
                repl.CompressionLevel = props.CompressionLevel;
            if (props.ConnectionRetryTimeout != 120)
                repl.ConnectionRetryTimeout = props.ConnectionRetryTimeout;
            if (props.ConnectTimeout > 0)
                repl.ConnectTimeout = props.ConnectTimeout;
            if (props.ReceiveTimeout != 60000)
                repl.ReceiveTimeout = props.ReceiveTimeout;
            if (props.SendTimeout > 0)
                repl.SendTimeout = props.SendTimeout;
        }

        private ReplicationProperties GetProperties()
        {
            if (ReplicationProperties != null)
                return ReplicationProperties;

            var props = new ReplicationProperties
            {
                InternetLogin = ConfigurationManager.AppSettings[_configPrefix + "InternetLogin"],
                InternetPassword = ConfigurationManager.AppSettings[_configPrefix + "InternetPassword"],
                InternetUrl = ConfigurationManager.AppSettings[_configPrefix + "InternetUrl"],
                Publication = ConfigurationManager.AppSettings[_configPrefix + "Publication"],
                Publisher = ConfigurationManager.AppSettings[_configPrefix + "Publisher"],
                PublisherDatabase = ConfigurationManager.AppSettings[_configPrefix + "PublisherDatabase"],
                PublisherLogin = ConfigurationManager.AppSettings[_configPrefix + "PublisherLogin"],
                PublisherPassword = ConfigurationManager.AppSettings[_configPrefix + "PublisherPassword"],
                Distributor = ConfigurationManager.AppSettings[_configPrefix + "Distributor"],
                DistributorLogin = ConfigurationManager.AppSettings[_configPrefix + "DistributorLogin"],
                DistributorPassword = ConfigurationManager.AppSettings[_configPrefix + "DistributorPassword"],
                UseNT = Convert.ToBoolean(ConfigurationManager.AppSettings[_configPrefix + "UseNT"]),
                Subscriber = ConfigurationManager.AppSettings[_configPrefix + "Subscriber"],
                UseProxy = Convert.ToBoolean(ConfigurationManager.AppSettings[_configPrefix + "UseProxy"]),
                InternetProxyLogin = ConfigurationManager.AppSettings[_configPrefix + "InternetProxyLogin"],
                InternetProxyPassword = ConfigurationManager.AppSettings[_configPrefix + "InternetProxyPassword"],
                InternetProxyServer = ConfigurationManager.AppSettings[_configPrefix + "InternetProxyServer"]
            };
            var level = Convert.ToInt16(ConfigurationManager.AppSettings[_configPrefix + "CompressionLevel"]);
            if (level != 0)
                props.CompressionLevel = Convert.ToInt16(ConfigurationManager.AppSettings[_configPrefix + "CompressionLevel"]);
            var retry = Convert.ToInt16(ConfigurationManager.AppSettings[_configPrefix + "ConnectionRetryTimeout"]);
            if (retry != 0)
                props.ConnectionRetryTimeout = Convert.ToInt16(ConfigurationManager.AppSettings[_configPrefix + "ConnectionRetryTimeout"]);
            var connect = Convert.ToInt32(ConfigurationManager.AppSettings[_configPrefix + "ConnectTimeout"]);
            if (connect != 0)
                props.ConnectTimeout = Convert.ToInt32(ConfigurationManager.AppSettings[_configPrefix + "ConnectTimeout"]);
            var receive = Convert.ToInt32(ConfigurationManager.AppSettings[_configPrefix + "ReceiveTimeout"]);
            if (receive != 0)
                props.ReceiveTimeout = Convert.ToInt32(ConfigurationManager.AppSettings[_configPrefix + "ReceiveTimeout"]);
            var send = Convert.ToInt32(ConfigurationManager.AppSettings[_configPrefix + "SendTimeout"]);
            if (send != 0)
                props.SendTimeout = Convert.ToInt32(ConfigurationManager.AppSettings[_configPrefix + "SendTimeout"]);
            return props;
        }

        private string GetSubscriberConnectionString(SqlCeConnection conn)
        {
            if (!string.IsNullOrWhiteSpace(_dbPassword))
            {
                return conn.ConnectionString + ";Password=" + _dbPassword;
            }
            return conn.ConnectionString;
        }

        private void InsertSyncLog(SqlCeConnection connection, string hostName, int otherId, string status, string syncInfo)
        {
            //TODO Add a SyncLog table to the publication to collect Sync status messages, if desired
            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }

                using (var cmd = connection.CreateCommand())
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
            catch
            {
                // ignored
            }
        }
        #endregion
    }
}
