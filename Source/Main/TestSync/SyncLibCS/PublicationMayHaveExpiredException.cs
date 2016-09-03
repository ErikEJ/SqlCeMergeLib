using System.Data.SqlServerCe;

namespace ErikEJ.SqlCeMergeLib
{
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
        /// <param name="innerException"></param>
        public PublicationMayHaveExpiredException(string message,
        SqlCeException innerException) : base(message, innerException)
        {
        }
    }
}
