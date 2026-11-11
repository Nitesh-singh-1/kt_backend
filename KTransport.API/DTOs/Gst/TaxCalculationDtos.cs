using System;
using KTransport.API.Models.Gst;

namespace KTransport.API.DTOs.Gst;

public class TaxCalculationInputDto
{
    public GstServiceType ServiceType { get; set; } = GstServiceType.GtaRoadTransport;

    public bool ProviderIsGta { get; set; }
    public bool IssuesConsignmentNote { get; set; }
    public bool ProviderGstRegistered { get; set; }
    public string? ProviderState { get; set; }
    public string? ProviderStateCode { get; set; }
    public GtaTaxScheme GtaTaxScheme { get; set; } = GtaTaxScheme.NotApplicable;

    public RecipientCategory RecipientCategory { get; set; } = RecipientCategory.UnregisteredBusiness;
    public string? RecipientState { get; set; }
    public string? RecipientStateCode { get; set; }

    public string? PlaceOfSupply { get; set; }
    public string? PlaceOfSupplyStateCode { get; set; }
    public bool? PlaceOfSupplyIsUtWithoutLegislature { get; set; }

    public decimal FreightAmount { get; set; }
    public bool TaxInclusive { get; set; } = false;

    public DateOnly? TransactionDate { get; set; }

    public string? OverrideReason { get; set; }

    /// <summary>
    /// TASK-033 — Location at which this shipment was booked. Stored into
    /// <c>tax_calculations.input_json</c> so a recalculation stays correct
    /// even if the Location is later re-pointed to a different registration.
    /// </summary>
    public long? BookingLocationId { get; set; }

    /// <summary>
    /// TASK-033 — Registration resolved at write-time (BookingLocation.GstRegistrationId).
    /// Recalculation re-resolves the profile by (gstRegistrationId, originalTxnDate)
    /// instead of walking Location -> Registration again (Location may have moved).
    /// </summary>
    public long? GstRegistrationId { get; set; }
}

public class TaxCalculationResultDto
{
    public decimal TaxableAmount { get; set; }
    public GstTaxTreatment TaxTreatment { get; set; }
    public decimal? GstRate { get; set; }
    public decimal? CgstRate { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal? SgstRate { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal? IgstRate { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal? UtgstRate { get; set; }
    public decimal UtgstAmount { get; set; }
    public decimal TotalTax { get; set; }
    public decimal GrandTotal { get; set; }
    public bool IsRcm { get; set; }
    public bool IsItcEligible { get; set; }
    public PosRelation PosRelation { get; set; }
    public string? PlaceOfSupply { get; set; }
    public Guid RuleVersionId { get; set; }
    public string? NotificationReference { get; set; }
}

public class TaxCalculationApplyRequestDto
{
    public TaxCalculationInputDto Input { get; set; } = new();

    /// <summary>Optional legacy GstBill id (int key) this calculation attaches to.</summary>
    public int? BiltyId { get; set; }

    /// <summary>Optional modern Shipment id (long key) this calculation attaches to.</summary>
    public long? ShipmentId { get; set; }

    /// <summary>Optional Invoice id this calculation attaches to.</summary>
    public int? InvoiceId { get; set; }
}

/// <summary>Full audit record returned on apply / recalculate / fetch.</summary>
public class TaxCalculationDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public int? BiltyId { get; set; }
    public long? ShipmentId { get; set; }
    public int? InvoiceId { get; set; }

    public TaxCalculationResultDto Result { get; set; } = new();

    public DateTime CalculatedAt { get; set; }
    public int? CalculatedBy { get; set; }

    public Guid? SupersededBy { get; set; }
}
