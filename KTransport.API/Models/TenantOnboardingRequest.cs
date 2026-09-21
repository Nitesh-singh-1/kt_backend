namespace KTransport.API.Models
{
    public class TenantOnboardingRequest
    {
        public string OrganizationName { get; set; } = null!;
        public string OrganizationCode { get; set; } = null!;
        public string AdminUsername { get; set; } = null!;
        public string AdminPassword { get; set; } = null!;
        public string AdminFullName { get; set; } = null!;
        public string? AdminMobile { get; set; }
    }

    public class TenantOnboardingResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Guid? TenantId { get; set; }
        public string? OrganizationName { get; set; }
        public string? OrganizationCode { get; set; }
        public string? Token { get; set; }
        public UserDto? AdminUser { get; set; }
    }
}
