using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;
using KTransport.API.Models;

namespace KTransport.API.Services
{
    public interface ITenantService
    {
        Task<TenantOnboardingResponse> OnboardTenantAsync(TenantOnboardingRequest request);

        /// <summary>
        /// TASK-046 Phase 1: platform-admin onboarding path. Respects
        /// <c>AdminRoleCode</c> + explicit <c>EnabledModuleCodes</c> from the
        /// caller, and seeds the 7 system roles for the new tenant.
        /// </summary>
        Task<TenantOnboardingResponse> OnboardTenantByAdminAsync(TenantAdminOnboardRequest request, int? platformAdminUserId);

        /// <summary>TASK-046 Phase 1: list the 7 seeded system role templates for the admin UI.</summary>
        IReadOnlyList<SystemRoleTemplateDto> GetSystemRoleTemplates();

        /// <summary>
        /// Re-syncs an EXISTING tenant's system-role permissions against the
        /// single authoritative matrix (<see cref="KTransport.API.Services.Rbac.SystemRolePermissionMatrix"/>).
        /// Idempotent and additive only (never revokes). This is the
        /// supported way to fix a tenant whose roles drifted (e.g. seeded
        /// before a role template existed, or before a new permission was
        /// added to the catalog) — call this instead of writing a one-off
        /// SQL migration.
        /// </summary>
        Task ReconcileSystemRolePermissionsForTenantAsync(Guid tenantId, int? actingUserId);
        Task<Tenant?> GetTenantByIdAsync(Guid tenantId);
        Task<IEnumerable<TenantAdminListItemDto>> GetAllTenantsWithDetailsAsync();
        Task<bool> UpdateTenantStatusAsync(Guid tenantId, bool isActive);
        Task<bool> UpdateTenantSubscriptionPlanAsync(Guid tenantId, string planTier);

        /// <summary>Lists the users of any tenant (platform-operator use — bypasses tenant filter).</summary>
        Task<IEnumerable<TenantUserDto>> GetTenantUsersAsync(Guid tenantId);

        /// <summary>
        /// Sets a user's role within a tenant (platform-operator use). Allowed roles are constrained
        /// to admin / standard user. Returns (success, message).
        /// </summary>
        Task<(bool Success, string Message)> SetTenantUserRoleAsync(Guid tenantId, int userId, string role);

        /// <summary>
        /// Snapshot of the tenant's current usage vs. plan limits, safe for any authenticated user
        /// of the tenant to read (used by the plan-usage warning banner). No enforcement or state
        /// change — pure derived read.
        /// </summary>
        Task<TenantUsageDto> GetUsageSnapshotAsync(Guid tenantId);
    }
}
