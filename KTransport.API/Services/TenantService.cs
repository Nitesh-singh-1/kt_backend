using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KTransport.API.Services
{
    public class TenantService : ITenantService
    {
        private readonly ILogger<TenantService> _logger;
        private readonly KTransportDbContext _context;
        private readonly JwtSettings _jwtSettings;

        public TenantService(
            ILogger<TenantService> logger, 
            KTransportDbContext context, 
            IOptions<JwtSettings> jwtSettings)
        {
            _logger = logger;
            _context = context;
            _jwtSettings = jwtSettings.Value;
        }

        public async Task<TenantOnboardingResponse> OnboardTenantAsync(TenantOnboardingRequest request)
        {
            try
            {
                _logger.LogInformation("Onboarding new tenant: {OrgName} ({OrgCode})", request.OrganizationName, request.OrganizationCode);

                var normalizedCode = request.OrganizationCode.Trim().ToUpperInvariant();

                // Check if tenant code already exists
                var existingTenant = await _context.Tenants
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.Code.ToUpper() == normalizedCode);

                if (existingTenant != null)
                {
                    return new TenantOnboardingResponse
                    {
                        Success = false,
                        Message = $"Organization code '{request.OrganizationCode}' is already registered."
                    };
                }

                // Check if admin username exists across tenants or within tenant
                var existingUser = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == request.AdminUsername.Trim().ToLower());

                if (existingUser != null)
                {
                    return new TenantOnboardingResponse
                    {
                        Success = false,
                        Message = $"Username '{request.AdminUsername}' is already taken."
                    };
                }

                // 1. Create Tenant
                var tenant = new Tenant
                {
                    Id = Guid.NewGuid(),
                    Name = request.OrganizationName.Trim(),
                    Code = normalizedCode,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Tenants.Add(tenant);
                await _context.SaveChangesAsync();

                // 2. Create Admin User for this Tenant
                var adminUser = new User
                {
                    TenantId = tenant.Id,
                    Username = request.AdminUsername.Trim(),
                    Password = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
                    FullName = request.AdminFullName.Trim(),
                    Mobile = request.AdminMobile,
                    Role = "admin",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(adminUser);
                await _context.SaveChangesAsync();

                // 3. Generate JWT Token for immediate login
                var token = GenerateJwtToken(adminUser);

                return new TenantOnboardingResponse
                {
                    Success = true,
                    Message = "Tenant and administrator onboarded successfully.",
                    TenantId = tenant.Id,
                    OrganizationName = tenant.Name,
                    OrganizationCode = tenant.Code,
                    Token = token,
                    AdminUser = new UserDto
                    {
                        Id = adminUser.Id,
                        Username = adminUser.Username,
                        FullName = adminUser.FullName ?? string.Empty,
                        Role = adminUser.Role ?? "admin",
                        Mobile = adminUser.Mobile
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error onboarding tenant {OrgName}", request.OrganizationName);
                return new TenantOnboardingResponse
                {
                    Success = false,
                    Message = "An unexpected error occurred during tenant onboarding."
                };
            }
        }

        public async Task<Tenant?> GetTenantByIdAsync(Guid tenantId)
        {
            return await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantId);
        }

        public async Task<IEnumerable<Tenant>> GetAllTenantsAsync()
        {
            return await _context.Tenants
                .IgnoreQueryFilters()
                .ToListAsync();
        }

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.Secret);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role ?? "admin"),
                new Claim("FullName", user.FullName ?? string.Empty),
                new Claim("tenant_id", user.TenantId.ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryInMinutes),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
