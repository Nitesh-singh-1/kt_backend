namespace KTransport.API.Models
{
    public class ForgotPasswordRequest
    {
        public string Username { get; set; } = null!;
        public string Mobile { get; set; } = null!;
        public string NewPassword { get; set; } = null!;
    }

    public class ChangePasswordRequest
    {
        public string OldPassword { get; set; } = null!;
        public string NewPassword { get; set; } = null!;
    }

    public class ResetPasswordResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
