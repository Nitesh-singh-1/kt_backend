using System;
using System.Threading.Tasks;

namespace KTransport.API.Services
{
    /// <summary>
    /// A plan-limited resource whose creation is gated by the tenant's subscription.
    /// </summary>
    public enum UsageResource
    {
        Users,
        Vehicles,
        MonthlyShipments
    }

    public sealed record UsageCheckResult(
        bool Allowed,
        int Limit,
        int Current,
        string PlanName,
        UsageResource Resource)
    {
        /// <summary>Unlimited / not-enforced sentinel (no plan assigned, or a non-positive limit).</summary>
        public bool Unlimited => Limit <= 0;
    }

    /// <summary>
    /// Enforces subscription plan limits (seats, vehicles, monthly shipments).
    /// Shared by the <c>[EnforceUsageLimit]</c> attribute and the usage report so
    /// "what counts against the plan" has exactly one definition.
    ///
    /// Policy (deliberately non-breaking for existing tenants):
    ///  - A tenant with NO subscription plan assigned is treated as UNLIMITED
    ///    (grandfathered) — enforcement only begins once the platform assigns a
    ///    plan with a positive limit.
    ///  - A plan limit of 0 (or negative) means "unlimited" for that resource.
    /// </summary>
    public interface IUsageLimitService
    {
        Task<UsageCheckResult> CheckAsync(Guid tenantId, UsageResource resource);
    }
}
