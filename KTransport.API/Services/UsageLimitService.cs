using System;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Data;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    /// <inheritdoc cref="IUsageLimitService"/>
    public class UsageLimitService : IUsageLimitService
    {
        private readonly KTransportDbContext _db;

        public UsageLimitService(KTransportDbContext db)
        {
            _db = db;
        }

        public async Task<UsageCheckResult> CheckAsync(Guid tenantId, UsageResource resource)
        {
            // IgnoreQueryFilters + explicit tenant scope: this runs inside the
            // authorization pipeline where the ambient tenant is already the
            // caller's, but we pin tenantId explicitly as belt-and-suspenders.
            var subscription = await _db.TenantSubscriptions
                .IgnoreQueryFilters()
                .Include(s => s.SubscriptionPlan)
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            var plan = subscription?.SubscriptionPlan;

            // No plan assigned → grandfathered / unlimited. Do not enforce.
            if (plan == null)
                return new UsageCheckResult(true, 0, 0, "Unplanned", resource);

            int limit = resource switch
            {
                UsageResource.Users => plan.MaxUsers,
                UsageResource.Vehicles => plan.MaxVehicles,
                UsageResource.MonthlyShipments => plan.MaxMonthlyShipments,
                _ => 0
            };

            // Non-positive limit = unlimited for that resource.
            if (limit <= 0)
                return new UsageCheckResult(true, limit, 0, plan.Name, resource);

            int current = resource switch
            {
                UsageResource.Users =>
                    await _db.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId == tenantId),
                UsageResource.Vehicles =>
                    await _db.Vehicles.IgnoreQueryFilters().CountAsync(v => v.TenantId == tenantId),
                UsageResource.MonthlyShipments =>
                    await _db.Shipments.IgnoreQueryFilters().CountAsync(s =>
                        s.TenantId == tenantId && s.CreatedAt >= StartOfCurrentMonth()),
                _ => 0
            };

            return new UsageCheckResult(current < limit, limit, current, plan.Name, resource);
        }

        private static DateTime StartOfCurrentMonth()
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        }
    }
}
