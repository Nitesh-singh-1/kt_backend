using System.Threading.Tasks;
using KTransport.API.Models;

namespace KTransport.API.Services
{
    public interface IAuthService
    {
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
        Task<AuthResponse> ValidateTokenAsync(string token);
        Task<RequestResetCodeResponse> RequestPasswordResetCodeAsync(RequestResetCodeRequest request);
        Task<ResetPasswordResponse> VerifyAndResetPasswordAsync(VerifyAndResetPasswordRequest request);
        Task<ResetPasswordResponse> ResetPasswordAsync(ForgotPasswordRequest request);
        Task<ResetPasswordResponse> ChangePasswordAsync(int userId, ChangePasswordRequest request);
        Task<MyProfileDto?> GetMyProfileAsync(int userId);
        Task<(bool Success, string Message, MyProfileDto? Profile)> UpdateMyProfileAsync(int userId, UpdateProfileRequest request);
        Task<AuthResponse> RefreshTokenAsync(string refreshToken);
        Task RevokeRefreshTokenAsync(string refreshToken);

        /// <summary>
        /// Anonymous availability probe for new usernames (used by the onboard + user-add forms).
        /// Returns Valid=false with a Reason when the input is empty / too short / too long /
        /// contains disallowed characters — the DB is only hit for well-formed candidates.
        /// Comparison is case-insensitive to match the Register/Onboarding duplicate checks.
        /// </summary>
        Task<(bool Valid, bool Available, string? Reason)> IsUsernameAvailableAsync(string? candidate);
    }
}