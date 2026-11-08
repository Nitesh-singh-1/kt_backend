using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using KTransport.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KTransport.API.Tests.Services;

/// <summary>
/// TASK-038 — covers the "tests_required" bullets in
/// <c>.agent/contracts/TASK-038-challan-billbook-fixes.yaml</c>. Uses the EF
/// InMemory provider the way <see cref="TripSettlementServiceTests"/> does.
/// Focus: the <c>ComputeShipmentRate</c> helper (via reflection, since it is
/// private), the three rewired rate-call sites, and the new per-item
/// projections surfaced on <c>GetInvoiceById</c> / <c>GetUnbilledByParty</c>.
/// </summary>
public class InvoiceServiceTests
{
    private static readonly Guid TestTenantId = TenantConstants.DefaultTenantId;

    private sealed class StubTenantContext : ITenantContext
    {
        public Guid CurrentTenantId { get; private set; } = TestTenantId;
        public bool HasTenant => true;
        public void SetTenantId(Guid tenantId) => CurrentTenantId = tenantId;
    }

    private sealed class StubNumberingSequenceService : INumberingSequenceService
    {
        private int _counter;
        public Task<string> GetNextNumberAsync(string entityType, string? customPrefix = null)
        {
            var prefix = customPrefix ?? entityType;
            return Task.FromResult($"{prefix}-{++_counter:D4}");
        }
    }

