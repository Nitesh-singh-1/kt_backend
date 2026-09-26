using System.ComponentModel.DataAnnotations;

namespace KTransport.API.Models
{
    public class UpdateProfileRequest
    {
        [MaxLength(100)]
        public string? FullName { get; set; }

        [MaxLength(10)]
        public string? Mobile { get; set; }

        [MaxLength(150)]
        [EmailAddress]
        public string? Email { get; set; }
    }

    public class MyProfileDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? Mobile { get; set; }
        public string? Email { get; set; }

        /// <summary>True only for the platform operator (super user of the default tenant). Drives
        /// visibility of platform-only UI such as the entitlement/subscription configurator.</summary>
        public bool IsPlatformAdmin { get; set; }
    }
}
