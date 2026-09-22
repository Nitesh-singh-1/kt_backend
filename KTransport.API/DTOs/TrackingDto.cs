using System;
using System.Collections.Generic;
using KTransport.API.Models;

namespace KTransport.API.DTOs
{
    public class PublicTrackingDto
    {
        public string TrackingNumber { get; set; } = null!; // Shipment No (LR/GR)
        public DateOnly BookingDate { get; set; }
        public string FromLocation { get; set; } = null!;
        public string ToLocation { get; set; } = null!;
        public string CurrentStatus { get; set; } = null!;
        public string ConsignorMasked { get; set; } = null!; // e.g. "A*** C****" for privacy
        public string ConsigneeMasked { get; set; } = null!;
        public int TotalPackages { get; set; }
        public decimal TotalWeightKg { get; set; }
        public DateOnly? ExpectedDeliveryDate { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public string? DeliveredToPerson { get; set; }
        public List<TrackingTimelineEventDto> Timeline { get; set; } = new();
    }

    public class TrackingTimelineEventDto
    {
        public string EventTitle { get; set; } = null!;
        public string? Location { get; set; }
        public string? Description { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsCompleted { get; set; }
    }
}
