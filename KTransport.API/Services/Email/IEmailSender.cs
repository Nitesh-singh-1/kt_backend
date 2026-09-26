using System.Threading.Tasks;

namespace KTransport.API.Services.Email
{
    public interface IEmailSender
    {
        /// <summary>True when a real SMTP transport is configured and enabled.</summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Sends an HTML email. Returns true if the message was handed off to the transport.
        /// Never throws — delivery failures are logged and reported as false so callers can decide
        /// how to proceed (e.g. fall back to logging the OTP).
        /// </summary>
        Task<bool> SendAsync(string toAddress, string subject, string htmlBody);
    }
}
