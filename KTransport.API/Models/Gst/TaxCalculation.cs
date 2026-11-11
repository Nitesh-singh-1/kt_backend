using System;
using KTransport.API.Common;

namespace KTransport.API.Models.Gst;

/// <summary>
/// Append-only audit of every tax calculation. Never updated; a recalc
/// inserts a new row and the Bilty/Shipment pointer is swung to it,
/// preserving history.
/// </summary>
public class TaxCalculation : ITenantScopedEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public int? BiltyId { get; set; }
    public long? ShipmentId { get; set; }
    public int? InvoiceId { get; set; }

    public string InputJson { get; set; } = "{}";

    public Guid? RuleVersionId { get; set; }

    public GstTaxTreatment TaxTreatment { get; set; }
    public decimal? GstRate { get; set; }
    public decimal? CgstRate { get; set; }
    public decimal? CgstAmount { get; set; }
    public decimal? SgstRate { get; set; }
    public decimal? SgstAmount { get; set; }
    public decimal? IgstRate { get; set; }
    public decimal? IgstAmount { get; set; }
    public decimal? UtgstRate { get; set; }
    public decimal? UtgstAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TotalTax { get; set; }
    public decimal GrandTotal { get; set; }

    public bool IsRcm { get; set; }
    public bool IsItcEligible { get; set; }

    public string? PlaceOfSupply { get; set; }
    public PosRelation? PosRelation { get; set; }

    public int? CalculatedBy { get; set; }
    public DateTime CalculatedAt { get; set; }

    public string? NotificationReference { get; set; }

    public Guid? SupersededBy { get; set; }
}