    private static KTransportDbContext NewContext()
    {
        var opts = new DbContextOptionsBuilder<KTransportDbContext>()
            .UseInMemoryDatabase(databaseName: $"InvoiceServiceTests_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new KTransportDbContext(opts, new StubTenantContext());
    }

    private static InvoiceService NewService(KTransportDbContext ctx) =>
        new InvoiceService(ctx, new StubNumberingSequenceService(), NullLogger<InvoiceService>.Instance);

    /// <summary>
    /// Reflective access to <c>InvoiceService.ComputeShipmentRate</c>. The helper is
    /// deliberately private — tests invoke it this way to verify the formula directly
    /// without routing through the service's write paths.
    /// </summary>
    private static decimal InvokeComputeShipmentRate(Shipment s)
    {
        var mi = typeof(InvoiceService).GetMethod(
            "ComputeShipmentRate",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(mi);
        return (decimal)mi!.Invoke(null, new object[] { s })!;
    }

    private static Shipment SeedShipment(
        KTransportDbContext ctx,
        string shipmentNo = "GR-001",
        DateOnly? shipmentDate = null,
        decimal totalFreight = 0m,
        decimal grandTotal = 0m,
        List<ShipmentItem>? items = null,
        List<ShipmentChargeItem>? charges = null,
        string toLocation = "MUMBAI",
        DateOnly? deliveryDate = null,
        PaymentTerm paymentTerm = PaymentTerm.TBB)
    {
        var shipment = new Shipment
        {
            TenantId = TestTenantId,
            ShipmentNo = shipmentNo,
            ShipmentDate = shipmentDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            FromLocation = "DELHI",
            ToLocation = toLocation,
            ConsignorName = "Acme Traders",
            ConsignorPartyId = 100,
            ConsigneeName = "Beta Corp",
            TotalFreight = totalFreight,
            GrandTotal = grandTotal,
            Status = ShipmentStatus.InTransit,
            IsActive = true,
            PaymentTerm = paymentTerm,
            DeliveryDate = deliveryDate,
            CreatedAt = DateTime.UtcNow,
            Items = items ?? new List<ShipmentItem>(),
            ChargeItems = charges ?? new List<ShipmentChargeItem>()
        };
        ctx.Shipments.Add(shipment);
        ctx.SaveChanges();
        return shipment;
    }

    // ---------- 1. ComputeShipmentRate — weighted average with items ----------
    [Fact]
    public void ComputeShipmentRate_WithItems_WeightedAverage()
    {
        // 2 items: (Rate=50, Qty=3) + (Rate=100, Qty=2) = 350 / 5 = 70
        var s = new Shipment
        {
            TenantId = TestTenantId,
            ShipmentNo = "GR-WA",
            TotalFreight = 350m,
            Items = new List<ShipmentItem>
            {
                new() { TenantId = TestTenantId, Rate = 50m, Quantity = 3, Weight = 10m },
                new() { TenantId = TestTenantId, Rate = 100m, Quantity = 2, Weight = 20m },
            }
        };

        var rate = InvokeComputeShipmentRate(s);
        Assert.Equal(70m, rate);
    }

    // ---------- 2. ComputeShipmentRate — fallback to freight/pkg when no items ----------
    [Fact]
    public void ComputeShipmentRate_NoItems_FallbackToFreightDivPkg()
    {
        // Items collection empty, TotalFreight=600, pkgCount defaults to 1 → 600.
        var s = new Shipment
        {
            TenantId = TestTenantId,
            ShipmentNo = "GR-NI",
            TotalFreight = 600m,
            GrandTotal = 800m,
            Items = new List<ShipmentItem>()
        };

        var rate = InvokeComputeShipmentRate(s);
        Assert.Equal(600m, rate);
    }

    // ---------- 3. GenerateBillBook stores Rate from helper, not amount/weight ----------
    [Fact]
    public async Task GenerateBillBook_StoresRateFromHelper_NotWeightDivision()
    {
        await using var ctx = NewContext();
        // Items with known weighted-average rate = (50*3 + 100*2) / 5 = 70.
        // weight=100 kg so biltyAmt/weight would be ~ 3.5 — a very different number.
        var items = new List<ShipmentItem>
        {
            new() { TenantId = TestTenantId, Rate = 50m, Quantity = 3, Weight = 60m },
            new() { TenantId = TestTenantId, Rate = 100m, Quantity = 2, Weight = 40m },
        };
        var shipment = SeedShipment(ctx, "GR-RATE-01", totalFreight: 350m, grandTotal: 350m, items: items);
        var service = NewService(ctx);

        var resp = await service.CreateBillBookInvoiceAsync(new CreateBillBookRequestDto
        {
            PartyName = "Acme Traders",
            PartyId = 100,
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ShipmentIds = new List<long> { shipment.Id }
        });

        Assert.True(resp.Success);
        Assert.NotNull(resp.Data);
        var invoiceItem = Assert.Single(resp.Data!.Items);
        Assert.Equal(70m, invoiceItem.Rate);
        // Sanity check: a buggy biltyAmt/totalWeight = 350/100 = 3.5 would be totally
        // different. The helper must win.
        Assert.NotEqual(3.5m, invoiceItem.Rate);
    }

    // ---------- 4. GenerateBillBook: Amount equals GrandTotal (>0) else TotalFreight ----------
    [Fact]
    public async Task GenerateBillBook_InvoiceItemAmountMatchesGrandTotal()
    {
        await using var ctx = NewContext();
        var items = new List<ShipmentItem>
        {
            new() { TenantId = TestTenantId, Rate = 50m, Quantity = 2, Weight = 10m },
        };
        // GrandTotal > 0 → Amount must equal GrandTotal, not TotalFreight.
        var shipment = SeedShipment(ctx, "GR-AMT-01", totalFreight: 100m, grandTotal: 150m, items: items);
        var service = NewService(ctx);

        var resp = await service.CreateBillBookInvoiceAsync(new CreateBillBookRequestDto
        {
            PartyName = "Acme Traders",
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ShipmentIds = new List<long> { shipment.Id }
        });

        Assert.True(resp.Success);
        var invoiceItem = Assert.Single(resp.Data!.Items);
        Assert.Equal(150m, invoiceItem.Amount);
    }

    // ---------- 5. GetInvoiceById returns the ORIGINAL shipment date ----------
    [Fact]
    public async Task GetInvoiceById_ReturnsOriginalShipmentDate()
    {
        await using var ctx = NewContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fiveDaysAgo = today.AddDays(-5);

        var items = new List<ShipmentItem>
        {
            new() { TenantId = TestTenantId, Rate = 40m, Quantity = 2, Weight = 20m },
        };
        var shipment = SeedShipment(ctx, "GR-DT-01",
            shipmentDate: fiveDaysAgo,
            totalFreight: 80m, grandTotal: 80m, items: items);

        var service = NewService(ctx);
        var create = await service.CreateBillBookInvoiceAsync(new CreateBillBookRequestDto
        {
            PartyName = "Acme Traders",
            InvoiceDate = today, // DIFFERENT from the bilty's shipmentDate.
            ShipmentIds = new List<long> { shipment.Id }
        });
        Assert.True(create.Success);

        var fetched = await service.GetInvoiceByIdAsync(create.Data!.Id);
        Assert.NotNull(fetched);
        var itemDto = Assert.Single(fetched!.Items);
        Assert.Equal(fiveDaysAgo, itemDto.ShipmentDate);
        Assert.NotEqual(fetched.InvoiceDate, itemDto.ShipmentDate);
    }

    // ---------- 6. GetInvoiceById surfaces each ShipmentChargeItem ----------
    [Fact]
    public async Task GetInvoiceById_ReturnsChargeItems()
    {
        await using var ctx = NewContext();
        var items = new List<ShipmentItem>
        {
            new() { TenantId = TestTenantId, Rate = 60m, Quantity = 1, Weight = 10m },
        };
        var charges = new List<ShipmentChargeItem>
        {
            new() { TenantId = TestTenantId, ChargeName = "Hamali", Amount = 100m },
            new() { TenantId = TestTenantId, ChargeName = "Statistical", Amount = 25m },
        };
        var shipment = SeedShipment(ctx, "GR-CHG-01",
            totalFreight: 60m, grandTotal: 185m, items: items, charges: charges);

        var service = NewService(ctx);
        var create = await service.CreateBillBookInvoiceAsync(new CreateBillBookRequestDto
        {
            PartyName = "Acme Traders",
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ShipmentIds = new List<long> { shipment.Id }
        });
        Assert.True(create.Success);

        var fetched = await service.GetInvoiceByIdAsync(create.Data!.Id);
        Assert.NotNull(fetched);
        var itemDto = Assert.Single(fetched!.Items);
        Assert.NotNull(itemDto.ChargeItems);
        Assert.Equal(2, itemDto.ChargeItems!.Count);
        var hamali = itemDto.ChargeItems.Single(c => c.ChargeName == "Hamali");
        Assert.Equal(100m, hamali.Amount);
        var statistical = itemDto.ChargeItems.Single(c => c.ChargeName == "Statistical");
        Assert.Equal(25m, statistical.Amount);
    }

    // ---------- 7. GetUnbilledByParty returns ChargeItems per bilty ----------
    [Fact]
    public async Task GetUnbilledByParty_ReturnsChargeItems()
    {
        await using var ctx = NewContext();
        var items = new List<ShipmentItem>
        {
            new() { TenantId = TestTenantId, Rate = 50m, Quantity = 2, Weight = 15m },
        };
        var charges = new List<ShipmentChargeItem>
        {
            new() { TenantId = TestTenantId, ChargeName = "Hamali", Amount = 75m },
            new() { TenantId = TestTenantId, ChargeName = "DoorDelivery", Amount = 50m },
        };
        SeedShipment(ctx, "GR-UNB-01",
            totalFreight: 100m, grandTotal: 225m, items: items, charges: charges);

        var service = NewService(ctx);
        var unbilled = await service.GetUnbilledShipmentsByPartyAsync(partyName: "Acme Traders");

        var row = Assert.Single(unbilled);
        Assert.NotNull(row.ChargeItems);
        Assert.Equal(2, row.ChargeItems!.Count);
        Assert.Contains(row.ChargeItems, c => c.ChargeName == "Hamali" && c.Amount == 75m);
        Assert.Contains(row.ChargeItems, c => c.ChargeName == "DoorDelivery" && c.Amount == 50m);
    }

    // ---------- 8. Bill Book loads ONLY "To Be Billed" (TBB) consignments ----------
    [Fact]
    public async Task GetUnbilled_ReturnsOnlyTbbBilties()
    {
        await using var ctx = NewContext();
        SeedShipment(ctx, "GR-TBB-01", totalFreight: 100m, grandTotal: 100m, paymentTerm: PaymentTerm.TBB);
        SeedShipment(ctx, "GR-PAID-01", totalFreight: 200m, grandTotal: 200m, paymentTerm: PaymentTerm.Paid);
        SeedShipment(ctx, "GR-TOPAY-01", totalFreight: 300m, grandTotal: 300m, paymentTerm: PaymentTerm.ToPay);

        var service = NewService(ctx);

        // Per-party list: only the TBB bilty, not Paid / ToPay.
        var byParty = await service.GetUnbilledShipmentsByPartyAsync(partyName: "Acme Traders");
        var tbbRow = Assert.Single(byParty);
        Assert.Equal("GR-TBB-01", tbbRow.ShipmentNo);

        // Parties summary count must agree with the list (exactly 1).
        var summary = await service.GetUnbilledPartiesSummaryAsync();
        var acme = Assert.Single(summary);
        Assert.Equal(1, acme.UnbilledCount);
    }

    // ---------- 9. UpdateBillShipments: removing a bilty returns it to the pool ----------
    [Fact]
    public async Task UpdateBillShipments_RemovesBilty_ReturnsItToUnbilledPool()
    {
        await using var ctx = NewContext();
        var a = SeedShipment(ctx, "GR-A", totalFreight: 100m, grandTotal: 100m);
        var b = SeedShipment(ctx, "GR-B", totalFreight: 200m, grandTotal: 200m);
        var service = NewService(ctx);

        var create = await service.CreateBillBookInvoiceAsync(new CreateBillBookRequestDto
        {
            PartyName = "Acme Traders",
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ShipmentIds = new List<long> { a.Id, b.Id }
        });
        Assert.True(create.Success);
        var invoiceId = create.Data!.Id;

        // Keep only B.
        var update = await service.UpdateBillShipmentsAsync(invoiceId, new UpdateBillShipmentsRequest
        {
            ShipmentIds = new List<long> { b.Id }
        });

        Assert.True(update.Success);
        var item = Assert.Single(update.Data!.Items);
        Assert.Equal("GR-B", item.ShipmentNo);
        Assert.Equal(200m, update.Data!.GrandTotal);

        // A is back in the unbilled pool.
        var unbilled = await service.GetUnbilledShipmentsByPartyAsync(partyName: "Acme Traders");
        Assert.Contains(unbilled, s => s.ShipmentNo == "GR-A");
        Assert.DoesNotContain(unbilled, s => s.ShipmentNo == "GR-B");
    }

    // ---------- 10. UpdateBillShipments: adding an eligible bilty grows the bill ----------
    [Fact]
    public async Task UpdateBillShipments_AddsBilty_GrowsTotal()
    {
        await using var ctx = NewContext();
        var a = SeedShipment(ctx, "GR-A", totalFreight: 100m, grandTotal: 100m);
        var b = SeedShipment(ctx, "GR-B", totalFreight: 250m, grandTotal: 250m);
        var service = NewService(ctx);

        var create = await service.CreateBillBookInvoiceAsync(new CreateBillBookRequestDto
        {
            PartyName = "Acme Traders",
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ShipmentIds = new List<long> { a.Id }
        });
        Assert.True(create.Success);

        var update = await service.UpdateBillShipmentsAsync(create.Data!.Id, new UpdateBillShipmentsRequest
        {
            ShipmentIds = new List<long> { a.Id, b.Id }
        });

        Assert.True(update.Success);
        Assert.Equal(2, update.Data!.Items.Count);
        Assert.Equal(350m, update.Data!.GrandTotal);
    }

    // ---------- 11. UpdateBillShipments: cannot drop total below amount already received ----------
    [Fact]
    public async Task UpdateBillShipments_BlocksDropBelowPaidAmount()
    {
        await using var ctx = NewContext();
        var a = SeedShipment(ctx, "GR-A", totalFreight: 100m, grandTotal: 100m);
        var b = SeedShipment(ctx, "GR-B", totalFreight: 200m, grandTotal: 200m);
        var service = NewService(ctx);

        var create = await service.CreateBillBookInvoiceAsync(new CreateBillBookRequestDto
        {
            PartyName = "Acme Traders",
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ShipmentIds = new List<long> { a.Id, b.Id } // total 300
        });
        Assert.True(create.Success);
        var invoiceId = create.Data!.Id;

        // Receive 150 (partial).
        await service.RecordPaymentAsync(invoiceId, new RecordPaymentRequest { PaymentAmount = 150m });

        // Keeping only A (100) would drop the bill below the 150 already received -> blocked.
        var update = await service.UpdateBillShipmentsAsync(invoiceId, new UpdateBillShipmentsRequest
        {
            ShipmentIds = new List<long> { a.Id }
        });

        Assert.False(update.Success);

        // Composition unchanged: both bilties still on the bill.
        var fetched = await service.GetInvoiceByIdAsync(invoiceId);
        Assert.Equal(2, fetched!.Items.Count);
        Assert.Equal(300m, fetched.GrandTotal);
    }

    // ---------- 12. UpdateBillShipments: cannot add a different client's bilty ----------
    [Fact]
    public async Task UpdateBillShipments_BlocksCrossClientBilty()
    {
        await using var ctx = NewContext();
        var a = SeedShipment(ctx, "GR-A", totalFreight: 100m, grandTotal: 100m);
        var other = SeedShipment(ctx, "GR-OTHER", totalFreight: 500m, grandTotal: 500m);
        other.ConsignorName = "Other Co"; // belongs to a different client
        ctx.SaveChanges();
        var service = NewService(ctx);

        var create = await service.CreateBillBookInvoiceAsync(new CreateBillBookRequestDto
        {
            PartyName = "Acme Traders",
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ShipmentIds = new List<long> { a.Id }
        });
        Assert.True(create.Success);

        var update = await service.UpdateBillShipmentsAsync(create.Data!.Id, new UpdateBillShipmentsRequest
        {
            ShipmentIds = new List<long> { a.Id, other.Id }
        });

        Assert.False(update.Success);
    }
}
