using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace KTransport.API.Services
{
    public interface IWhatsAppShareService
    {
        /// <summary>Ready-to-send WhatsApp links for a shipment (tenant-scoped), or null if not found.</summary>
        Task<ShipmentWhatsAppShareDto?> GetShipmentShareLinksAsync(long shipmentId);
    }

    /// <summary>
    /// Produces free WhatsApp click-to-chat links for customer updates (booking,
    /// status, delivery, payment reminder) using data already on the shipment.
    /// No external messaging API or cost — see <see cref="WhatsAppLinkBuilder"/>.
    /// </summary>
    public class WhatsAppShareService : IWhatsAppShareService
    {
        private readonly KTransportDbContext _context;
        private readonly IConfiguration _config;

        public WhatsAppShareService(KTransportDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public async Task<ShipmentWhatsAppShareDto?> GetShipmentShareLinksAsync(long shipmentId)
        {
            var s = await _context.Shipments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == shipmentId); // tenant filter applies

            if (s == null) return null;

            var trackingUrl = BuildTrackingUrl(s.ShipmentNo);
            var consignee = string.IsNullOrWhiteSpace(s.ConsigneeName) ? "Customer" : s.ConsigneeName!;
            var route = $"{s.FromLocation ?? "Origin"} to {s.ToLocation ?? "Destination"}";

            var dto = new ShipmentWhatsAppShareDto
            {
                ShipmentId = s.Id,
                ShipmentNo = s.ShipmentNo,
                TrackingUrl = trackingUrl
            };

            // Prefer the consignee (the person awaiting goods) for delivery updates;
            // fall back to the consignor if no consignee number is on file.
            var updateMobile = FirstNonEmpty(s.ConsigneeMobile, s.ConsignorMobile);

            dto.Links.Add(Make("Booking", "Send booking + tracking to consignee", updateMobile,
                $"Namaste {consignee}! Your consignment (Bilty {s.ShipmentNo}), {route}, has been booked. " +
                $"Track it live here: {trackingUrl}"));

            dto.Links.Add(Make("Status", "Send latest status to consignee", updateMobile,
                $"Update on your consignment (Bilty {s.ShipmentNo}): {StatusLabel(s.Status)}. " +
                $"Track live: {trackingUrl}"));

            if (s.Status == ShipmentStatus.Delivered)
            {
                dto.Links.Add(Make("Delivered", "Send delivery confirmation", updateMobile,
                    $"Your consignment (Bilty {s.ShipmentNo}) has been delivered. Thank you for shipping with us! " +
                    $"Details: {trackingUrl}"));
            }

            if (s.DueAmount > 0)
            {
                // Payment usually sits with the billing party (consignor); fall back to consignee.
                var payMobile = FirstNonEmpty(s.ConsignorMobile, s.ConsigneeMobile);
                dto.Links.Add(Make("PaymentReminder", "Send payment reminder", payMobile,
                    $"Gentle reminder: {Inr(s.DueAmount)} is pending against Bilty {s.ShipmentNo} ({route}). " +
                    $"Kindly arrange the payment at your convenience. Details: {trackingUrl}"));
            }

            return dto;
        }

        private string? BuildTrackingUrl(string shipmentNo)
        {
            var baseUrl = (_config["App:FrontendBaseUrl"] ?? string.Empty).Trim();
            var path = $"/tracking?lr={Uri.EscapeDataString(shipmentNo)}";
            return string.IsNullOrEmpty(baseUrl) ? path : $"{baseUrl.TrimEnd('/')}{path}";
        }

        private static WhatsAppShareLinkDto Make(string kind, string label, string? mobile, string message) => new()
        {
            Kind = kind,
            Label = label,
            ToMobile = WhatsAppLinkBuilder.NormalizeMobile(mobile),
            Message = message,
            Url = WhatsAppLinkBuilder.BuildShareUrl(mobile, message)
        };

        private static string? FirstNonEmpty(params string?[] vals)
        {
            foreach (var v in vals)
                if (!string.IsNullOrWhiteSpace(v)) return v;
            return null;
        }

        private static string Inr(decimal amount) => $"Rs. {amount:N2}";

        private static string StatusLabel(ShipmentStatus status) => status switch
        {
            ShipmentStatus.Draft => "Draft created",
            ShipmentStatus.Booked => "Booking confirmed",
            ShipmentStatus.Manifested => "Loaded into vehicle",
            ShipmentStatus.InTransit => "Dispatched / in transit",
            ShipmentStatus.OutForDelivery => "Arrived at destination hub; out for delivery",
            ShipmentStatus.Delivered => "Delivered",
            ShipmentStatus.Returned => "Returned to origin",
            ShipmentStatus.Cancelled => "Booking cancelled",
            _ => status.ToString()
        };
    }
}
