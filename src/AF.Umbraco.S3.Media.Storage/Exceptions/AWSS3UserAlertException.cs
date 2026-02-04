using System;

namespace AF.Umbraco.S3.Media.Storage.Exceptions
{

    /// <summary>
    /// Represents a storage exception intended to be shown to users with a localized message.
    /// </summary>
    public class AWSS3UserAlertException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AWSS3UserAlertException"/> class.
        /// </summary>
        /// <param name="message">Localized user-facing message.</param>
        public AWSS3UserAlertException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AWSS3UserAlertException"/> class.
        /// </summary>
        /// <param name="message">Localized user-facing message.</param>
        /// <param name="innerException">Inner exception.</param>
        public AWSS3UserAlertException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
