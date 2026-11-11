using System;
using KTransport.API.Models.Gst;

namespace KTransport.API.DTOs.Gst;

public class GstRuleDto
{
    public Guid Id { get; set; }
    public GstServiceType ServiceType { get; set; }
    public bool ProviderIsGta { get; set; }
    public bool IssuesConsignmentNote { get; set; }
    public bool ProviderGstRegistered { get; set; }
    public GtaTaxScheme GtaTaxScheme { get; set; }
    public RecipientCategory RecipientCategory { get; set; }
    public PosRelation PosRelation { get; set; }

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

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool Active { get; set; }
}

public class GstRuleCreateDto
{
    public GstServiceType ServiceType { get; set; }
    public bool ProviderIsGta { get; set; }
    public bool IssuesConsignmentNote { get; set; }
    public bool ProviderGstRegistered { get; set; }
    public GtaTaxScheme GtaTaxScheme { get; set; }
    public RecipientCategory RecipientCategory { get; set; }
    public PosRelation PosRelation { get; set; }

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

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

public class GstRuleUpdateDto
{
    public DateOnly? EffectiveTo { get; set; }
    public bool? Active { get; set; }
}
