using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KTransport.API.DTOs
{
    public class CreateInviteRequest
    {
        [Required]
        [EmailAddress]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = "SUB_USER";

        public List<string> AssignedFeatures { get; set; } = new();
    }

    public class InviteDto
    {
        public long Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public List<string> AssignedFeatures { get; set; } = new();
        public string Status { get; set; } = "Pending";
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool EmailSent { get; set; }
        /// <summary>Populated only when email delivery is disabled (dev), so the admin can share the link manually.</summary>
        public string? AcceptUrl { get; set; }
    }

    /// <summary>Public info shown on the accept-invite page before the invitee submits.</summary>
    public class InviteInfoDto
    {
        public bool Valid { get; set; }
        public string? Email { get; set; }
        public string? OrganizationName { get; set; }
        public string? Role { get; set; }
        public string? Message { get; set; }
    }

    public class AcceptInviteRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(10)]
        public string? Mobile { get; set; }
    }
}
