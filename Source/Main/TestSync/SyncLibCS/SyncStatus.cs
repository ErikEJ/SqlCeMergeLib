namespace ErikEJ.SqlCeMergeLib
{
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
