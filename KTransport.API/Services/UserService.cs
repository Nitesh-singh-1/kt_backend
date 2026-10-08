using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    public class UserService : IUserService
    {
        private readonly KTransportDbContext _context;
        private readonly IFeatureAuthorizationService _authService;
        private readonly IAuditLogService _auditLogService;

        public UserService(KTransportDbContext context, IFeatureAuthorizationService authService, IAuditLogService auditLogService)
        {
            _context = context;
            _authService = authService;
            _auditLogService = auditLogService;
        }

        public async Task<List<SubUserDetailsDto>> GetTenantUsersAsync(Guid tenantId)
        {
            var users = await _context.Users
                .Where(u => u.TenantId == tenantId)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            // TASK-044 Phase 3: assigned features now come from user_permission_overrides,
            // keyed on userId. Fetch the whole set up-front to avoid N+1.
            var allOverrides = await _context.UserPermissionOverrides
                .IgnoreQueryFilters()
                .Where(o => o.TenantId == tenantId && o.IsGranted && o.SupersededBy == null)
                .ToListAsync();
            var overridesByUser = allOverrides
                .GroupBy(o => o.UserId)
                .ToDictionary(g => g.Key, g => g.Select(o => o.PermissionKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList());

            var result = new List<SubUserDetailsDto>();

            foreach (var u in users)
            {
                var assigned = overridesByUser.TryGetValue(u.Id, out var keys) ? keys : new List<string>();
                var effective = await _authService.GetEffectiveFeaturesForUserAsync(tenantId, u.Role ?? "SUB_USER", u.Username);

                result.Add(new SubUserDetailsDto
                {
                    Id = u.Id,
                    TenantId = u.TenantId,
                    Username = u.Username,
                    FullName = u.FullName ?? string.Empty,
                    Role = u.Role ?? "SUB_USER",
                    Mobile = u.Mobile,
                    Email = u.Email,
                    IsActive = u.IsActive ?? true,
                    CreatedAt = u.CreatedAt,
                    AssignedFeatures = assigned,
                    EffectiveFeatures = effective.ToList()
                });
            }

            return result;
        }

        public async Task<SubUserDetailsDto?> GetUserByIdAsync(Guid tenantId, int userId)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId);

            if (user == null) return null;

            var effective = await _authService.GetEffectiveFeaturesForUserAsync(tenantId, user.Role ?? "SUB_USER", user.Username);

            var assigned = await _context.UserPermissionOverrides
                .IgnoreQueryFilters()
                .Where(o => o.TenantId == tenantId && o.UserId == userId && o.IsGranted && o.SupersededBy == null)
                .Select(o => o.PermissionKey)
                .ToListAsync();

            return new SubUserDetailsDto
            {
                Id = user.Id,
                TenantId = user.TenantId,
                Username = user.Username,
                FullName = user.FullName ?? string.Empty,
                Role = user.Role ?? "SUB_USER",
                Mobile = user.Mobile,
                Email = user.Email,
                IsActive = user.IsActive ?? true,
                CreatedAt = user.CreatedAt,
                AssignedFeatures = assigned,
                EffectiveFeatures = effective.ToList()
            };
        }

        public async Task<(bool Success, string Message, SubUserDetailsDto? Data)> CreateSubUserAsync(Guid tenantId, CreateSubUserRequest request)
        {
            var existing = await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.Trim().ToLower());

            if (existing != null)
            {
                return (false, $"Username '{request.Username}' is already taken.", null);
            }

            var subscribedFeatures = await _authService.GetSubscribedFeaturesAsync(tenantId);
            var validAssigned = new List<string>();

            if (request.AssignedFeatures != null && request.AssignedFeatures.Count > 0)
            {
                foreach (var feat in request.AssignedFeatures)
                {
                    var canon = FeatureConstants.Normalize(feat);
                    var root = feat.Contains('.') ? feat.Substring(0, feat.IndexOf('.')) : feat;
                    var canonRoot = canon.Contains('.') ? canon.Substring(0, canon.IndexOf('.')) : canon;
                    if (subscribedFeatures.Contains(feat) || subscribedFeatures.Contains(canon) ||
                        subscribedFeatures.Contains(root) || subscribedFeatures.Contains(canonRoot))
                    {
                        validAssigned.Add(feat);
                    }
                    else
                    {
                        return (false, $"Cannot assign feature '{feat}' because the organization has not subscribed to it.", null);
                    }
                }
            }

            var newUser = new User
            {
                TenantId = tenantId,
                Username = request.Username.Trim(),
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                FullName = request.FullName.Trim(),
                Mobile = request.Mobile?.Trim(),
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                Role = string.IsNullOrWhiteSpace(request.Role) ? "SUB_USER" : request.Role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            await SaveUserFeatureAssignmentsAsync(tenantId, newUser.Id, validAssigned, actingUserId: null);

            await _auditLogService.LogAsync("UserCreated", tenantId: tenantId, entityType: "User", entityId: newUser.Id.ToString(), details: $"Created sub-user '{newUser.Username}' with role '{newUser.Role}'");

            var effective = await _authService.GetEffectiveFeaturesForUserAsync(tenantId, newUser.Role, newUser.Username);

            var result = new SubUserDetailsDto
            {
                Id = newUser.Id,
                TenantId = newUser.TenantId,
                Username = newUser.Username,
                FullName = newUser.FullName ?? string.Empty,
                Role = newUser.Role ?? "SUB_USER",
                Mobile = newUser.Mobile,
                Email = newUser.Email,
                IsActive = newUser.IsActive ?? true,
                CreatedAt = newUser.CreatedAt,
                AssignedFeatures = validAssigned,
                EffectiveFeatures = effective.ToList()
            };

            return (true, "Sub-user created successfully.", result);
        }

        public async Task<(bool Success, string Message, SubUserDetailsDto? Data)> UpdateSubUserAsync(Guid tenantId, int userId, UpdateSubUserRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId);

            if (user == null)
            {
                return (false, "User not found.", null);
            }

            if (!string.IsNullOrWhiteSpace(request.FullName)) user.FullName = request.FullName.Trim();
            if (request.Mobile != null) user.Mobile = request.Mobile.Trim();
            if (request.Email != null) user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
            if (!string.IsNullOrWhiteSpace(request.Role)) user.Role = request.Role.Trim();
            if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

            await _context.SaveChangesAsync();

            return (true, "User updated successfully.", await GetUserByIdAsync(tenantId, userId));
        }

        public async Task<(bool Success, string Message)> UpdateUserPermissionsAsync(Guid tenantId, int userId, UpdateUserPermissionsRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId);

            if (user == null)
            {
                return (false, "User not found.");
            }

            var subscribedFeatures = await _authService.GetSubscribedFeaturesAsync(tenantId);
            var validAssigned = new List<string>();

            if (request.AssignedFeatures != null)
            {
                foreach (var feat in request.AssignedFeatures)
                {
                    var canon = FeatureConstants.Normalize(feat);
                    var root = feat.Contains('.') ? feat.Substring(0, feat.IndexOf('.')) : feat;
                    var canonRoot = canon.Contains('.') ? canon.Substring(0, canon.IndexOf('.')) : canon;
                    if (subscribedFeatures.Contains(feat) || subscribedFeatures.Contains(canon) ||
                        subscribedFeatures.Contains(root) || subscribedFeatures.Contains(canonRoot))
                    {
                        validAssigned.Add(feat);
                    }
                    else
                    {
                        return (false, $"Cannot assign feature '{feat}' because the organization has not subscribed to it.");
                    }
                }
            }

            await SaveUserFeatureAssignmentsAsync(tenantId, user.Id, validAssigned, actingUserId: null);
            return (true, "User permissions updated successfully.");
        }

        public async Task<(bool Success, string Message)> ToggleUserStatusAsync(Guid tenantId, int userId, bool isActive)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId);

            if (user == null) return (false, "User not found.");

            user.IsActive = isActive;
            await _context.SaveChangesAsync();

            return (true, $"User {(isActive ? "activated" : "deactivated")} successfully.");
        }

        public async Task<(bool Success, string Message)> DeleteUserAsync(Guid tenantId, int userId)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId);

            if (user == null) return (false, "User not found.");

            user.IsActive = false;
            await _context.SaveChangesAsync();

            return (true, "User deactivated successfully.");
        }

        public async Task<(bool Success, string Message)> AdminResetUserPasswordAsync(Guid tenantId, int userId, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                return (false, "Password must be at least 6 characters long.");
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId);

            if (user == null) return (false, "User not found.");

            user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync();

            return (true, $"Password for user '{user.Username}' was reset successfully.");
        }

        /// <summary>
        /// TASK-044 Phase 3: writes directly into user_permission_overrides. The
        /// legacy menu_entitlements_json column was dropped.
        /// Handles expansion of feature keys and direct action keys, and supersedes
        /// any previously active overrides that are no longer assigned.
        /// </summary>
        private async Task SaveUserFeatureAssignmentsAsync(Guid tenantId, int userId, List<string> features, int? actingUserId)
        {
            var desiredPerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var featureKey in features)
            {
                if (string.IsNullOrWhiteSpace(featureKey)) continue;

                var expanded = EntitlementsCatalog.ExpandFeatureKey(featureKey);
                if (expanded.Count > 0)
                {
                    foreach (var k in expanded) desiredPerms.Add(k);
                }
                else
                {
                    desiredPerms.Add(featureKey.Trim());
                }
            }

            var existingActive = await _context.UserPermissionOverrides
                .IgnoreQueryFilters()
                .Where(o => o.TenantId == tenantId && o.UserId == userId && o.IsGranted && o.SupersededBy == null)
                .ToListAsync();

            var existingMap = existingActive.ToDictionary(o => o.PermissionKey, o => o, StringComparer.OrdinalIgnoreCase);

            // Mark removed overrides as superseded
            foreach (var kvp in existingMap)
            {
                if (!desiredPerms.Contains(kvp.Key))
                {
                    kvp.Value.SupersededBy = 0; // mark superseded
                    kvp.Value.RevokeReason = "Permission removed during user configuration update.";
                }
            }

            // Add newly granted overrides
            foreach (var permKey in desiredPerms)
            {
                if (existingMap.ContainsKey(permKey)) continue;

                _context.UserPermissionOverrides.Add(new UserPermissionOverride
                {
                    TenantId = tenantId,
                    UserId = userId,
                    PermissionKey = permKey,
                    IsGranted = true,
                    GrantedAt = DateTime.UtcNow,
                    GrantedBy = actingUserId,
                    SupersededBy = null
                });
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // partial unique index enforces idempotency
            }
        }
    }
}
