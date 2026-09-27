using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class Quotation : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public string QuoteNo { get; set; } = null!;

        public DateOnly QuoteDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        public DateOnly? ValidUntil { get; set; }

        public long? PartyId { get; set; }

        public virtual Party? Party { get; set; }

        public string? PartyName { get; set; }

        public string? PartyMobile { get; set; }

        public string? PartyGstNo { get; set; }

        public string? FromLocation { get; set; }

        public string? ToLocation { get; set; }

        public string? VehicleType { get; set; }

        public string? GoodsDescription { get; set; }

        public decimal? WeightKg { get; set; }

        public decimal? RatePerUnit { get; set; }

        public string? RateBasis { get; set; } // Per Kg, Per MT, Per Trip, Fixed

        public decimal EstimatedFreight { get; set; } = 0;

        public QuotationStatus Status { get; set; } = QuotationStatus.Draft;

        /// <summary>Reference to the booking/GR created when this quote was converted.</summary>
        public string? ConvertedRef { get; set; }

        public string? Terms { get; set; }

        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
