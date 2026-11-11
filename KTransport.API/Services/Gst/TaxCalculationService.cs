using System;
using System.Text.Json;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.DTOs.Gst;
using KTransport.API.Models.Gst;
using KTransport.API.Services;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services.Gst;

/// <summary>
/// Decision engine for GST on transportation services. Implements the §B
/// flow from .agent/specs/GST-IMPLEMENTATION.md. All rates/treatments are
/// resolved by looking up a <see cref="GstRule"/> row — never hard-coded here.
/// </summary>
public class TaxCalculationService : ITaxCalculationService
{
    private readonly IGstRuleRepository _rules;
    private readonly KTransportDbContext _db;
    private readonly ITenantContext _tenantContext;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TaxCalculationService(
        IGstRuleRepository rules,
        KTransportDbContext db,
        ITenantContext tenantContext)
    {
        _rules = rules;
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<TaxCalculationResultDto> CalculateAsync(TaxCalculationInputDto x)
    {
        return (await CalculateInternalAsync(x)).result;
    }

    public async Task<TaxCalculationDto> ApplyAsync(TaxCalculationApplyRequestDto request, int? calculatedByUserId)
    {
        if (!_tenantContext.HasTenant)
            throw new InvalidOperationException("Tenant context required to persist a tax calculation.");

        var (result, rule) = await CalculateInternalAsync(request.Input);
        var audit = PersistAudit(request.Input, result, rule, request.BiltyId, request.ShipmentId, request.InvoiceId, calculatedByUserId);
        await _db.SaveChangesAsync();
        return ToDto(audit, result);
    }

    public async Task<TaxCalculationDto> RecalculateAsync(Guid taxCalculationId, int? calculatedByUserId)
    {
        if (!_tenantContext.HasTenant)
            throw new InvalidOperationException("Tenant context required.");

        var old = await _db.TaxCalculations.FirstOrDefaultAsync(c => c.Id == taxCalculationId)
            ?? throw new InvalidOperationException("Tax calculation not found.");

        if (old.SupersededBy.HasValue)
            throw new InvalidOperationException("Tax calculation already superseded.");

        var input = JsonSerializer.Deserialize<TaxCalculationInputDto>(old.InputJson, JsonOpts)
            ?? throw new InvalidOperationException("Stored input JSON is not deserializable.");

        // TASK-033: If a GstRegistrationId was pinned at write time, re-resolve
        // the profile active on the ORIGINAL txn date against THAT registration
        // (not via Location, which may have been re-pointed since). Preserves
        // historical fidelity even across location->registration moves.
        if (input.GstRegistrationId.HasValue)
        {
            var txnDate = input.TransactionDate ?? DateOnly.FromDateTime(old.CalculatedAt);
            var registration = await _db.GstRegistrations
                .FirstOrDefaultAsync(r => r.Id == input.GstRegistrationId.Value);
            var activeProfile = await _db.GstRegistrationGstProfiles
                .Where(p => p.GstRegistrationId == input.GstRegistrationId.Value)
                .Where(p => p.EffectiveFrom <= txnDate && (p.EffectiveTo == null || p.EffectiveTo >= txnDate))
                .OrderByDescending(p => p.EffectiveFrom)
                .FirstOrDefaultAsync();

            if (registration != null)
            {
                input.ProviderState = registration.State;
                input.ProviderStateCode = registration.StateCode;
            }
            if (activeProfile != null)
            {
                input.ProviderIsGta = activeProfile.IsGta;
                input.IssuesConsignmentNote = activeProfile.IssuesConsignmentNote;
                input.ProviderGstRegistered = activeProfile.GstRegistered;
                input.GtaTaxScheme = activeProfile.GtaTaxScheme;
            }
        }

        // Historical fidelity: keep the ORIGINAL txn date so the rule effective
        // on that date still applies, even if newer rules exist today.
        var (result, rule) = await CalculateInternalAsync(input);
        var neu = PersistAudit(input, result, rule, old.BiltyId, old.ShipmentId, old.InvoiceId, calculatedByUserId);
        old.SupersededBy = neu.Id;
        await _db.SaveChangesAsync();
        return ToDto(neu, result);
    }

    public async Task<TaxCalculationDto?> GetAsync(Guid id)
    {
        var row = await _db.TaxCalculations.FirstOrDefaultAsync(c => c.Id == id);
        if (row == null) return null;

        var dto = new TaxCalculationDto
        {
            Id = row.Id,
            TenantId = row.TenantId,
            BiltyId = row.BiltyId,
            ShipmentId = row.ShipmentId,
            InvoiceId = row.InvoiceId,
            CalculatedAt = row.CalculatedAt,
            CalculatedBy = row.CalculatedBy,
            SupersededBy = row.SupersededBy,
            Result = new TaxCalculationResultDto
            {
                TaxableAmount = row.TaxableAmount,
                TaxTreatment = row.TaxTreatment,
                GstRate = row.GstRate,
                CgstRate = row.CgstRate,
                CgstAmount = row.CgstAmount ?? 0,
                SgstRate = row.SgstRate,
                SgstAmount = row.SgstAmount ?? 0,
                IgstRate = row.IgstRate,
                IgstAmount = row.IgstAmount ?? 0,
                UtgstRate = row.UtgstRate,
                UtgstAmount = row.UtgstAmount ?? 0,
                TotalTax = row.TotalTax,
                GrandTotal = row.GrandTotal,
                IsRcm = row.IsRcm,
                IsItcEligible = row.IsItcEligible,
                PosRelation = row.PosRelation ?? Models.Gst.PosRelation.IntraState,
                PlaceOfSupply = row.PlaceOfSupply,
                RuleVersionId = row.RuleVersionId ?? Guid.Empty,
                NotificationReference = row.NotificationReference
            }
        };
        return dto;
    }

    // --------------------- core calculation -----------------------------

    private async Task<(TaxCalculationResultDto result, GstRule rule)> CalculateInternalAsync(TaxCalculationInputDto x)
    {
        if (x.FreightAmount < 0)
            throw new ArgumentException("FreightAmount must be non-negative.");

        var txnDate = x.TransactionDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // OwnGoodsMovement — not a supply; short-circuit without touching rules.
        if (x.ServiceType == GstServiceType.OwnGoodsMovement)
        {
            var ownResult = new TaxCalculationResultDto
            {
                TaxableAmount = x.FreightAmount,
                TaxTreatment = GstTaxTreatment.OutOfScope,
                PosRelation = ClassifyPos(x),
                PlaceOfSupply = x.PlaceOfSupply,
                GrandTotal = x.FreightAmount
            };
            // No rule row for OutOfScope — build a synthetic sentinel so persistence
            // still works; audit stores NULL rule_version_id in that case.
            return (ownResult, new GstRule
            {
                Id = Guid.Empty,
                TaxTreatment = GstTaxTreatment.OutOfScope,
                EffectiveFrom = txnDate
            });
        }

        var posRelation = ClassifyPos(x);

        var rule = await _rules.FindOneAsync(
            serviceType: x.ServiceType,
            providerIsGta: x.ProviderIsGta,
            issuesConsignmentNote: x.IssuesConsignmentNote,
            providerGstRegistered: x.ProviderGstRegistered,
            gtaTaxScheme: x.GtaTaxScheme,
            recipientCategory: x.RecipientCategory,
            posRelation: posRelation,
            onDate: txnDate);

        if (rule == null) throw new GstRuleUnresolvedException(x);

        var taxable = x.FreightAmount;
        if (x.TaxInclusive && rule.GstRate.HasValue && rule.GstRate.Value > 0)
        {
            taxable = Math.Round(x.FreightAmount / (1m + rule.GstRate.Value), 2, MidpointRounding.AwayFromZero);
        }

        if (rule.TaxTreatment == GstTaxTreatment.Exempt)
            return (Build(rule, taxable, posRelation, x, 0, 0, 0, 0, false), rule);
        if (rule.TaxTreatment == GstTaxTreatment.ReverseCharge)
            return (Build(rule, taxable, posRelation, x, 0, 0, 0, 0, true), rule);

        var cgstAmount = Round(taxable * (rule.CgstRate ?? 0m));
        var sgstAmount = Round(taxable * (rule.SgstRate ?? 0m));
        var igstAmount = Round(taxable * (rule.IgstRate ?? 0m));
        var utgstAmount = Round(taxable * (rule.UtgstRate ?? 0m));

        return (Build(rule, taxable, posRelation, x, cgstAmount, sgstAmount, igstAmount, utgstAmount, false), rule);
    }

    private TaxCalculation PersistAudit(
        TaxCalculationInputDto input,
        TaxCalculationResultDto result,
        GstRule rule,
        int? biltyId, long? shipmentId, int? invoiceId, int? calculatedByUserId)
    {
        var audit = new TaxCalculation
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.CurrentTenantId,
            BiltyId = biltyId,
            ShipmentId = shipmentId,
            InvoiceId = invoiceId,
            InputJson = JsonSerializer.Serialize(input, JsonOpts),
            RuleVersionId = rule.Id == Guid.Empty ? null : rule.Id,
            TaxTreatment = result.TaxTreatment,
            GstRate = result.GstRate,
            CgstRate = result.CgstRate,
            CgstAmount = result.CgstAmount,
            SgstRate = result.SgstRate,
            SgstAmount = result.SgstAmount,
            IgstRate = result.IgstRate,
            IgstAmount = result.IgstAmount,
            UtgstRate = result.UtgstRate,
            UtgstAmount = result.UtgstAmount,
            TaxableAmount = result.TaxableAmount,
            TotalTax = result.TotalTax,
            GrandTotal = result.GrandTotal,
            IsRcm = result.IsRcm,
            IsItcEligible = result.IsItcEligible,
            PlaceOfSupply = result.PlaceOfSupply,
            PosRelation = result.PosRelation,
            CalculatedBy = calculatedByUserId,
            CalculatedAt = DateTime.UtcNow,
            NotificationReference = result.NotificationReference
        };
        _db.TaxCalculations.Add(audit);
        return audit;
    }

