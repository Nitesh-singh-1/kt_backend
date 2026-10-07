using System;
using System.Collections.Generic;
using System.Linq;
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
/// TASK-037 — covers the "tests.required" bullets in
/// <c>.agent/contracts/TASK-037-trip-settlement.yaml</c>. Runs against the EF
/// InMemory provider: unique indexes and FKs behave as hints only, so the
/// uniqueness regression test asserts in-process behaviour (two settlements
/// against the same tenant produce distinct SettlementNos). The DB-level
/// enforcement is covered by the migration's PostgreSQL unique index.
/// </summary>
public class TripSettlementServiceTests
{
    private static readonly Guid TestTenantId = TenantConstants.DefaultTenantId;

    /// <summary>
    /// EF's parameter extractor evaluates captured member accesses eagerly, so
    /// a null <see cref="ITenantContext"/> blows up the query filter even
    /// though the predicate short-circuits in C#. Hand it a stub.
    /// </summary>
    private sealed class StubTenantContext : ITenantContext
    {
        public Guid CurrentTenantId { get; private set; } = TestTenantId;
        public bool HasTenant => true;
        public void SetTenantId(Guid tenantId) => CurrentTenantId = tenantId;
    }

    private static KTransportDbContext NewContext()
    {
        var opts = new DbContextOptionsBuilder<KTransportDbContext>()
            .UseInMemoryDatabase(databaseName: $"TripSettlementTests_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new KTransportDbContext(opts, new StubTenantContext());
    }

    private static Trip SeedTrip(KTransportDbContext ctx, string tripNo = "TRIP-001", decimal advCash = 5000m, decimal advFuel = 3000m)
    {
        var trip = new Trip
        {
            TenantId = TestTenantId,
            TripNo = tripNo,
            TripDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = TripStatus.InTransit,
            StartOdometer = 10000m,
            EndOdometer = 10500m,
            DriverAdvanceCash = advCash,
            DriverAdvanceFuel = advFuel,
            Remarks = "Original remark",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        ctx.Trips.Add(trip);
        ctx.TripExpenses.Add(new TripExpense
        {
            TenantId = TestTenantId,
            Trip = trip,
            ExpenseType = TripExpenseType.Fuel,
            Amount = 2000m,
            ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTime.UtcNow,
        });
        ctx.SaveChanges();
        return trip;
    }

    private static TripService NewService(KTransportDbContext ctx) =>
        new TripService(ctx, numberingService: null!, logger: NullLogger<TripService>.Instance);

    private static SettleTripRequestDto SettleRequest(long tripId, decimal collected = 1000m, decimal settled = 500m, string? remarks = "test settle")
        => new SettleTripRequestDto
        {
            TripId = tripId,
            EndOdometer = 10600m,
            CollectedToPayFreight = collected,
            SettledAmount = settled,
            PaymentMode = "CASH",
            SettlementRemarks = remarks,
            SettlementDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };

    [Fact]
    public async Task SettleTrip_CreatesExactlyOneNewSettlementRow()
    {
        await using var ctx = NewContext();
        var trip = SeedTrip(ctx);
        var service = NewService(ctx);

        var resp = await service.SettleTripAsync(SettleRequest(trip.Id), userId: 42);

        Assert.True(resp.Success);
        var rows = await ctx.TripSettlements.Where(s => s.TripId == trip.Id).ToListAsync();
        Assert.Single(rows);
        Assert.False(rows[0].IsReversed);
        Assert.Equal(42, rows[0].SettledBy);
        // Snapshot values must be exactly what we fed in, frozen at write time.
        Assert.Equal(5000m, rows[0].DriverAdvanceCashSnapshot);
        Assert.Equal(3000m, rows[0].DriverAdvanceFuelSnapshot);
        Assert.Equal(1000m, rows[0].CollectedToPayFreight);
        Assert.Equal(9000m, rows[0].TotalDriverAccountability); // 5000 + 3000 + 1000
        Assert.Equal(2000m, rows[0].TotalExpensesSnapshot);
        Assert.Equal(7000m, rows[0].NetDriverBalance); // 9000 - 2000
    }

    [Fact]
    public async Task SettleTrip_TransitionsStatusToCompleted()
    {
        await using var ctx = NewContext();
        var trip = SeedTrip(ctx);
        var service = NewService(ctx);

        var resp = await service.SettleTripAsync(SettleRequest(trip.Id));

        Assert.True(resp.Success);
        var fresh = await ctx.Trips.FindAsync(trip.Id);
        Assert.Equal(TripStatus.Completed, fresh!.Status);
        Assert.NotNull(fresh.ArrivalTime);
        Assert.Equal(10600m, fresh.EndOdometer);
    }

    [Fact]
    public async Task SettleTrip_DoesNotModifyTripRemarks()
    {
        await using var ctx = NewContext();
        var trip = SeedTrip(ctx);
        var service = NewService(ctx);

        await service.SettleTripAsync(SettleRequest(trip.Id, remarks: "Driver returned extra cash"));

        var fresh = await ctx.Trips.FindAsync(trip.Id);
        // Regression: old code appended "[Settled on …]" to Remarks; new code must NOT.
        Assert.Equal("Original remark", fresh!.Remarks);
        Assert.DoesNotContain("[Settled on", fresh.Remarks ?? string.Empty);
    }

    [Fact]
    public async Task SettleTrip_GeneratesDistinctSettlementNosPerTenant()
    {
        await using var ctx = NewContext();
        var trip1 = SeedTrip(ctx, tripNo: "TRIP-A");
        var trip2 = SeedTrip(ctx, tripNo: "TRIP-B");
        var service = NewService(ctx);

        var r1 = await service.SettleTripAsync(SettleRequest(trip1.Id));
        var r2 = await service.SettleTripAsync(SettleRequest(trip2.Id));

        Assert.True(r1.Success);
        Assert.True(r2.Success);
        var all = await ctx.TripSettlements.OrderBy(s => s.CreatedAt).ToListAsync();
        Assert.Equal(2, all.Count);
        Assert.NotEqual(all[0].SettlementNo, all[1].SettlementNo);
        // Format: TRIP-SET-YYYY-NNNN
        var year = DateTime.UtcNow.Year;
        Assert.StartsWith($"TRIP-SET-{year}-", all[0].SettlementNo);
        Assert.StartsWith($"TRIP-SET-{year}-", all[1].SettlementNo);
    }

    [Fact]
    public async Task GetTripSettlementSummary_PopulatesSettledAtFromLatestNonReversedRow()
    {
        await using var ctx = NewContext();
        var trip = SeedTrip(ctx);
        var service = NewService(ctx);

        await service.SettleTripAsync(SettleRequest(trip.Id, remarks: "First settle"));

        var summary = await service.GetTripSettlementSummaryAsync(trip.Id);
        Assert.NotNull(summary);
        Assert.NotNull(summary!.SettledAt);
        Assert.Equal("First settle", summary.SettlementRemarks);
    }

    [Fact]
    public async Task SettleTrip_CalledTwice_AppendsSecondSettlementRow()
    {
        await using var ctx = NewContext();
        var trip = SeedTrip(ctx);
        var service = NewService(ctx);

        await service.SettleTripAsync(SettleRequest(trip.Id, remarks: "first"));
        await service.SettleTripAsync(SettleRequest(trip.Id, remarks: "second"));

        var history = await service.GetTripSettlementsAsync(trip.Id);
        Assert.Equal(2, history.Count);
        Assert.Equal("first", history[0].SettlementRemarks);
        Assert.Equal("second", history[1].SettlementRemarks);
        // History must be oldest-first.
        Assert.True(history[0].CreatedAt <= history[1].CreatedAt);

        // Summary reflects the most recent remarks.
        var summary = await service.GetTripSettlementSummaryAsync(trip.Id);
        Assert.Equal("second", summary!.SettlementRemarks);
    }
}
