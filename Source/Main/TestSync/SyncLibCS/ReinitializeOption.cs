namespace ErikEJ.SqlCeMergeLib
{
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
}
