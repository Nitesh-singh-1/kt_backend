using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class Location : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public string Code { get; set; } = null!; // Station/Branch Code e.g. BOM, DEL, PUN

        public string Name { get; set; } = null!; // Station Name e.g. Mumbai, Delhi, Pune

        public string? City { get; set; }

        public string? State { get; set; }

        public string? Address { get; set; }

        public string? Pincode { get; set; }

        /// <summary>
        /// Contact phone number for the branch / station — printed next to
        /// the station name on the Bilty footer so a consignee can reach
        /// the local office directly for pickup / delivery queries.
        /// </summary>
        public string? ContactNumber { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
