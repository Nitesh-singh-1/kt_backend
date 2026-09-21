using System;
using System.Collections.Generic;

namespace KTransport.API.Models
{
    public class Tenant
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation collections
        public virtual ICollection<User> Users { get; set; } = new List<User>();
        public virtual ICollection<GstBill> GstBills { get; set; } = new List<GstBill>();
        public virtual ICollection<WithoutGstBill> WithoutGstBills { get; set; } = new List<WithoutGstBill>();
        public virtual ICollection<Challan> Challans { get; set; } = new List<Challan>();
    }
}
