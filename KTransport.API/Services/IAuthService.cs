using KTransport.API.Models;

namespace KTransport.API.Services
{
    public interface IAuthService
    {
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
        Task<AuthResponse> ValidateTokenAsync(string token);
        Task<ResetPasswordResponse> ResetPasswordAsync(ForgotPasswordRequest request);
        Task<ResetPasswordResponse> ChangePasswordAsync(int userId, ChangePasswordRequest request);
    }
}