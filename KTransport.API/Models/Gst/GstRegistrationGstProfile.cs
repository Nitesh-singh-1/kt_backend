using System;
using KTransport.API.Common;

namespace KTransport.API.Models.Gst;

/// <summary>
/// TASK-033 — Versioned GST profile keyed on <see cref="GstRegistrationId"/>.
/// Formerly <c>CompanyGstProfile</c> (keyed per-tenant). State / StateCode
/// now live on the parent <see cref="GstRegistration"/> row and are no
/// longer versioned here.
///
/// A new row CLOSES the current active row (EffectiveTo = new.EffectiveFrom - 1)
/// and inserts the new one. Historical rows are never updated.
/// </summary>
public class GstRegistrationGstProfile : ITenantScopedEntity
{
    public Guid Id { get; set; }

    /// <summary>Denormalized tenant filter — same semantics as the old column.</summary>
    public Guid TenantId { get; set; }

    public long GstRegistrationId { get; set; }
    public virtual GstRegistration? GstRegistration { get; set; }

    public string? Gstin { get; set; }
    public bool GstRegistered { get; set; }

    public bool IsGta { get; set; }
    public bool IssuesConsignmentNote { get; set; }
    public GtaTaxScheme GtaTaxScheme { get; set; } = GtaTaxScheme.NotApplicable;

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
