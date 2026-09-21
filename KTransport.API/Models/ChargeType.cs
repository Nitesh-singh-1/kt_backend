using System;
using System.Collections.Generic;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class ChargeType : ITenantScopedEntity
    {
        public int Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public string Code { get; set; } = null!;

        public string Name { get; set; } = null!;

        public decimal DefaultAmount { get; set; } = 0;

        public bool IsTaxable { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public virtual ICollection<ShipmentChargeItem> ShipmentChargeItems { get; set; } = new List<ShipmentChargeItem>();
    }
}
