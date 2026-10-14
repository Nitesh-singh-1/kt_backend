using System;
using System.Collections.Generic;
using KTransport.API.Models;

namespace KTransport.API.Services.Rbac
{
    /// <summary>
    /// Single source of truth for "which catalog permissions does each of the
    /// 7 system roles get". Before this existed, the same rule set was
    /// duplicated in two places that silently drifted: the coarse
    /// module+action matcher in <c>TenantService.PermissionFitsRoleSeedGrant</c>
    /// (ran at onboarding for new tenants) and a hand-written SQL migration
    /// (<c>20261013220200_SeedNonAdminSystemRolePermissions</c>, a one-time
    /// backfill for tenants onboarded before the matcher existed). The drift
    /// is exactly how a brand-new tenant's non-admin roles could end up with
    /// zero permissions, and how admin roles ended up incomplete whenever a
    /// tenant didn't have every module enabled.
    ///
    /// Both the onboarding seeder (<see cref="TenantService"/>) and any
    /// future reconciliation job/admin tool must call
    /// <see cref="Grants"/> instead of re-encoding the rules locally.
    /// </summary>
    public static class SystemRolePermissionMatrix
    {
        /// <summary>
        /// The 7 system role codes, in seed order. Kept here (not just in
        /// TenantService) so any caller can enumerate the full set.
        /// </summary>
        public static readonly string[] SystemRoleCodes =
        {
            "admin", "operations_manager", "supervisor", "operator",
            "billing_executive", "accountant", "viewer",
        };

        /// <summary>
        /// True if the given permission belongs on the given system role,
        /// by role business scope alone — NOT yet filtered by which modules
        /// the tenant has enabled. Callers decide separately whether module
        /// scoping applies:
        /// - "admin" must get EVERY catalog permission regardless of the
        ///   tenant's enabled modules (an admin should never lose access to
        ///   settings/management just because a module toggle is off).
        /// - every other role should additionally be ANDed against the
        ///   tenant's enabled-module set.
        /// </summary>
        public static bool Grants(string roleCode, Permission permission)
        {
            if (permission == null) throw new ArgumentNullException(nameof(permission));
            var key = permission.Key ?? string.Empty;
            var action = permission.Action ?? string.Empty;

            switch (roleCode?.ToLowerInvariant())
            {
                case "admin":
                    return true;

                case "operations_manager":
                    return StartsWithAny(key, "consignments.", "trips.", "empty_trips.", "pod.",
                               "delivery_settlement.", "trip_settlement.", "master_data.fleet.",
                               "master_data.compliance.", "tracking.", "reports.booking_register")
                           || key is "dashboard.view" or "reports.view" or "vendors.view";

                case "supervisor":
                    return key.EndsWith(".view", StringComparison.OrdinalIgnoreCase)
                           || action == "View"
                           || (StartsWithAny(key, "consignments.", "trips.", "pod.")
                               && (action is "Create" or "Edit"
                                   || key.EndsWith(".create", StringComparison.OrdinalIgnoreCase)
                                   || key.EndsWith(".edit", StringComparison.OrdinalIgnoreCase)));

                case "operator":
                    return (StartsWithAny(key, "consignments.", "trips.")
                               && (action is "Create" or "Edit" or "View"
                                   || key.EndsWith(".create", StringComparison.OrdinalIgnoreCase)
                                   || key.EndsWith(".edit", StringComparison.OrdinalIgnoreCase)
                                   || key.EndsWith(".view", StringComparison.OrdinalIgnoreCase)))
                           || (key.StartsWith("pod.", StringComparison.OrdinalIgnoreCase)
                               && (action is "View" or "Edit" || key is "pod.view" or "pod.edit"))
                           || key is "dashboard.view" or "master_data.parties.view" or "master_data.fleet.view";

                case "billing_executive":
                    return key.StartsWith("billing.", StringComparison.OrdinalIgnoreCase)
                           || key.StartsWith("reports.booking_register", StringComparison.OrdinalIgnoreCase)
                           || key is "consignments.all.view" or "consignments.view" or "reports.view" or "dashboard.view";

                case "accountant":
                    return (key.StartsWith("billing.", StringComparison.OrdinalIgnoreCase)
                               && (key.EndsWith(".view", StringComparison.OrdinalIgnoreCase)
                                   || key.EndsWith(".export", StringComparison.OrdinalIgnoreCase)
                                   || action is "View" or "Export"))
                           || key.StartsWith("reports.", StringComparison.OrdinalIgnoreCase)
                           || key.StartsWith("master_data.driver_ledger.", StringComparison.OrdinalIgnoreCase)
                           || key is "master_data.loans.view" or "dashboard.view";

                case "viewer":
                    return key.EndsWith(".view", StringComparison.OrdinalIgnoreCase) || action == "View";

                default:
                    // Custom / non-system role: no implicit grants. Access is
                    // managed entirely via explicit role_permissions rows.
                    return false;
            }
        }

        private static bool StartsWithAny(string value, params string[] prefixes)
        {
            foreach (var p in prefixes)
            {
                if (value.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
