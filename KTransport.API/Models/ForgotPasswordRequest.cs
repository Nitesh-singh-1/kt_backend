using System.ComponentModel.DataAnnotations;

namespace KTransport.API.Models
{
    public class ForgotPasswordRequest
    {
        [Required]
        public string Username { get; set; } = null!;

        [Required]
        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Mobile number must be a valid 10-digit Indian mobile number.")]
        public string Mobile { get; set; } = null!;

        [Required]
        [StringLength(200, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
        public string NewPassword { get; set; } = null!;
    }

    public class RequestResetCodeRequest
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Mobile number must be a valid 10-digit Indian mobile number.")]
        public string Mobile { get; set; } = string.Empty;
    }

    public class VerifyAndResetPasswordRequest
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Mobile number must be a valid 10-digit Indian mobile number.")]
        public string Mobile { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Verification code must be exactly 6 digits.")]
        public string VerificationCode { get; set; } = string.Empty;

        [Required]
        [StringLength(200, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
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
        [Required]
        public string OldPassword { get; set; } = null!;

        [Required]
        [StringLength(200, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
        public string NewPassword { get; set; } = null!;
    }

    public class AdminResetPasswordRequest
    {
        [Required]
        [StringLength(200, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ResetPasswordResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
