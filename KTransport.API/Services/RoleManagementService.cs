using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KTransport.API.Services
{
    public class RoleManagementService : IRoleManagementService
    {
        private readonly KTransportDbContext _db;
        private readonly IAuditLogService _audit;
        private readonly ILogger<RoleManagementService> _log;

        private static readonly Regex CodePattern = new("^[a-z0-9_]+$", RegexOptions.Compiled);

        public RoleManagementService(KTransportDbContext db, IAuditLogService audit, ILogger<RoleManagementService> log)
        {
            _db = db;
            _audit = audit;
            _log = log;
        }

        public async Task<List<RoleDto>> GetRolesAsync(Guid tenantId)
        {
            var roles = await _db.Roles.IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId)
                .OrderByDescending(r => r.IsSystem)
                .ThenBy(r => r.Name)
                .ToListAsync();

            var roleIds = roles.Select(r => r.Id).ToList();

            var permCounts = await _db.RolePermissions.IgnoreQueryFilters()
                .Where(rp => rp.TenantId == tenantId && rp.RevokedAt == null && rp.RoleId != null && roleIds.Contains(rp.RoleId!.Value))
                .GroupBy(rp => rp.RoleId!.Value)
                .Select(g => new { RoleId = g.Key, Count = g.Count() })
                .ToListAsync();
            var permCountByRole = permCounts.ToDictionary(x => x.RoleId, x => x.Count);

            var userCounts = await _db.UserRoles.IgnoreQueryFilters()
                .Where(ur => ur.TenantId == tenantId && ur.RevokedAt == null && roleIds.Contains(ur.RoleId))
                .GroupBy(ur => ur.RoleId)
                .Select(g => new { RoleId = g.Key, Count = g.Count() })
                .ToListAsync();
            var userCountByRole = userCounts.ToDictionary(x => x.RoleId, x => x.Count);

            return roles.Select(r => new RoleDto
            {
                Id = r.Id,
                Code = r.Code,
                Name = r.Name,
                Description = r.Description,
                IsSystem = r.IsSystem,
                IsActive = r.IsActive,
                PermissionCount = permCountByRole.TryGetValue(r.Id, out var pc) ? pc : 0,
                AssignedUserCount = userCountByRole.TryGetValue(r.Id, out var uc) ? uc : 0,
            }).ToList();
        }

        public async Task<AssignablePermissionsDto> GetAssignablePermissionsAsync(Guid tenantId)
        {
            var entitled = await GetEntitledPermissionsAsync(tenantId);

            var modules = entitled
                .GroupBy(p => new { p.ModuleCode, p.ModuleName })
                .OrderBy(g => g.Key.ModuleName)
                .Select(mg => new AssignableModuleDto
                {
                    ModuleCode = mg.Key.ModuleCode,
                    ModuleName = mg.Key.ModuleName,
                    Pages = mg.GroupBy(p => p.FeatureKey)
                        .OrderBy(pg => pg.Key)
                        .Select(pg => new AssignablePageDto
                        {
                            FeatureKey = pg.Key,
                            Actions = pg.OrderBy(a => a.Key)
                                .Select(a => new AssignableActionDto { Key = a.Key, Action = a.Action })
                                .ToList()
                        }).ToList()
                }).ToList();

            return new AssignablePermissionsDto { Modules = modules };
        }

        public async Task<RoleDetailDto?> GetRoleAsync(Guid tenantId, int roleId)
        {
            var role = await _db.Roles.IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == roleId);
            if (role == null) return null;

            var keys = await _db.RolePermissions.IgnoreQueryFilters()
                .Where(rp => rp.TenantId == tenantId && rp.RoleId == roleId && rp.RevokedAt == null)
                .Select(rp => rp.PermissionKey)
                .ToListAsync();

            return ToDetail(role, keys);
        }

        public async Task<(bool Success, string Message, RoleDetailDto? Data)> CreateRoleAsync(
            Guid tenantId, CreateRoleRequest request, int? actingUserId)
        {
            var code = (request.Code ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_');
            if (string.IsNullOrWhiteSpace(code) || !CodePattern.IsMatch(code))
                return (false, "Role code must be lower_snake_case (letters, digits, underscores).", null);

            // A tenant must never be able to mint a role that aliases an admin / super-user code.
            if (RbacRoles.IsReservedRoleCode(code))
                return (false, $"'{code}' is a reserved role code and cannot be used for a custom role.", null);

            if (string.IsNullOrWhiteSpace(request.Name))
                return (false, "Role name is required.", null);

            var exists = await _db.Roles.IgnoreQueryFilters()
                .AnyAsync(r => r.TenantId == tenantId && r.Code.ToLower() == code);
            if (exists)
                return (false, $"A role with code '{code}' already exists in this organization.", null);

            var (validKeys, rejected) = await ResolveEntitledPermissionKeysAsync(tenantId, request.PermissionKeys);
            if (rejected != null)
                return (false, rejected, null);

            var role = new Role
            {
                TenantId = tenantId,
                Code = code,
                Name = request.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                IsSystem = false,
                IsActive = true,
                CreatedBy = actingUserId,
                CreatedAt = DateTime.UtcNow,
            };
            _db.Roles.Add(role);
            await _db.SaveChangesAsync();

            foreach (var p in validKeys)
                AddGrant(tenantId, role, p.Id, p.Key, actingUserId);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("RoleCreated", tenantId: tenantId, userId: actingUserId,
                entityType: "Role", entityId: role.Id.ToString(),
                details: $"Created custom role '{code}' with {validKeys.Count} permissions.");

            return (true, "Role created.", ToDetail(role, validKeys.Select(p => p.Key).ToList()));
        }

        public async Task<(bool Success, string Message, RoleDetailDto? Data)> UpdateRoleAsync(
            Guid tenantId, int roleId, UpdateRoleRequest request, int? actingUserId)
        {
            var role = await _db.Roles.IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == roleId);
            if (role == null) return (false, "Role not found.", null);
            if (role.IsSystem)
                return (false, "System roles are managed by the platform and cannot be edited here.", null);

            if (!string.IsNullOrWhiteSpace(request.Name)) role.Name = request.Name.Trim();
            if (request.Description != null)
                role.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            role.UpdatedAt = DateTime.UtcNow;

            if (request.PermissionKeys != null)
            {
                var (validKeys, rejected) = await ResolveEntitledPermissionKeysAsync(tenantId, request.PermissionKeys);
                if (rejected != null)
                    return (false, rejected, null);

                var current = await _db.RolePermissions.IgnoreQueryFilters()
                    .Where(rp => rp.TenantId == tenantId && rp.RoleId == roleId && rp.RevokedAt == null)
                    .ToListAsync();
                var currentKeys = new HashSet<string>(current.Select(c => c.PermissionKey), StringComparer.OrdinalIgnoreCase);
                var desiredKeys = new HashSet<string>(validKeys.Select(p => p.Key), StringComparer.OrdinalIgnoreCase);

                // Revoke removed grants (soft — append-only discipline).
                foreach (var row in current.Where(c => !desiredKeys.Contains(c.PermissionKey)))
                {
                    row.RevokedAt = DateTime.UtcNow;
                    row.RevokedBy = actingUserId;
                    row.RevokeReason = "role_update";
                }
                // Grant newly added keys.
                foreach (var p in validKeys.Where(p => !currentKeys.Contains(p.Key)))
                    AddGrant(tenantId, role, p.Id, p.Key, actingUserId);
            }

            await _db.SaveChangesAsync();

            await _audit.LogAsync("RoleUpdated", tenantId: tenantId, userId: actingUserId,
                entityType: "Role", entityId: role.Id.ToString(),
                details: $"Updated role '{role.Code}'.");

            var keys = await _db.RolePermissions.IgnoreQueryFilters()
                .Where(rp => rp.TenantId == tenantId && rp.RoleId == roleId && rp.RevokedAt == null)
                .Select(rp => rp.PermissionKey)
                .ToListAsync();
            return (true, "Role updated.", ToDetail(role, keys));
        }

        public async Task<(bool Success, string Message)> DeactivateRoleAsync(
            Guid tenantId, int roleId, int? actingUserId)
        {
            var role = await _db.Roles.IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == roleId);
            if (role == null) return (false, "Role not found.");
            if (role.IsSystem)
                return (false, "System roles cannot be deactivated.");

            role.IsActive = false;
            role.UpdatedAt = DateTime.UtcNow;

            // The effective-permission query does not consult Role.IsActive, so to
            // actually drop access we soft-revoke this role's active grants. Any
            // user_roles assignment is left in place (historical) but now grants
            // nothing.
            var grants = await _db.RolePermissions.IgnoreQueryFilters()
                .Where(rp => rp.TenantId == tenantId && rp.RoleId == roleId && rp.RevokedAt == null)
                .ToListAsync();
            foreach (var g in grants)
            {
                g.RevokedAt = DateTime.UtcNow;
                g.RevokedBy = actingUserId;
                g.RevokeReason = "role_deactivated";
            }
            await _db.SaveChangesAsync();

            await _audit.LogAsync("RoleDeactivated", tenantId: tenantId, userId: actingUserId,
                entityType: "Role", entityId: role.Id.ToString(),
                details: $"Deactivated role '{role.Code}' and revoked {grants.Count} grants.");

            return (true, "Role deactivated.");
        }

        private void AddGrant(Guid tenantId, Role role, int permissionId, string permissionKey, int? actingUserId)
        {
            _db.RolePermissions.Add(new RolePermission
            {
                TenantId = tenantId,
                RoleName = role.Code,
                RoleId = role.Id,
                PermissionKey = permissionKey,
                PermissionId = permissionId,
                GrantedAt = DateTime.UtcNow,
                GrantedBy = actingUserId,
            });
        }

        private sealed record EntitledPermission(int Id, string Key, string FeatureKey, string Action, string ModuleCode, string ModuleName);

        /// <summary>
        /// The catalog permissions whose module the tenant currently has enabled
        /// (EnabledUntil IS NULL) — the exact set a tenant admin is allowed to
        /// assign. Mirrors the runtime entitlement gate in
        /// <see cref="EntitlementsService.ComputeEffectivePermissionsFromTablesAsync"/>.
        /// </summary>
        private async Task<List<EntitledPermission>> GetEntitledPermissionsAsync(Guid tenantId)
        {
            var moduleIds = await _db.TenantModules.IgnoreQueryFilters()
                .Where(tm => tm.TenantId == tenantId && tm.EnabledUntil == null)
                .Select(tm => tm.ModuleId)
                .ToListAsync();
            if (moduleIds.Count == 0) return new List<EntitledPermission>();

            var query = from p in _db.Permissions.IgnoreQueryFilters()
                        join m in _db.Modules.IgnoreQueryFilters() on p.ModuleId equals m.Id
                        where moduleIds.Contains(p.ModuleId)
                        select new EntitledPermission(p.Id, p.Key, p.FeatureKey, p.Action, m.Code, m.Name);
            return await query.ToListAsync();
        }

        /// <summary>
        /// Resolves requested keys against the tenant's ENTITLED permissions.
        /// Returns the valid (id, key) pairs, or a non-null rejection message
        /// naming keys that are unknown or outside the tenant's paid modules.
        /// </summary>
        private async Task<(List<(int Id, string Key)> Valid, string? Rejection)> ResolveEntitledPermissionKeysAsync(
            Guid tenantId, IEnumerable<string>? requested)
        {
            var wanted = (requested ?? Enumerable.Empty<string>())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (wanted.Count == 0) return (new List<(int, string)>(), null);

            var catalogKeys = new HashSet<string>(
                await _db.Permissions.IgnoreQueryFilters().Select(p => p.Key).ToListAsync(),
                StringComparer.OrdinalIgnoreCase);
            var entitled = await GetEntitledPermissionsAsync(tenantId);
            var entitledByKey = entitled.ToDictionary(e => e.Key, e => e.Id, StringComparer.OrdinalIgnoreCase);

            var valid = new List<(int, string)>();
            var unknown = new List<string>();
            var notEntitled = new List<string>();
            foreach (var k in wanted)
            {
                if (entitledByKey.TryGetValue(k, out var id))
                    valid.Add((id, entitled.First(e => string.Equals(e.Key, k, StringComparison.OrdinalIgnoreCase)).Key));
                else if (!catalogKeys.Contains(k))
                    unknown.Add(k);
                else
                    notEntitled.Add(k);
            }

            if (unknown.Count > 0 || notEntitled.Count > 0)
            {
                var parts = new List<string>();
                if (unknown.Count > 0) parts.Add($"unknown permission keys: {string.Join(", ", unknown)}");
                if (notEntitled.Count > 0) parts.Add($"not included in this organization's plan: {string.Join(", ", notEntitled)}");
                return (valid, "Cannot assign " + string.Join("; ", parts) + ".");
            }
            return (valid, null);
        }

        private static RoleDetailDto ToDetail(Role role, List<string> keys) => new()
        {
            Id = role.Id,
            Code = role.Code,
            Name = role.Name,
            Description = role.Description,
            IsSystem = role.IsSystem,
            IsActive = role.IsActive,
            PermissionKeys = keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }
}
