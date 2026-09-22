using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KTransport.API.Services
{
    public class PodService : IPodService
    {
        private readonly KTransportDbContext _context;
        private readonly ILogger<PodService> _logger;

        public PodService(KTransportDbContext context, ILogger<PodService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<PodRecordDto>> GetPodsAsync(PodStatus? status = null, string? search = null)
        {
            var query = _context.PodRecords.AsNoTracking();

            if (status.HasValue)
            {
                query = query.Where(p => p.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(p =>
                    p.ShipmentNo != null && p.ShipmentNo.ToLower().Contains(s) ||
                    p.ReceiverName.ToLower().Contains(s) ||
                    (p.ReceiverMobile != null && p.ReceiverMobile.Contains(s))
                );
            }

            var pods = await query.OrderByDescending(p => p.DeliveryDate).ThenByDescending(p => p.Id).ToListAsync();
            return pods.Select(MapToDto).ToList();
        }

        public async Task<PodRecordDto?> GetPodByShipmentIdAsync(long shipmentId)
        {
            var pod = await _context.PodRecords.AsNoTracking().FirstOrDefaultAsync(p => p.ShipmentId == shipmentId);
            return pod == null ? null : MapToDto(pod);
        }

        public async Task<PodRecordDto> UploadPodAsync(UploadPodRequest request, int? userId = null)
        {
            var shipment = await _context.Shipments.FindAsync(request.ShipmentId);
            string? shipmentNo = shipment?.ShipmentNo;

            var pod = await _context.PodRecords.FirstOrDefaultAsync(p => p.ShipmentId == request.ShipmentId);
            if (pod == null)
            {
                pod = new PodRecord
                {
                    ShipmentId = request.ShipmentId,
                    ShipmentNo = shipmentNo,
                    DeliveryDate = request.DeliveryDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    ReceiverName = request.ReceiverName.Trim(),
                    ReceiverMobile = request.ReceiverMobile?.Trim(),
                    ReceiverAadharOrId = request.ReceiverAadharOrId?.Trim(),
                    DocumentUrl = request.DocumentUrl,
                    SignatureUrl = request.SignatureUrl,
                    Status = PodStatus.Uploaded,
                    Remarks = request.Remarks,
                    CreatedAt = DateTime.UtcNow
                };
                _context.PodRecords.Add(pod);
            }
            else
            {
                pod.DeliveryDate = request.DeliveryDate ?? pod.DeliveryDate;
                pod.ReceiverName = request.ReceiverName.Trim();
                pod.ReceiverMobile = request.ReceiverMobile?.Trim();
                pod.ReceiverAadharOrId = request.ReceiverAadharOrId?.Trim();
                pod.DocumentUrl = request.DocumentUrl ?? pod.DocumentUrl;
                pod.SignatureUrl = request.SignatureUrl ?? pod.SignatureUrl;
                pod.Status = PodStatus.Uploaded;
                pod.Remarks = request.Remarks ?? pod.Remarks;
                pod.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Uploaded POD for Shipment ID: {ShipmentId}", request.ShipmentId);
            return MapToDto(pod);
        }

        public async Task<PodRecordDto?> VerifyPodAsync(long id, VerifyPodRequest request, int? userId = null)
        {
            var pod = await _context.PodRecords.FirstOrDefaultAsync(p => p.Id == id);
            if (pod == null) return null;

            if (request.IsApproved)
            {
                pod.Status = PodStatus.Verified;
                pod.VerifiedByUserId = userId;
                pod.VerifiedAt = DateTime.UtcNow;
                pod.Remarks = request.Remarks ?? pod.Remarks;

                // Move shipment to Delivered
                var shipment = await _context.Shipments.FindAsync(pod.ShipmentId);
                if (shipment != null)
                {
                    shipment.Status = ShipmentStatus.Delivered;

                    _context.ShipmentStatusHistories.Add(new ShipmentStatusHistory
                    {
                        ShipmentId = shipment.Id,
                        FromStatus = ShipmentStatus.OutForDelivery,
                        ToStatus = ShipmentStatus.Delivered,
                        Location = shipment.ToLocation ?? "Destination Hub",
                        Remarks = $"Delivered to {pod.ReceiverName}. POD Verified.",
                        ChangedByUserId = userId,
                        ChangedAt = DateTime.UtcNow
                    });
                }
            }
            else
            {
                pod.Status = PodStatus.Rejected;
                pod.RejectionReason = request.RejectionReason;
                pod.Remarks = request.Remarks ?? pod.Remarks;
            }

            pod.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Verified POD ID: {Id} -> Status: {Status}", pod.Id, pod.Status);
            return MapToDto(pod);
        }

        public async Task<bool> DeletePodAsync(long id)
        {
            var pod = await _context.PodRecords.FirstOrDefaultAsync(p => p.Id == id);
            if (pod == null) return false;

            _context.PodRecords.Remove(pod);
            await _context.SaveChangesAsync();
            return true;
        }

        private static PodRecordDto MapToDto(PodRecord p)
        {
            return new PodRecordDto
            {
                Id = p.Id,
                TenantId = p.TenantId,
                ShipmentId = p.ShipmentId,
                ShipmentNo = p.ShipmentNo,
                DeliveryDate = p.DeliveryDate,
                ReceiverName = p.ReceiverName,
                ReceiverMobile = p.ReceiverMobile,
                ReceiverAadharOrId = p.ReceiverAadharOrId,
                DocumentUrl = p.DocumentUrl,
                SignatureUrl = p.SignatureUrl,
                Status = p.Status,
                RejectionReason = p.RejectionReason,
                Remarks = p.Remarks,
                VerifiedByUserId = p.VerifiedByUserId,
                VerifiedAt = p.VerifiedAt,
                CreatedAt = p.CreatedAt
            };
        }
    }
}
