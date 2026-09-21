using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class NumberingSequence : ITenantScopedEntity
    {
        public int Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public string EntityType { get; set; } = "SHIPMENT"; // SHIPMENT, CHALLAN, INVOICE

        public string Prefix { get; set; } = "GR";

        public long CurrentValue { get; set; } = 0;

        public int Padding { get; set; } = 4;

        public int Year { get; set; } = DateTime.UtcNow.Year;

        public string FormatPattern { get; set; } = "{PREFIX}-{YEAR}-{SEQ}"; // e.g. GR-2026-0001
    }
}
