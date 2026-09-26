using System.Threading.Tasks;

namespace KTransport.API.Services
{
    public interface IVerificationCodeService
    {
        /// <summary>
        /// Generates a fresh 6-digit code for the username/purpose, stores it hashed with an expiry,
        /// and invalidates any earlier unconsumed codes for the same username/purpose.
        /// Returns the plaintext code (for delivery only — it is never persisted in plaintext).
        /// </summary>
        Task<string> GenerateAsync(string username, string purpose, int validMinutes);

        /// <summary>
        /// Verifies a submitted code against the latest active code for the username/purpose.
        /// On success the code is marked consumed. Enforces expiry and a max attempt count.
        /// </summary>
        Task<VerificationResult> VerifyAsync(string username, string purpose, string code, int maxAttempts = 5);
    }

    public enum VerificationResult
    {
        Success,
        NotFound,
        Expired,
        Invalid,
        TooManyAttempts
    }
}
