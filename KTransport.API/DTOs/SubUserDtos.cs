using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KTransport.API.DTOs
{
    public class CreateSubUserRequest
    {
        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(15)]
        public string? Mobile { get; set; }

        public string Role { get; set; } = "SUB_USER";

        public List<string> AssignedFeatures { get; set; } = new();
    }

    public class UpdateSubUserRequest
    {
        [MaxLength(100)]
        public string? FullName { get; set; }

        [MaxLength(15)]
        public string? Mobile { get; set; }

        public string? Role { get; set; }

        public bool? IsActive { get; set; }
    }

    public class UpdateUserPermissionsRequest
    {
        public List<string> AssignedFeatures { get; set; } = new();
    }

    public class UpdateUserStatusRequest
    {
        public bool IsActive { get; set; }
    }

    public class SubUserDetailsDto
    {
        public int Id { get; set; }
        public Guid TenantId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = "SUB_USER";
        public string? Mobile { get; set; }
        public bool IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public List<string> AssignedFeatures { get; set; } = new();
        public List<string> EffectiveFeatures { get; set; } = new();
    }
}
