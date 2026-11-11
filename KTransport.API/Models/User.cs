using System;
using System.Collections.Generic;
using KTransport.API.Common;

namespace KTransport.API.Models;

public partial class User : ITenantScopedEntity
{
    public int Id { get; set; }

    public Guid TenantId { get; set; }

    public virtual Tenant? Tenant { get; set; }

    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string? FullName { get; set; }

    public string? Role { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? Mobile { get; set; }

    public string? Email { get; set; }

    /// <summary>TASK-033 — Default operational Location this user books shipments at. Nullable.</summary>
    public long? HomeLocationId { get; set; }
    public virtual Location? HomeLocation { get; set; }

    public virtual ICollection<GstBill> GstBillCreatedByNavigations { get; set; } = new List<GstBill>();

    public virtual ICollection<GstBill> GstBillUpdatedByNavigations { get; set; } = new List<GstBill>();

    public virtual ICollection<WithoutGstBill> WithoutGstBillCreatedByNavigations { get; set; } = new List<WithoutGstBill>();

    public virtual ICollection<WithoutGstBill> WithoutGstBillUpdatedByNavigations { get; set; } = new List<WithoutGstBill>();

    // Add Challan navigation properties
    public virtual ICollection<Challan> ChallanCreatedByNavigations { get; set; } = new List<Challan>();

    public virtual ICollection<Challan> ChallanModifiedByNavigations { get; set; } = new List<Challan>();
}
