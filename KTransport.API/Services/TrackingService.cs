using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    public class TrackingService : ITrackingService
    {
        private readonly KTransportDbContext _context;

        public TrackingService(KTransportDbContext context)
        {
            _context = context;
        }

        public async Task<PublicTrackingDto?> GetTrackingInfoAsync(string trackingNo)
        {
            if (string.IsNullOrWhiteSpace(trackingNo)) return null;

            var s = trackingNo.Trim().ToLower();

            // Use IgnoreQueryFilters so tracking can find shipments across all tenants anonymously
            var shipment = await _context.Shipments
                .IgnoreQueryFilters()
                .Include(x => x.Items)
                .Include(x => x.StatusHistory)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ShipmentNo.ToLower() == s ||
                                         (x.InvoiceNo != null && x.InvoiceNo.ToLower() == s));

            if (shipment == null) return null;

            // Fetch POD if exists
            var pod = await _context.PodRecords
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ShipmentId == shipment.Id && p.Status == PodStatus.Verified);

            var timeline = new List<TrackingTimelineEventDto>();

            // 1. Booking Event
            timeline.Add(new TrackingTimelineEventDto
            {
                EventTitle = "Consignment Booked",
                Location = shipment.FromLocation ?? "Origin Station",
                Description = $"Consignment registered with LR No. {shipment.ShipmentNo}",
                Timestamp = shipment.CreatedAt,
                IsCompleted = true
            });

            // 2. Audit History Events
            if (shipment.StatusHistory != null)
            {
                foreach (var h in shipment.StatusHistory.OrderBy(x => x.ChangedAt))
                {
                    timeline.Add(new TrackingTimelineEventDto
                    {
                        EventTitle = GetStatusTitle(h.ToStatus),
                        Location = h.Location,
                        Description = h.Remarks ?? $"Status updated to {h.ToStatus}",
                        Timestamp = h.ChangedAt,
                        IsCompleted = true
                    });
                }
            }

            // 3. POD Event if verified
            if (pod != null)
            {
                timeline.Add(new TrackingTimelineEventDto
                {
                    EventTitle = "Delivered & POD Verified",
                    Location = shipment.ToLocation,
                    Description = $"Delivered to {pod.ReceiverName}",
                    Timestamp = pod.VerifiedAt ?? pod.CreatedAt,
                    IsCompleted = true
                });
            }

            return new PublicTrackingDto
            {
                TrackingNumber = shipment.ShipmentNo,
                BookingDate = shipment.ShipmentDate,
                FromLocation = shipment.FromLocation ?? "Origin",
                ToLocation = shipment.ToLocation ?? "Destination",
                CurrentStatus = shipment.Status.ToString(),
                ConsignorMasked = MaskName(shipment.ConsignorName),
                ConsigneeMasked = MaskName(shipment.ConsigneeName),
                TotalPackages = shipment.Items?.Sum(i => i.Quantity) ?? 1,
                TotalWeightKg = shipment.Items?.Sum(i => i.Weight) ?? 0,
                DeliveredAt = pod?.VerifiedAt,
                DeliveredToPerson = pod?.ReceiverName,
                Timeline = timeline
            };
        }

        private static string MaskName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Customer";
            if (name.Length <= 3) return name[0] + "**";
            return name.Substring(0, 2) + new string('*', name.Length - 4) + name.Substring(name.Length - 2);
        }

        private static string GetStatusTitle(ShipmentStatus status)
        {
            return status switch
            {
                ShipmentStatus.Draft => "Draft Created",
                ShipmentStatus.Booked => "Booking Confirmed",
                ShipmentStatus.Manifested => "Loaded into Vehicle / Manifest Prepared",
                ShipmentStatus.InTransit => "Dispatched / In Transit",
                ShipmentStatus.OutForDelivery => "Arrived at Destination Hub / Out for Delivery",
                ShipmentStatus.Delivered => "Delivered",
                ShipmentStatus.Returned => "Returned to Origin",
                ShipmentStatus.Cancelled => "Booking Cancelled",
                _ => status.ToString()
            };
        }
    }
}
