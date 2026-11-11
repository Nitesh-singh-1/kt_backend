using System;
using System.Collections.Generic;
using KTransport.API.Common;

namespace KTransport.API.Models.Gst;

/// <summary>
/// TASK-033 — First-class entity carrying the GSTIN + state + GTA flags
/// for one of a tenant's statewise registrations. Operational Locations
/// roll up to one GstRegistration row; the tax engine resolves provider
/// identity through Shipment.BookingLocation -> GstRegistration ->
/// active <see cref="GstRegistrationGstProfile"/>.
/// </summary>
public class GstRegistration : ITenantScopedEntity
{
    public long Id { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = null!;   // "Maharashtra Office" etc.
    public string State { get; set; } = null!;  // "Maharashtra"
    public string StateCode { get; set; } = null!; // "27"

    public string? Address { get; set; }
    public string? Pincode { get; set; }

    /// <summary>Exactly one true per tenant — enforced by a partial unique index.</summary>
    public bool IsPrincipal { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<GstRegistrationGstProfile> Profiles { get; set; }
        = new List<GstRegistrationGstProfile>();
}
