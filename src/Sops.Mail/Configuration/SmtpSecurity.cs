namespace Sops.Mail.Configuration
{
    /// <summary>
    /// Specifies the TLS/SSL transport security mode for connecting to an SMTP server.
    /// </summary>
    public enum SmtpSecurity
    {
        /// <summary>
        /// Automatically selects the best security mode based on the port and server capabilities.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// Connects using an SSL/TLS socket immediately upon connection (typically port 465).
        /// </summary>
        SslOnConnect = 1,

        /// <summary>
        /// Connects using plain text and immediately negotiates TLS via STARTTLS (typically port 587). Fails if STARTTLS is not supported.
        /// </summary>
        StartTls = 2,

        /// <summary>
        /// Negotiates STARTTLS if advertised by the server, otherwise proceeds unencrypted.
        /// </summary>
        StartTlsWhenAvailable = 3,

        /// <summary>
        /// Uses plain-text communication without encryption (suitable only for local development/testing).
        /// </summary>
        None = 4
    }
}
