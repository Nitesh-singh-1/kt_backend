using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace KTransport.API.Services.Email
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<EmailSettings> settings, ILogger<SmtpEmailSender> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public bool IsEnabled => _settings.Enabled && !string.IsNullOrWhiteSpace(_settings.Host);

        public async Task<bool> SendAsync(string toAddress, string subject, string htmlBody, string? fromDisplayName = null)
        {
            if (string.IsNullOrWhiteSpace(toAddress))
            {
                _logger.LogWarning("Email not sent: no recipient address for subject '{Subject}'.", subject);
                return false;
            }

            // Dev / unconfigured mode: log instead of sending so flows work without a mail server.
            if (!IsEnabled)
            {
                _logger.LogInformation(
                    "[Email disabled] Would send to {To} | Subject: {Subject}", toAddress, subject);
                return false;
            }

            try
            {
                var displayName = string.IsNullOrWhiteSpace(fromDisplayName) ? _settings.FromName : fromDisplayName;
                using var message = new MailMessage
                {
                    From = new MailAddress(_settings.FromAddress, displayName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                message.To.Add(toAddress);

                using var client = new SmtpClient(_settings.Host, _settings.Port)
                {
                    EnableSsl = _settings.UseSsl,
                    Credentials = string.IsNullOrWhiteSpace(_settings.Username)
                        ? CredentialCache.DefaultNetworkCredentials
                        : new NetworkCredential(_settings.Username, _settings.Password)
                };

                await client.SendMailAsync(message);
                _logger.LogInformation("Email sent to {To} | Subject: {Subject}", toAddress, subject);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To} | Subject: {Subject}", toAddress, subject);
                return false;
            }
        }
    }
}
