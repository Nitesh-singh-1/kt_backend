using System;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.Models;
using KTransport.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KTransport.API.Tests.Services;

/// <summary>
/// TASK — usage/plan limit enforcement. Confirms the non-breaking policy:
/// no plan ⇒ unlimited (grandfathered); a positive plan limit is enforced
/// exactly at the ceiling.
/// </summary>
public class UsageLimitServiceTests
{
    private static readonly Guid Tenant = TenantConstants.DefaultTenantId;

    private sealed class StubTenantContext : ITenantContext
    {
        public Guid CurrentTenantId => Tenant;
        public bool HasTenant => true;
        public void SetTenantId(Guid tenantId) { }
    }

    private static KTransportDbContext NewContext()
    {
        var opts = new DbContextOptionsBuilder<KTransportDbContext>()
            .UseInMemoryDatabase($"Usage_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new KTransportDbContext(opts, new StubTenantContext());
    }

    private static void SeedPlan(KTransportDbContext db, int maxUsers)
    {
        var plan = new SubscriptionPlan { Name = "Starter", Tier = "Starter", MaxUsers = maxUsers, MaxVehicles = 10, MaxMonthlyShipments = 200 };
        db.Set<SubscriptionPlan>().Add(plan);
        db.TenantSubscriptions.Add(new TenantSubscription { TenantId = Tenant, SubscriptionPlanId = plan.Id, SubscriptionPlan = plan, Status = "Active" });
    }

    private static void SeedUsers(KTransportDbContext db, int n)
    {
        for (int i = 0; i < n; i++)
            db.Users.Add(new User { TenantId = Tenant, Username = $"u{i}_{Guid.NewGuid():N}", FullName = "U", Password = "x", Role = "user", IsActive = true });
    }

    [Fact]
    public async Task No_plan_is_unlimited()
    {
        await using var db = NewContext();
        SeedUsers(db, 50);
        await db.SaveChangesAsync();

        var result = await new UsageLimitService(db).CheckAsync(Tenant, UsageResource.Users);

        Assert.True(result.Allowed);
        Assert.True(result.Unlimited);
    }

    [Fact]
    public async Task At_the_ceiling_is_blocked()
    {
        await using var db = NewContext();
        SeedPlan(db, maxUsers: 3);
        SeedUsers(db, 3);
        await db.SaveChangesAsync();

        var result = await new UsageLimitService(db).CheckAsync(Tenant, UsageResource.Users);

        Assert.False(result.Allowed);
        Assert.Equal(3, result.Limit);
        Assert.Equal(3, result.Current);
    }

    [Fact]
    public async Task Below_the_ceiling_is_allowed()
    {
        await using var db = NewContext();
        SeedPlan(db, maxUsers: 5);
        SeedUsers(db, 3);
        await db.SaveChangesAsync();

        var result = await new UsageLimitService(db).CheckAsync(Tenant, UsageResource.Users);

        Assert.True(result.Allowed);
        Assert.Equal(5, result.Limit);
        Assert.Equal(3, result.Current);
    }
}
