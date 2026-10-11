namespace KTransport.API.Services.Email
{
    /// <summary>
    /// SMTP configuration, bound from the "Email" section of configuration.
    /// When Enabled is false (the default in Development), no real mail is sent — the message is
    /// logged instead — so the app runs without a mail server and the reset flow stays testable.
    /// </summary>
    public class EmailSettings
    {
        public bool Enabled { get; set; } = false;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool UseSsl { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
        public string FromName { get; set; } = "KTransport";

        /// <summary>
        /// When true AND email delivery is unavailable, the raw OTP is included in the
        /// forgot-password API response so the flow stays testable without a mail server.
        /// Defaults to false and is intentionally NOT wired to any deploy-time env var — only
        /// appsettings.Development.json turns it on, so it can never leak into prod/UAT via a
        /// forgotten .env setting.
        /// </summary>
        public bool ExposeOtpWhenDisabled { get; set; } = false;
    }
}
