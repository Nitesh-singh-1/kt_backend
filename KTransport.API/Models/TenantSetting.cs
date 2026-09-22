using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class TenantSetting : ITenantScopedEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        // Structured JSON configuration blocks
        public string GeneralJson { get; set; } = "{}";

        public string BillingAndTaxJson { get; set; } = "{}";

        public string DocumentSequencesJson { get; set; } = "[]";

        public string OperationalWorkflowsJson { get; set; } = "{}";

        public string FeatureFlagsJson { get; set; } = "{}";

        public string IntegrationsJson { get; set; } = "{}";

        public string MenuEntitlementsJson { get; set; } = "{}";

        public string CustomSettingsJson { get; set; } = "{}";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
