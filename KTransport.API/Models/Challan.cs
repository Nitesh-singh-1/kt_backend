using System;
using System.Collections.Generic;
using KTransport.API.Common;
using KTransport.API.Services;

namespace KTransport.API.Models
{
    public partial class Challan : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public string? ChallanNo { get; set; }

        public DateOnly ChallanDate { get; set; }

        public string? LorryNo { get; set; }

        public string? DriverName { get; set; }

        public string? VoiceDriverName { get; set; }

        public string? FromLocation { get; set; }

        public string? ToLocation { get; set; }

        public string? Remarks { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime CreatedDate { get; set; }

        public int? CreatedBy { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public int? ModifiedBy { get; set; }

        // Navigation properties
        public virtual User? CreatedByNavigation { get; set; }

        public virtual User? ModifiedByNavigation { get; set; }

        public virtual ICollection<ChallanDetail> ChallanDetails { get; set; } = new List<ChallanDetail>();
    }
}