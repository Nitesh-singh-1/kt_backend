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
    }
}