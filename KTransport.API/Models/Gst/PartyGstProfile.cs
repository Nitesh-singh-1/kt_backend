using System;
using KTransport.API.Common;

namespace KTransport.API.Models.Gst;

public class PartyGstProfile : ITenantScopedEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public int PartyId { get; set; }

    public string? Gstin { get; set; }
    public bool GstRegistered { get; set; }
    public string? State { get; set; }
    public string? StateCode { get; set; }

    public RecipientCategory RecipientCategory { get; set; } = RecipientCategory.UnregisteredBusiness;
    public string? PlaceOfSupplyDefault { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
