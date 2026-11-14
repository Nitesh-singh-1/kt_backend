using System;

namespace KTransport.API.DTOs
{
    /// <summary>One money-in event against a bilty, for the payment-history UI.</summary>
    public class ShipmentPaymentDto
    {
        public long Id { get; set; }
        public long ShipmentId { get; set; }
        public decimal Amount { get; set; }
        public string? PaymentMode { get; set; }
        public string? ReferenceNo { get; set; }
        public DateOnly PaymentDate { get; set; }

        /// <summary>Raw source enum name (Receipt / OnSpot / BillBook / DeliverySettlement / OpeningBackfill).</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>Human label for the UI, e.g. "Money Receipt", "On-spot (booking)", "Bill Book", "Delivery Settlement".</summary>
        public string SourceLabel { get; set; } = string.Empty;

        /// <summary>True when this payment can be reversed from the bilty screen (false for Bill Book — reverse via the bill).</summary>
        public bool CanReverseHere { get; set; }

        public long? InvoiceId { get; set; }

        public bool IsReversed { get; set; }
        public DateTime? ReversedAt { get; set; }
        public string? ReversalReason { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ReversePaymentRequest
    {
        public string? Reason { get; set; }
    }
}
