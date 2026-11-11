using System;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.DTOs.Gst;
using KTransport.API.Models.Gst;
using KTransport.API.Services;
using KTransport.API.Services.Gst;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KTransport.API.Tests.Services.Gst;

/// <summary>
/// Unit tests for the GST decision engine (TASK-032/033), standing in for the
/// live-server <c>scripts/gst/smoke-G.sh</c> script with deterministic,
/// CI-runnable xUnit tests. Uses the EF InMemory provider (no DB, no HTTP),
/// exercising the REAL <see cref="TaxCalculationService"/> + real
/// <see cref="GstRuleRepository"/> against seeded <see cref="GstRule"/> rows.
///
/// Covers GST-IMPLEMENTATION.md §G scenarios 1-15 plus the negative /
/// invariant / audit cases the smoke script never asserted. Rates asserted
/// here are the LOCAL-TEST-SEED values (5% / 12%) — update alongside gap
/// G1 (12% -> 18%) when the production seed lands.
/// </summary>
public class TaxCalculationServiceTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // ---- test doubles -------------------------------------------------

    private sealed class StubTenantContext : ITenantContext
    {
        private Guid _id;
        public StubTenantContext(Guid id) => _id = id;
        public Guid CurrentTenantId => _id;
        public bool HasTenant => _id != Guid.Empty;
        public void SetTenantId(Guid tenantId) => _id = tenantId;
    }

    private static KTransportDbContext NewContext(ITenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<KTransportDbContext>()
            .UseInMemoryDatabase($"gst-tests-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;
        return new KTransportDbContext(options, tenant);
    }

    private static (TaxCalculationService svc, KTransportDbContext ctx) NewEngine(ITenantContext? tenant = null)
    {
        tenant ??= new StubTenantContext(TenantA);
        var ctx = NewContext(tenant);
        var repo = new GstRuleRepository(ctx);
        return (new TaxCalculationService(repo, ctx, tenant), ctx);
    }

    // ---- seed helpers -------------------------------------------------

    private static GstRule Rule(
        GtaTaxScheme scheme, RecipientCategory rc, PosRelation pos, GstTaxTreatment treat,
        GstServiceType st = GstServiceType.GtaRoadTransport, bool gta = true, bool cn = true, bool reg = true,
        decimal? gst = null, decimal? cgst = null, decimal? sgst = null, decimal? igst = null, decimal? utgst = null,
        bool itc = false, string? rcmBy = null, string? notif = null,
        DateOnly? from = null, DateOnly? to = null)
        => new GstRule
        {
            Id = Guid.NewGuid(),
            ServiceType = st,
            ProviderIsGta = gta,
            IssuesConsignmentNote = cn,
            ProviderGstRegistered = reg,
            GtaTaxScheme = scheme,
            RecipientCategory = rc,
            PosRelation = pos,
            TaxTreatment = treat,
            GstRate = gst,
            CgstRate = cgst,
            SgstRate = sgst,
            IgstRate = igst,
            UtgstRate = utgst,
            ItcEligibleForProvider = itc,
            RcmPayableBy = rcmBy,
            NotificationReference = notif ?? "LOCAL-TEST-SEED",
            EffectiveFrom = from ?? new DateOnly(2024, 1, 1),
            EffectiveTo = to,
            Active = true,
            CreatedAt = DateTime.UtcNow
        };

    /// <summary>Seeds the common §G rate rows used by the happy-path tests.</summary>
    private static void SeedStandardRules(KTransportDbContext ctx)
    {
        ctx.GstRules.AddRange(
            // R1 — GTA FCM5 intra (G.1)
            Rule(GtaTaxScheme.Fcm5NoItc, RecipientCategory.RegisteredBusiness, PosRelation.IntraState,
                 GstTaxTreatment.ForwardCharge, gst: 0.05m, cgst: 0.025m, sgst: 0.025m),
            // R2 — GTA FCM5 inter (G.2)
            Rule(GtaTaxScheme.Fcm5NoItc, RecipientCategory.RegisteredBusiness, PosRelation.InterState,
                 GstTaxTreatment.ForwardCharge, gst: 0.05m, igst: 0.05m),
            // R3 — GTA FCM12 intra (G.3)
            Rule(GtaTaxScheme.Fcm12WithItc, RecipientCategory.RegisteredBusiness, PosRelation.IntraState,
                 GstTaxTreatment.ForwardCharge, gst: 0.12m, cgst: 0.06m, sgst: 0.06m, itc: true),
            // R4 — GTA FCM12 inter (G.4)
            Rule(GtaTaxScheme.Fcm12WithItc, RecipientCategory.RegisteredBusiness, PosRelation.InterState,
                 GstTaxTreatment.ForwardCharge, gst: 0.12m, igst: 0.12m, itc: true),
            // R5 — GTA RCM, specified recipient (G.5)
            Rule(GtaTaxScheme.Rcm, RecipientCategory.SpecifiedPersonRcm, PosRelation.IntraState,
                 GstTaxTreatment.ReverseCharge, rcmBy: "RECIPIENT"),
            // R6 — non-GTA road EXEMPT (G.6)
            Rule(GtaTaxScheme.NotApplicable, RecipientCategory.UnregisteredBusiness, PosRelation.IntraState,
                 GstTaxTreatment.Exempt, st: GstServiceType.NonGtaRoadTransport, gta: false, cn: false),
            // R7 — GTA FCM5 intra, unregistered customer (G.10)
            Rule(GtaTaxScheme.Fcm5NoItc, RecipientCategory.UnregisteredBusiness, PosRelation.IntraState,
                 GstTaxTreatment.ForwardCharge, gst: 0.05m, cgst: 0.025m, sgst: 0.025m),
            // R8 — GTA FCM5 UT-without-legislature → CGST + UTGST
            Rule(GtaTaxScheme.Fcm5NoItc, RecipientCategory.RegisteredBusiness, PosRelation.UnionTerritory,
                 GstTaxTreatment.ForwardCharge, gst: 0.05m, cgst: 0.025m, utgst: 0.025m)
        );
        ctx.SaveChanges();
    }

    private static TaxCalculationInputDto In(
        GtaTaxScheme scheme = GtaTaxScheme.Fcm5NoItc,
        RecipientCategory rc = RecipientCategory.RegisteredBusiness,
        string providerCode = "10", string recipientCode = "10",
        decimal freight = 10000m,
        GstServiceType st = GstServiceType.GtaRoadTransport, bool gta = true, bool cn = true,
        bool taxInclusive = false, string? posCode = null, bool? utNoLeg = null, DateOnly? txn = null)
        => new TaxCalculationInputDto
        {
            ServiceType = st,
            ProviderIsGta = gta,
            IssuesConsignmentNote = cn,
            ProviderGstRegistered = true,
            ProviderStateCode = providerCode,
            GtaTaxScheme = scheme,
            RecipientCategory = rc,
            RecipientStateCode = recipientCode,
            PlaceOfSupplyStateCode = posCode,
            PlaceOfSupplyIsUtWithoutLegislature = utNoLeg,
            FreightAmount = freight,
            TaxInclusive = taxInclusive,
            TransactionDate = txn ?? new DateOnly(2024, 6, 15)
        };

    // ==================================================================
    //  §G happy-path calculations
    // ==================================================================

    [Fact] // G.1
    public async Task Gta_Fcm5_IntraState_SplitsCgstSgst()
    {
        var (svc, ctx) = NewEngine();
        SeedStandardRules(ctx);

        var r = await svc.CalculateAsync(In(GtaTaxScheme.Fcm5NoItc));

        Assert.Equal(GstTaxTreatment.ForwardCharge, r.TaxTreatment);
        Assert.Equal(PosRelation.IntraState, r.PosRelation);
        Assert.Equal(250.00m, r.CgstAmount);
        Assert.Equal(250.00m, r.SgstAmount);
        Assert.Equal(0m, r.IgstAmount);
        Assert.Equal(500.00m, r.TotalTax);
        Assert.Equal(10500.00m, r.GrandTotal);
        Assert.False(r.IsRcm);
        Assert.False(r.IsItcEligible);
    }

    [Fact] // G.2
    public async Task Gta_Fcm5_InterState_UsesIgst()
    {
        var (svc, ctx) = NewEngine();
        SeedStandardRules(ctx);

        var r = await svc.CalculateAsync(In(GtaTaxScheme.Fcm5NoItc, recipientCode: "27"));

        Assert.Equal(PosRelation.InterState, r.PosRelation);
        Assert.Equal(500.00m, r.IgstAmount);
        Assert.Equal(0m, r.CgstAmount);
        Assert.Equal(0m, r.SgstAmount);
        Assert.Equal(10500.00m, r.GrandTotal);
    }

    [Fact] // G.3
    public async Task Gta_Fcm12_IntraState_WithItc()
    {
        var (svc, ctx) = NewEngine();
        SeedStandardRules(ctx);

        var r = await svc.CalculateAsync(In(GtaTaxScheme.Fcm12WithItc));

        Assert.Equal(600.00m, r.CgstAmount);
        Assert.Equal(600.00m, r.SgstAmount);
        Assert.Equal(11200.00m, r.GrandTotal);
        Assert.True(r.IsItcEligible);
    }

    [Fact] // G.4
    public async Task Gta_Fcm12_InterState_UsesIgst()
    {
        var (svc, ctx) = NewEngine();
        SeedStandardRules(ctx);

        var r = await svc.CalculateAsync(In(GtaTaxScheme.Fcm12WithItc, recipientCode: "27"));

        Assert.Equal(1200.00m, r.IgstAmount);
        Assert.Equal(11200.00m, r.GrandTotal);
        Assert.True(r.IsItcEligible);
    }

    [Fact] // G.5
    public async Task Gta_Rcm_ZeroTaxOnInvoice_IsRcmTrue()
    {
        var (svc, ctx) = NewEngine();
        SeedStandardRules(ctx);

        var r = await svc.CalculateAsync(In(GtaTaxScheme.Rcm, RecipientCategory.SpecifiedPersonRcm));

        Assert.Equal(GstTaxTreatment.ReverseCharge, r.TaxTreatment);
        Assert.True(r.IsRcm);
        Assert.Equal(0m, r.TotalTax);
        Assert.Equal(0m, r.CgstAmount);
        Assert.Equal(0m, r.IgstAmount);
        Assert.Equal(10000m, r.GrandTotal); // recipient pays GST in its own books
    }

    [Fact] // G.6
    public async Task NonGta_RoadTransport_IsExempt()
    {
        var (svc, ctx) = NewEngine();
        SeedStandardRules(ctx);

        var r = await svc.CalculateAsync(In(
            scheme: GtaTaxScheme.NotApplicable, rc: RecipientCategory.UnregisteredBusiness,
            st: GstServiceType.NonGtaRoadTransport, gta: false, cn: false));

        Assert.Equal(GstTaxTreatment.Exempt, r.TaxTreatment);
        Assert.Equal(0m, r.TotalTax);
        Assert.Equal(10000m, r.GrandTotal);
        Assert.False(r.IsRcm);
    }

    [Fact] // G.7 — own fleet short-circuits BEFORE any rule lookup (empty rule table)
    public async Task OwnGoodsMovement_IsOutOfScope_WithoutRuleLookup()
    {
        var (svc, _) = NewEngine(); // deliberately seed NO rules

        var r = await svc.CalculateAsync(In(st: GstServiceType.OwnGoodsMovement, gta: false, cn: false));

        Assert.Equal(GstTaxTreatment.OutOfScope, r.TaxTreatment);
        Assert.Equal(0m, r.TotalTax);
        Assert.Equal(10000m, r.GrandTotal);
    }

    [Fact] // G.10 — unregistered (B2C) customer: tax still applies under FCM
    public async Task Gta_Fcm5_UnregisteredCustomer_StillTaxed()
    {
        var (svc, ctx) = NewEngine();
        SeedStandardRules(ctx);

        var r = await svc.CalculateAsync(In(GtaTaxScheme.Fcm5NoItc, RecipientCategory.UnregisteredBusiness));

        Assert.Equal(250.00m, r.CgstAmount);
        Assert.Equal(250.00m, r.SgstAmount);
        Assert.False(r.IsRcm);
    }

    [Fact] // G.11 — explicit Place-of-Supply override flips intra -> inter
    public async Task PlaceOfSupplyOverride_ForcesInterState()
    {
        var (svc, ctx) = NewEngine();
        SeedStandardRules(ctx);

        // provider & recipient both state 10, but POS override = 27
        var r = await svc.CalculateAsync(In(GtaTaxScheme.Fcm5NoItc, providerCode: "10", recipientCode: "10", posCode: "27"));

        Assert.Equal(PosRelation.InterState, r.PosRelation);
        Assert.Equal(500.00m, r.IgstAmount);
    }

    [Fact] // G.12 — tax-inclusive freight reverses the base out
    public async Task TaxInclusiveFreight_ReversesTaxableBase()
    {
        var (svc, ctx) = NewEngine();
        SeedStandardRules(ctx);

        var r = await svc.CalculateAsync(In(GtaTaxScheme.Fcm5NoItc, freight: 10500m, taxInclusive: true));

        Assert.Equal(10000.00m, r.TaxableAmount);
        Assert.Equal(500.00m, r.TotalTax);
        Assert.Equal(10500.00m, r.GrandTotal);
    }

    [Fact] // UT without legislature → CGST + UTGST pair
    public async Task UnionTerritoryWithoutLegislature_UsesCgstUtgst()
    {
        var (svc, ctx) = NewEngine();
        SeedStandardRules(ctx);

        var r = await svc.CalculateAsync(In(GtaTaxScheme.Fcm5NoItc, utNoLeg: true));

        Assert.Equal(PosRelation.UnionTerritory, r.PosRelation);
        Assert.Equal(250.00m, r.CgstAmount);
        Assert.Equal(250.00m, r.UtgstAmount);
        Assert.Equal(0m, r.SgstAmount);
        Assert.Equal(0m, r.IgstAmount);
        Assert.Equal(10500.00m, r.GrandTotal);
    }

    [Fact] // splits are rounded to 2dp (away-from-zero), then summed
    public async Task TaxSplits_RoundToTwoDecimals()
    {
        var (svc, ctx) = NewEngine();
        SeedStandardRules(ctx);

        var r = await svc.CalculateAsync(In(GtaTaxScheme.Fcm5NoItc, freight: 333.33m));

        Assert.Equal(8.33m, r.CgstAmount);   // 333.33 * 0.025 = 8.333250 -> 8.33
        Assert.Equal(8.33m, r.SgstAmount);
        Assert.Equal(16.66m, r.TotalTax);
        Assert.Equal(349.99m, r.GrandTotal);
    }

    // ==================================================================
    //  Negative / invariant cases (never covered by smoke-G.sh)
    // ==================================================================

    [Fact] // 0 matching rules → explicit exception, never a silent guess
    public async Task NoMatchingRule_ThrowsGstRuleUnresolved()
    {
        var (svc, ctx) = NewEngine();
        SeedStandardRules(ctx); // no Government-recipient row exists (gap G5)

        await Assert.ThrowsAsync<GstRuleUnresolvedException>(
            () => svc.CalculateAsync(In(GtaTaxScheme.Fcm5NoItc, RecipientCategory.Government)));
    }

    [Fact]
    public async Task NegativeFreight_Throws()
    {
        var (svc, ctx) = NewEngine();
        SeedStandardRules(ctx);

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.CalculateAsync(In(GtaTaxScheme.Fcm5NoItc, freight: -1m)));
    }

    [Fact] // RCM result must never carry a tax split (invariant §H)
    public async Task Rcm_HasNoTaxSplit()
    {
        var (svc, ctx) = NewEngine();
        SeedStandardRules(ctx);

        var r = await svc.CalculateAsync(In(GtaTaxScheme.Rcm, RecipientCategory.SpecifiedPersonRcm));

        Assert.True(r.IsRcm);
        Assert.Equal(0m, r.CgstAmount + r.SgstAmount + r.IgstAmount + r.UtgstAmount);
    }

    // ==================================================================
    //  Audit persistence + recalculation / supersession
    // ==================================================================

    [Fact]
    public async Task Apply_PersistsImmutableAuditRow()
    {
        var (svc, ctx) = NewEngine();
        SeedStandardRules(ctx);

        var dto = await svc.ApplyAsync(
            new TaxCalculationApplyRequestDto { Input = In(GtaTaxScheme.Fcm5NoItc), ShipmentId = 14 },
            calculatedByUserId: 7);

        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal(14, dto.ShipmentId);
        Assert.Equal(7, dto.CalculatedBy);
        Assert.Null(dto.SupersededBy);
        Assert.Equal(10500.00m, dto.Result.GrandTotal);
        Assert.NotEqual(Guid.Empty, dto.Result.RuleVersionId);

        // round-trips from the store, scoped to the tenant
        var fetched = await svc.GetAsync(dto.Id);
        Assert.NotNull(fetched);
        Assert.Equal(dto.Id, fetched!.Id);
        Assert.Equal(1, await ctx.TaxCalculations.CountAsync());
    }

    [Fact]
    public async Task Apply_WithoutTenantContext_Throws()
    {
        var noTenant = new StubTenantContext(Guid.Empty);
        var (svc, ctx) = NewEngine(noTenant);
        SeedStandardRules(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ApplyAsync(new TaxCalculationApplyRequestDto { Input = In(GtaTaxScheme.Fcm5NoItc) }, 1));
    }

    [Fact] // G.15 — recalc preserves historical fidelity + builds a supersession chain
    public async Task Recalculate_SupersedesOldRow_AndBindsHistoricalRule()
    {
        var (svc, ctx) = NewEngine();
        // Two rows, same tuple, non-overlapping windows:
        ctx.GstRules.AddRange(
            Rule(GtaTaxScheme.Fcm5NoItc, RecipientCategory.RegisteredBusiness, PosRelation.IntraState,
                 GstTaxTreatment.ForwardCharge, gst: 0.05m, cgst: 0.025m, sgst: 0.025m,
                 notif: "R-CURRENT", from: new DateOnly(2024, 1, 1)),
            Rule(GtaTaxScheme.Fcm5NoItc, RecipientCategory.RegisteredBusiness, PosRelation.IntraState,
                 GstTaxTreatment.ForwardCharge, gst: 0.05m, cgst: 0.025m, sgst: 0.025m,
                 notif: "R-HISTORICAL", from: new DateOnly(2023, 1, 1), to: new DateOnly(2023, 12, 31))
        );
        ctx.SaveChanges();

        // Original transaction dated inside the 2023 window → binds the historical rule.
        var first = await svc.ApplyAsync(
            new TaxCalculationApplyRequestDto { Input = In(GtaTaxScheme.Fcm5NoItc, txn: new DateOnly(2023, 6, 15)) },
            calculatedByUserId: 1);
        Assert.Equal("R-HISTORICAL", first.Result.NotificationReference);

        var second = await svc.RecalculateAsync(first.Id, calculatedByUserId: 2);

        // Still bound to the 2023 rule — recalc did NOT jump to the current rule.
        Assert.Equal("R-HISTORICAL", second.Result.NotificationReference);
        Assert.NotEqual(first.Id, second.Id);

        // Supersession chain recorded; original row preserved (append-only).
        var oldRow = await svc.GetAsync(first.Id);
        Assert.Equal(second.Id, oldRow!.SupersededBy);
        Assert.Equal(2, await ctx.TaxCalculations.CountAsync());
    }

    [Fact] // recalculating an already-superseded row is rejected
    public async Task Recalculate_AlreadySuperseded_Throws()
    {
        var (svc, ctx) = NewEngine();
        SeedStandardRules(ctx);

        var first = await svc.ApplyAsync(
            new TaxCalculationApplyRequestDto { Input = In(GtaTaxScheme.Fcm5NoItc) }, 1);
        await svc.RecalculateAsync(first.Id, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RecalculateAsync(first.Id, 1));
    }
}
