namespace ErikEJ.SqlCeMergeLib
{
    /// <summary>
    /// Merge Replication Properties
    /// </summary>
    public class ReplicationProperties
    {
        /// <summary>
        /// true, use NT authorization - false, use database authorization, used to specify the security mode used when connecting to the Publisher. 
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public bool UseNT { get; set; }
        /// <summary>
        /// Specifies the name of the SQL Server Publisher. The Publisher is the computer that is running SQL Server and that contains the publication.
        /// </summary>
        public string Publisher { get; set; }
        /// <summary>
        /// Specifies the login name used when connecting to the Publisher. 
        /// </summary>
        public string PublisherLogin { get; set; }
        /// <summary>
        /// Specifies the login password used when connecting to the Publisher. 
        /// </summary>
        public string PublisherPassword { get; set; }
        /// <summary>
        /// Specifies the name of the publication database. 
        /// </summary>
        public string PublisherDatabase { get; set; }
        /// <summary>
        /// Specifies the SQL Server publication name that has been enabled for SQL Server Compact subscribers. 
        /// </summary>
        public string Publication { get; set; }
        /// <summary>
        /// Specifies the URL used to connect to the SQL Server Compact Server Agent. 
        /// </summary>
        public string InternetUrl { get; set; }
        /// <summary>
        /// Specifies the login name used when connecting to the SQL Server Compact Server Agent. 
        /// </summary>
        public string InternetLogin { get; set; }
        /// <summary>
        /// Specifies the password used when connecting to the SQL Server Compact Server Agent. 
        /// </summary>
        public string InternetPassword { get; set; }
        /// <summary>
        /// Specifies the name of the Subscriber. 
        /// </summary>
        public string Subscriber { get; set; }
        /// <summary>
        /// Specifies the connection string to the SQL Server Compact database. 
        /// </summary>
        public string SubscriberConnectionString { get; set; }
        /// <summary>
        /// Gets or sets the host name used for the device when connecting to the Publisher. 
        /// </summary>
        public string HostName { get; set; }

        ///// <summary>
        ///// Gets or sets the password to be used for the device when connecting to the SDF file, empty for no password. 
        ///// </summary>
        //public string ConnectionPassword { get; set; }


        /// <summary>
        /// Gets or sets the Use Proxy flag which determines if the proxy parameters are initialized when 
        /// connecting to the SQL Server Compact Agent.
        /// </summary>
        public bool UseProxy { get; set; }
        /// <summary>
        /// Specifies the proxy login used when connecting to the SQL Server Compact Server Agent. 
        /// </summary>
        public string InternetProxyLogin { get; set; }
        /// <summary>
        /// Specifies the proxy password used when connecting to the SQL Server Compact Server Agent. 
        /// </summary>
        public string InternetProxyPassword { get; set; }
        /// <summary>
        /// Specifies the proxy server used when connecting to the SQL Server Compact Server Agent. 
        /// </summary>
        public string InternetProxyServer { get; set; }

        private short _compressionLevel = 1;
        /// <summary>
        /// Specifies the amount of compression that will be used by the compression routines during replication. 
        /// </summary>
        public short CompressionLevel
        {
            get
            {
                return _compressionLevel;
            }
            set
            {
                _compressionLevel = value;
            }
        }

        private short _connectionRetryTimeout = 120;
        /// <summary>
        /// Specifies how long (in seconds) the SQL Server Compact 3.5 SP2 client will continue to retry sending requests after an established connection has failed. 
        /// </summary>
        public short ConnectionRetryTimeout
        {
            get
            {
                return _connectionRetryTimeout;
            }
            set
            {
                _connectionRetryTimeout = value;
            }
        }

        private int _connectTimeout;
        /// <summary>
        /// Gets or sets the amount of time, in milliseconds, that the SqlCeReplication object waits for a connection to the server. 
        /// </summary>
        public int ConnectTimeout
        {
            get
            {
                return _connectTimeout;
            }
            set
            {
                _connectTimeout = value;
            }
        }

        private int _receiveTimeout = 60000;
        /// <summary>
        /// Gets or sets the amount of time, in milliseconds, that the SqlCeReplication object waits for the response to a server request. 
        /// </summary>
        public int ReceiveTimeout
        {
            get
            {
                return _receiveTimeout;
            }
            set
            {
                _receiveTimeout = value;
            }
        }
        /// <summary>
        /// Gets or sets the amount of time, in milliseconds, that the SqlCeReplication object waits to send a request to the server. 
        /// </summary>
        public int SendTimeout { get; set; }
    }
}
