namespace KTransport.API.Models
{
    public class ForgotPasswordRequest
    {
        public string Username { get; set; } = null!;
        public string Mobile { get; set; } = null!;
        public string NewPassword { get; set; } = null!;
    }

    public class RequestResetCodeRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
    }

    public class VerifyAndResetPasswordRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string VerificationCode { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class RequestResetCodeResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? VerificationCode { get; set; }
        public int ExpiresInSeconds { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string OldPassword { get; set; } = null!;
        public string NewPassword { get; set; } = null!;
    }

    public class AdminResetPasswordRequest
    {
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ResetPasswordResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
