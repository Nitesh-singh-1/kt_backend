using System;
using System.Collections.Generic;
using System.Linq;

namespace KTransport.API.Common
{
    /// <summary>
    /// Single source of truth for "which role strings count as an admin / super
    /// user" across the whole backend.
    ///
    /// Before this existed, the same alias set was hand-copied into
    /// <c>AuthService</c>, <c>FeatureAuthorizationService</c> (twice),
    /// <c>TenantService</c> and <c>EntitlementsService</c> — and they had
    /// already drifted (EntitlementsService's admin-user query omitted
    /// <c>super_user</c> and <c>tenant_owner</c>), so "who is an admin"
    /// silently depended on which code path ran. Every authorization decision
    /// about admin status must now go through <see cref="IsAdminRole"/> /
    /// <see cref="AdminRoleCodes"/>.
    ///
    /// Note the layering:
    /// - <see cref="IsAdminRole"/> == "super user of SOME tenant" (a tenant
    ///   admin passes). Correct for own-tenant config endpoints, which are
    ///   additionally tenant-isolated by the EF global query filter.
    /// - PLATFORM authority (cross-tenant) additionally requires membership in
    ///   the default/platform tenant — see
    ///   <c>FeatureAuthorizationService.IsPlatformAdmin</c>. A tenant admin is
    ///   never in the default tenant, so this set never grants platform access
    ///   on its own.
    ///
    /// These codes are RESERVED: custom tenant roles (added in the role-CRUD
    /// work) must not reuse any of them, or a tenant could mint itself an
    /// admin-class role. Enforce with <see cref="IsReservedRoleCode"/>.
    /// </summary>
    public static class RbacRoles
    {
        /// <summary>The platform operator's role string (super user of the default tenant).</summary>
        public const string PlatformSuperUser = "SUPER_USER";

        /// <summary>The canonical tenant-admin role code seeded for every tenant.</summary>
        public const string TenantAdmin = "admin";

        /// <summary>
        /// All role strings treated as admin / super user, case-insensitive.
        /// Historical aliases are kept so no existing user loses access;
        /// <see cref="PlatformSuperUser"/> ("SUPER_USER") is covered by
        /// "super_user" case-insensitively.
        /// </summary>
        public static readonly IReadOnlySet<string> AdminRoleCodes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "admin",
                "superadmin",
                "tenantadmin",
                "super_user",
                "tenant_owner",
            };

        /// <summary>True if the given role string is an admin / super-user role.</summary>
        public static bool IsAdminRole(string? role)
            => !string.IsNullOrWhiteSpace(role) && AdminRoleCodes.Contains(role.Trim());

        /// <summary>
        /// True if the given role code is reserved (an admin alias) and so must
        /// not be assignable as a custom tenant role.
        /// </summary>
        public static bool IsReservedRoleCode(string? code)
            => IsAdminRole(code);

        /// <summary>
        /// Materialized list form for EF Core <c>Contains</c> translation in a
        /// DB query (EF cannot translate a HashSet instance).
        /// </summary>
        public static readonly List<string> AdminRoleCodeList = AdminRoleCodes.ToList();
    }
}
