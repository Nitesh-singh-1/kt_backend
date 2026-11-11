using System.Collections.Generic;

namespace KTransport.API.DTOs
{
    /// <summary>One ready-to-use WhatsApp share action for a shipment.</summary>
    public class WhatsAppShareLinkDto
    {
        /// <summary>Booking | Status | Delivered | PaymentReminder.</summary>
        public string Kind { get; set; } = null!;

        /// <summary>Button caption for the UI (e.g. "Send tracking to consignee").</summary>
        public string Label { get; set; } = null!;

        /// <summary>Normalised recipient number, or null when the picker is used.</summary>
        public string? ToMobile { get; set; }

        /// <summary>The plain-text message that will be pre-filled.</summary>
        public string Message { get; set; } = null!;

        /// <summary>The wa.me deep link the UI opens on tap.</summary>
        public string Url { get; set; } = null!;
    }

    public class ShipmentWhatsAppShareDto
    {
        public long ShipmentId { get; set; }
        public string ShipmentNo { get; set; } = null!;
        public string? TrackingUrl { get; set; }
        public List<WhatsAppShareLinkDto> Links { get; set; } = new();
    }
}
