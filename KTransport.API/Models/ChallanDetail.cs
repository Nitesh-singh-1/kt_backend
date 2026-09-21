using System;
using KTransport.API.Common;
using KTransport.API.Services;

namespace KTransport.API.Models
{
    public partial class ChallanDetail : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public long ChallanId { get; set; }

        public string? BillNo { get; set; }

        public int? Quantity { get; set; }

        public string? Destination { get; set; }

        public decimal? FreightAmount { get; set; }

        public int? BillTypeId { get; set; }

        public string? ConsigneeName { get; set; }

        public string? Remarks { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime CreatedDate { get; set; }

        // Navigation properties
        public virtual Challan Challan { get; set; } = null!;

        public virtual BillType? BillType { get; set; }
    }
}