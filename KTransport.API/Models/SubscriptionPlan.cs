using System;
using System.Collections.Generic;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class SubscriptionPlan
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        public string Tier { get; set; } = "Starter"; // Starter, Professional, Enterprise

        public string Description { get; set; } = string.Empty;

        public int MaxVehicles { get; set; } = 10;

        public int MaxUsers { get; set; } = 5;

        public int MaxMonthlyShipments { get; set; } = 200;

        public int StorageLimitMB { get; set; } = 5120;

        public decimal MonthlyPrice { get; set; } = 0.0m;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<TenantSubscription> TenantSubscriptions { get; set; } = new List<TenantSubscription>();
    }

    public class TenantSubscription : ITenantScopedEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public Guid SubscriptionPlanId { get; set; }

        public virtual SubscriptionPlan? SubscriptionPlan { get; set; }

        public string Status { get; set; } = "Active"; // Active, Trialing, Expired, Suspended

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ExpiresAt { get; set; }

        public bool IsAutoRenew { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
