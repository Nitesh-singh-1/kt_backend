using System;

namespace KTransport.API.Models.Gst;

/// <summary>
/// Versioned GST rule row. Global (not tenant-scoped) — platform-admin maintained.
/// A row referenced by any TaxCalculation is immutable except for EffectiveTo
/// and Active, enforced in GstRuleRepository.
/// </summary>
public class GstRule
{
    public Guid Id { get; set; }

    // Input tuple
    public GstServiceType ServiceType { get; set; }
    public bool ProviderIsGta { get; set; }
    public bool IssuesConsignmentNote { get; set; }
    public bool ProviderGstRegistered { get; set; }
    public GtaTaxScheme GtaTaxScheme { get; set; }
    public RecipientCategory RecipientCategory { get; set; }
    public PosRelation PosRelation { get; set; }

    // Resolved treatment
    public GstTaxTreatment TaxTreatment { get; set; }
    public decimal? GstRate { get; set; }
    public decimal? CgstRate { get; set; }
    public decimal? SgstRate { get; set; }
    public decimal? IgstRate { get; set; }
    public decimal? UtgstRate { get; set; }

    public bool ItcEligibleForProvider { get; set; }
    public string? RcmPayableBy { get; set; }
    public string? NotificationReference { get; set; }
    public string? ExceptionsNote { get; set; }

    // Validity window
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool Active { get; set; } = true;

    // Audit
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
