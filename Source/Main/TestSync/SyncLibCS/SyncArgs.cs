using System;

namespace ErikEJ.SqlCeMergeLib
{
    /// <summary>
    /// Sync args
    /// </summary>
    public class SyncArgs : EventArgs
    {
        /// <summary>
        /// Construct a new instance of SyncArgs
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        /// <param name="percentComplete"></param>
        /// <param name="status"></param>
        /// <param name="tableName"></param>
        public SyncArgs(string message, Exception ex, int percentComplete, SyncStatus status, string tableName)
        {
            Message = message;
            Exception = ex;
            PercentComplete = percentComplete;
            SyncStatus = status;
            TableName = tableName;
        }

        /// <summary>
        /// Message
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Exception
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Percentage complete
        /// </summary>
        public int PercentComplete { get; }

        /// <summary>
        /// Status
        /// </summary>
        public SyncStatus SyncStatus { get; }

        /// <summary>
        /// The table name
        /// </summary>
        public string TableName { get; }
    }
}