    // --------------------- helpers -----------------------------

    private static PosRelation ClassifyPos(TaxCalculationInputDto x)
    {
        if (x.PlaceOfSupplyIsUtWithoutLegislature == true) return PosRelation.UnionTerritory;

        var providerCode = x.ProviderStateCode ?? string.Empty;
        var posCode = x.PlaceOfSupplyStateCode ?? x.RecipientStateCode ?? string.Empty;

        if (string.IsNullOrWhiteSpace(providerCode) || string.IsNullOrWhiteSpace(posCode))
            return PosRelation.IntraState;

        return string.Equals(providerCode, posCode, StringComparison.OrdinalIgnoreCase)
            ? PosRelation.IntraState
            : PosRelation.InterState;
    }

    private static decimal Round(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);

    private static TaxCalculationResultDto Build(
        GstRule rule, decimal taxable, PosRelation posRelation, TaxCalculationInputDto x,
        decimal cgst, decimal sgst, decimal igst, decimal utgst, bool isRcm)
    {
        var totalTax = cgst + sgst + igst + utgst;
        return new TaxCalculationResultDto
        {
            TaxableAmount = Round(taxable),
            TaxTreatment = rule.TaxTreatment,
            GstRate = rule.GstRate,
            CgstRate = rule.CgstRate,
            CgstAmount = cgst,
            SgstRate = rule.SgstRate,
            SgstAmount = sgst,
            IgstRate = rule.IgstRate,
            IgstAmount = igst,
            UtgstRate = rule.UtgstRate,
            UtgstAmount = utgst,
            TotalTax = Round(totalTax),
            GrandTotal = Round(taxable + totalTax),
            IsRcm = isRcm,
            IsItcEligible = rule.ItcEligibleForProvider,
            PosRelation = posRelation,
            PlaceOfSupply = x.PlaceOfSupply,
            RuleVersionId = rule.Id,
            NotificationReference = rule.NotificationReference
        };
    }

    private static TaxCalculationDto ToDto(TaxCalculation row, TaxCalculationResultDto result) => new()
    {
        Id = row.Id,
        TenantId = row.TenantId,
        BiltyId = row.BiltyId,
        ShipmentId = row.ShipmentId,
        InvoiceId = row.InvoiceId,
        Result = result,
        CalculatedAt = row.CalculatedAt,
        CalculatedBy = row.CalculatedBy,
        SupersededBy = row.SupersededBy
    };
}
