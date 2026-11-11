using System;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.Models;
using KTransport.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KTransport.API.Tests.Services;

/// <summary>
/// Covers the automatic shipment status-history logging added to
/// KTransportDbContext.TrackShipmentStatusChanges(). Every Shipment.Status
/// transition must append a ShipmentStatusHistory row — this is the data
/// source the public tracking timeline (GET /api/tracking/{lr}) renders,
/// which was previously empty because no code wrote these rows.
/// </summary>
public class ShipmentStatusHistoryTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private sealed class StubTenant : ITenantContext
    {
        private Guid _id;
        public StubTenant(Guid id) => _id = id;
        public Guid CurrentTenantId => _id;
        public bool HasTenant => _id != Guid.Empty;
        public void SetTenantId(Guid t) => _id = t;
    }

    private static KTransportDbContext NewCtx() =>
        new(new DbContextOptionsBuilder<KTransportDbContext>()
                .UseInMemoryDatabase($"track-{Guid.NewGuid()}")
                .Options,
            new StubTenant(Tenant));

    private static Shipment NewBookedShipment(KTransportDbContext ctx)
    {
        var s = new Shipment
        {
            TenantId = Tenant,
            ShipmentNo = "GR-1001",
            FromLocation = "Patna",
            ToLocation = "Delhi",
            TruckNo = "MH12AB1234",
            Status = ShipmentStatus.Booked
        };
        ctx.Shipments.Add(s);
        ctx.SaveChanges(); // Added, not a transition -> no history row
        return s;
    }

    [Fact]
    public async Task StatusTransition_WritesHistoryRow_WithHubLocationAndRemark()
    {
        var ctx = NewCtx();
        var s = NewBookedShipment(ctx);

        s.Status = ShipmentStatus.Manifested;
        await ctx.SaveChangesAsync();

        var rows = await ctx.ShipmentStatusHistories.ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal(ShipmentStatus.Booked, row.FromStatus);
        Assert.Equal(ShipmentStatus.Manifested, row.ToStatus);
        Assert.Equal("Patna", row.Location);                 // origin hub, no GPS
        Assert.Contains("MH12AB1234", row.Remarks);           // vehicle on the remark
        Assert.Equal(Tenant, row.TenantId);                   // stamped by ApplyTenantId
        Assert.Equal(s.Id, row.ShipmentId);
    }

    [Fact]
    public async Task FullJourney_ProducesOneRowPerTransition_InOrder()
    {
        var ctx = NewCtx();
        var s = NewBookedShipment(ctx);

        foreach (var next in new[]
        {
            ShipmentStatus.Manifested, ShipmentStatus.InTransit,
            ShipmentStatus.OutForDelivery, ShipmentStatus.Delivered
        })
        {
            s.Status = next;
            await ctx.SaveChangesAsync();
        }

        var rows = await ctx.ShipmentStatusHistories.OrderBy(h => h.ChangedAt).ThenBy(h => h.Id).ToListAsync();
        Assert.Equal(4, rows.Count);
        Assert.Equal(
            new[] { ShipmentStatus.Manifested, ShipmentStatus.InTransit, ShipmentStatus.OutForDelivery, ShipmentStatus.Delivered },
            rows.Select(r => r.ToStatus).ToArray());
        // Delivery milestone resolves to the destination hub.
        Assert.Equal("Delhi", rows.Last().Location);
    }

    [Fact]
    public async Task SavingWithoutStatusChange_WritesNoHistoryRow()
    {
        var ctx = NewCtx();
        var s = NewBookedShipment(ctx);

        s.ToLocation = "New Delhi"; // unrelated edit, status unchanged
        await ctx.SaveChangesAsync();

        Assert.Equal(0, await ctx.ShipmentStatusHistories.CountAsync());
    }
}
