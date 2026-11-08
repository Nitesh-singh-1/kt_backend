using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace KTransport.API.Services.Rbac
{
    /// <summary>
    /// RBAC clean-slate (ADR rbac-menu-consolidation-clean-slate §2.5): the ONE
    /// place that resolves a user's super-user flag + primary role code from DB
    /// grants (user_roles → roles.code), and the ONE JWT generator used by login,
    /// register, refresh, and both onboarding paths. users.role is never read.
    /// </summary>
    public static class RbacTokenHelper
    {
        /// <summary>
        /// Resolve (isSuperUser, primaryRoleCode) purely from user_roles → roles.
        /// isSuperUser = holds an active admin-class role. primaryRoleCode = that
        /// admin code if present, else the first active role code, else "SUB_USER".
        /// </summary>
        public static async Task<(bool IsSuperUser, string RoleCode)> ResolvePrincipalAsync(
            KTransportDbContext db, Guid tenantId, int userId)
        {
            var roleCodes = await (
                from ur in db.UserRoles.IgnoreQueryFilters()
                join r in db.Roles.IgnoreQueryFilters() on ur.RoleId equals r.Id
                where ur.TenantId == tenantId && ur.UserId == userId && ur.RevokedAt == null && r.IsActive
                select r.Code).ToListAsync();

            var adminCode = roleCodes.FirstOrDefault(c => RbacRoles.AdminRoleCodeList.Contains(c.ToLower()));
            if (adminCode != null) return (true, adminCode);
            if (roleCodes.Count > 0) return (false, roleCodes[0]);
            return (false, "SUB_USER");
        }

        public static string GenerateToken(User user, bool isSuperUser, string roleCode, JwtSettings jwt)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(jwt.Secret);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, string.IsNullOrWhiteSpace(roleCode) ? "SUB_USER" : roleCode),
                new Claim("FullName", user.FullName ?? string.Empty),
                new Claim("tenant_id", user.TenantId.ToString()),
                new Claim("is_super_user", isSuperUser ? "true" : "false")
            };

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(jwt.ExpiryInMinutes),
                Issuer = jwt.Issuer,
                Audience = jwt.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            return tokenHandler.WriteToken(tokenHandler.CreateToken(descriptor));
        }
    }
}
