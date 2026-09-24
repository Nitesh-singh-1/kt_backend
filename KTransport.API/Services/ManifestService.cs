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
    public class ManifestService : IManifestService
    {
        private readonly ILogger<ManifestService> _logger;
        private readonly KTransportDbContext _context;
        private readonly INumberingSequenceService _sequenceService;
        private readonly ITenantContext _tenantContext;

        public ManifestService(
            ILogger<ManifestService> logger,
            KTransportDbContext context,
            INumberingSequenceService sequenceService,
            ITenantContext tenantContext)
        {
            _logger = logger;
            _context = context;
            _sequenceService = sequenceService;
            _tenantContext = tenantContext;
        }

        public async Task<ManifestResponse> CreateManifestAsync(CreateManifestRequest request, int userId)
        {
            try
            {
                var tenantId = _tenantContext.CurrentTenantId;

                var manifestNo = string.IsNullOrWhiteSpace(request.ManifestNo)
                    ? await _sequenceService.GetNextNumberAsync("MANIFEST", "MN")
                    : request.ManifestNo.Trim();

                var exists = await _context.Manifests
                    .AnyAsync(m => m.ManifestNo.ToUpper() == manifestNo.ToUpper());

                if (exists)
                {
                    return new ManifestResponse
                    {
                        Success = false,
                        Message = $"Manifest / Loading Sheet Number '{manifestNo}' already exists."
                    };
                }

                var manifest = new Manifest
                {
                    TenantId = tenantId,
                    ManifestNo = manifestNo,
                    ManifestDate = request.ManifestDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    OriginHubId = request.OriginHubId,
                    DestinationHubId = request.DestinationHubId,
                    TripId = request.TripId,
                    ConsolidatedEwayBillNo = request.ConsolidatedEwayBillNo,
                    ConsolidatedEwayBillDate = string.IsNullOrWhiteSpace(request.ConsolidatedEwayBillNo) ? null : DateTime.UtcNow,
                    SealNo = request.SealNo,
                    LoadingSupervisorName = request.LoadingSupervisorName,
                    Remarks = request.Remarks,
                    Status = ManifestStatus.Loaded,
                    CreatedBy = userId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                // Add Items
                if (request.Items != null && request.Items.Any())
                {
                    var shipmentIds = request.Items.Select(i => i.ShipmentId).Distinct().ToList();
                    var shipments = await _context.Shipments
                        .Include(s => s.Items)
                        .Where(s => shipmentIds.Contains(s.Id) && s.IsActive)
                        .ToDictionaryAsync(s => s.Id);

                    int totalPackages = 0;
                    decimal totalWeight = 0;

                    foreach (var itemReq in request.Items)
                    {
                        if (!shipments.TryGetValue(itemReq.ShipmentId, out var shipment))
                        {
                            continue;
                        }

                        int loadedPkgs = itemReq.LoadedPackages.HasValue && itemReq.LoadedPackages.Value > 0
                            ? itemReq.LoadedPackages.Value
                            : (shipment.Items.Sum(i => i.Quantity) > 0 ? shipment.Items.Sum(i => i.Quantity) : 1);

                        decimal loadedWt = itemReq.LoadedWeightKg.HasValue && itemReq.LoadedWeightKg.Value > 0
                            ? itemReq.LoadedWeightKg.Value
                            : shipment.Items.Sum(i => i.Weight);

                        totalPackages += loadedPkgs;
                        totalWeight += loadedWt;

                        manifest.Items.Add(new ManifestItem
                        {
                            TenantId = tenantId,
                            ShipmentId = shipment.Id,
                            TargetDestinationHubId = itemReq.TargetDestinationHubId ?? shipment.DestinationHubId,
                            LoadedPackages = loadedPkgs,
                            LoadedWeightKg = loadedWt,
                            UnloadingStatus = ManifestItemStatus.Loaded,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        });

                        // Transition shipment status to Manifested
                        if (shipment.Status == ShipmentStatus.Booked || shipment.Status == ShipmentStatus.Draft)
                        {
                            shipment.Status = ShipmentStatus.Manifested;
                            shipment.StatusHistory.Add(new ShipmentStatusHistory
                            {
                                TenantId = tenantId,
                                FromStatus = ShipmentStatus.Booked,
                                ToStatus = ShipmentStatus.Manifested,
                                Location = manifest.OriginHubId?.ToString(),
                                Remarks = $"Loaded on Manifest {manifestNo}",
                                ChangedByUserId = userId,
                                ChangedAt = DateTime.UtcNow
                            });
                        }
                    }

                    manifest.TotalConsignments = manifest.Items.Count;
                    manifest.TotalPackages = totalPackages;
                    manifest.TotalWeightKg = totalWeight;
                }

                _context.Manifests.Add(manifest);
                await _context.SaveChangesAsync();

                var created = await GetManifestEntityByIdAsync(manifest.Id) ?? manifest;
                return new ManifestResponse
                {
                    Success = true,
                    Message = "Manifest / Loading Sheet created successfully.",
                    Data = MapToDto(created)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating manifest");
                return new ManifestResponse
                {
                    Success = false,
                    Message = "An unexpected error occurred while creating manifest."
                };
            }
        }

        public async Task<ManifestResponse> UpdateManifestAsync(long id, UpdateManifestRequest request, int userId)
        {
            try
            {
                var manifest = await _context.Manifests
                    .Include(m => m.Items)
                    .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);

                if (manifest == null)
                {
                    return new ManifestResponse { Success = false, Message = "Manifest not found." };
                }

                if (request.ManifestDate.HasValue) manifest.ManifestDate = request.ManifestDate.Value;
                manifest.OriginHubId = request.OriginHubId;
                manifest.DestinationHubId = request.DestinationHubId;
                manifest.TripId = request.TripId;
                manifest.ConsolidatedEwayBillNo = request.ConsolidatedEwayBillNo;
                manifest.SealNo = request.SealNo;
                manifest.LoadingSupervisorName = request.LoadingSupervisorName;
                manifest.Remarks = request.Remarks;
                if (request.Status.HasValue) manifest.Status = request.Status.Value;
                manifest.UpdatedBy = userId;
                manifest.UpdatedAt = DateTime.UtcNow;

                // Sync items if provided
                if (request.Items != null)
                {
                    _context.ManifestItems.RemoveRange(manifest.Items);

                    var shipmentIds = request.Items.Select(i => i.ShipmentId).Distinct().ToList();
                    var shipments = await _context.Shipments
                        .Include(s => s.Items)
                        .Where(s => shipmentIds.Contains(s.Id) && s.IsActive)
                        .ToDictionaryAsync(s => s.Id);

                    int totalPackages = 0;
                    decimal totalWeight = 0;

                    foreach (var itemReq in request.Items)
                    {
                        if (!shipments.TryGetValue(itemReq.ShipmentId, out var shipment)) continue;

                        int loadedPkgs = itemReq.LoadedPackages.HasValue && itemReq.LoadedPackages.Value > 0
                            ? itemReq.LoadedPackages.Value
                            : (shipment.Items.Sum(i => i.Quantity) > 0 ? shipment.Items.Sum(i => i.Quantity) : 1);

                        decimal loadedWt = itemReq.LoadedWeightKg.HasValue && itemReq.LoadedWeightKg.Value > 0
                            ? itemReq.LoadedWeightKg.Value
                            : shipment.Items.Sum(i => i.Weight);

                        totalPackages += loadedPkgs;
                        totalWeight += loadedWt;

                        manifest.Items.Add(new ManifestItem
                        {
                            TenantId = manifest.TenantId,
                            ManifestId = manifest.Id,
                            ShipmentId = shipment.Id,
                            TargetDestinationHubId = itemReq.TargetDestinationHubId ?? shipment.DestinationHubId,
                            LoadedPackages = loadedPkgs,
                            LoadedWeightKg = loadedWt,
                            UnloadingStatus = ManifestItemStatus.Loaded,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    manifest.TotalConsignments = manifest.Items.Count;
                    manifest.TotalPackages = totalPackages;
                    manifest.TotalWeightKg = totalWeight;
                }

                await _context.SaveChangesAsync();

                var updated = await GetManifestEntityByIdAsync(manifest.Id) ?? manifest;
                return new ManifestResponse
                {
                    Success = true,
                    Message = "Manifest updated successfully.",
                    Data = MapToDto(updated)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating manifest {Id}", id);
                return new ManifestResponse { Success = false, Message = "An error occurred while updating manifest." };
            }
        }

        public async Task<ManifestResponse> GetManifestByIdAsync(long id)
        {
            var manifest = await GetManifestEntityByIdAsync(id);
            if (manifest == null) return new ManifestResponse { Success = false, Message = "Manifest not found." };
            return new ManifestResponse { Success = true, Data = MapToDto(manifest) };
        }

        public async Task<ManifestResponse> GetManifestByNoAsync(string manifestNo)
        {
            var manifest = await _context.Manifests
                .Include(m => m.OriginHub)
                .Include(m => m.DestinationHub)
                .Include(m => m.Trip)
                .Include(m => m.CreatedByNavigation)
                .Include(m => m.Items)
                    .ThenInclude(i => i.Shipment)
                .Include(m => m.Items)
                    .ThenInclude(i => i.TargetDestinationHub)
                .Include(m => m.Items)
                    .ThenInclude(i => i.UnloadedAtHub)
                .FirstOrDefaultAsync(m => m.ManifestNo == manifestNo && m.IsActive);

            if (manifest == null) return new ManifestResponse { Success = false, Message = "Manifest not found." };
            return new ManifestResponse { Success = true, Data = MapToDto(manifest) };
        }

        public async Task<ManifestListResponse> GetManifestsAsync(ManifestFilterRequest filter)
        {
            var query = _context.Manifests
                .Where(m => m.IsActive)
                .AsQueryable();

            if (filter.Status.HasValue)
            {
                query = query.Where(m => m.Status == filter.Status.Value);
            }

            if (filter.OriginHubId.HasValue)
            {
                query = query.Where(m => m.OriginHubId == filter.OriginHubId.Value);
            }

            if (filter.DestinationHubId.HasValue)
            {
                query = query.Where(m => m.DestinationHubId == filter.DestinationHubId.Value);
            }

            if (filter.TripId.HasValue)
            {
                query = query.Where(m => m.TripId == filter.TripId.Value);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(m => m.ManifestDate >= filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(m => m.ManifestDate <= filter.ToDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim().ToLower();
                query = query.Where(m =>
                    m.ManifestNo.ToLower().Contains(term) ||
                    (m.ConsolidatedEwayBillNo != null && m.ConsolidatedEwayBillNo.ToLower().Contains(term)) ||
                    (m.SealNo != null && m.SealNo.ToLower().Contains(term)) ||
                    (m.Trip != null && m.Trip.VehicleNo != null && m.Trip.VehicleNo.ToLower().Contains(term)));
            }

            var total = await query.CountAsync();
            var list = await query
                .OrderByDescending(m => m.CreatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Include(m => m.OriginHub)
                .Include(m => m.DestinationHub)
                .Include(m => m.Trip)
                .Include(m => m.CreatedByNavigation)
                .Include(m => m.Items)
                    .ThenInclude(i => i.Shipment)
                .ToListAsync();

            return new ManifestListResponse
            {
                Success = true,
                TotalCount = total,
                Data = list.Select(MapToDto).Where(x => x != null).Select(x => x!).ToList()
            };
        }

        public async Task<ManifestResponse> AssignTripAsync(long manifestId, long tripId, int userId)
        {
            var manifest = await _context.Manifests
                .Include(m => m.Items)
                    .ThenInclude(i => i.Shipment)
                .FirstOrDefaultAsync(m => m.Id == manifestId && m.IsActive);

            if (manifest == null) return new ManifestResponse { Success = false, Message = "Manifest not found." };

            var trip = await _context.Trips.FindAsync(tripId);
            if (trip == null) return new ManifestResponse { Success = false, Message = "Trip not found." };

            manifest.TripId = tripId;
            manifest.Status = ManifestStatus.Dispatched;
            manifest.UpdatedBy = userId;
            manifest.UpdatedAt = DateTime.UtcNow;

            // Update shipments to InTransit and assign truck number
            foreach (var item in manifest.Items)
            {
                if (item.Shipment != null)
                {
                    item.Shipment.Status = ShipmentStatus.InTransit;
                    item.Shipment.TruckNo = trip.VehicleNo;
                    item.Shipment.StatusHistory.Add(new ShipmentStatusHistory
                    {
                        TenantId = manifest.TenantId,
                        FromStatus = ShipmentStatus.Manifested,
                        ToStatus = ShipmentStatus.InTransit,
                        Location = trip.OriginLocationName ?? manifest.OriginHubId?.ToString(),
                        Remarks = $"Dispatched on Trip {trip.TripNo} (Vehicle: {trip.VehicleNo})",
                        ChangedByUserId = userId,
                        ChangedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();

            var updated = await GetManifestEntityByIdAsync(manifestId);
            return new ManifestResponse
            {
                Success = true,
                Message = $"Manifest assigned to Trip {trip.TripNo} successfully.",
                Data = MapToDto(updated)
            };
        }

        public async Task<ManifestResponse> DispatchManifestAsync(long manifestId, int userId)
        {
            var manifest = await _context.Manifests
                .Include(m => m.Items)
                    .ThenInclude(i => i.Shipment)
                .FirstOrDefaultAsync(m => m.Id == manifestId && m.IsActive);

            if (manifest == null) return new ManifestResponse { Success = false, Message = "Manifest not found." };

            manifest.Status = ManifestStatus.Dispatched;
            manifest.UpdatedBy = userId;
            manifest.UpdatedAt = DateTime.UtcNow;

            foreach (var item in manifest.Items)
            {
                if (item.Shipment != null && item.Shipment.Status != ShipmentStatus.InTransit)
                {
                    item.Shipment.Status = ShipmentStatus.InTransit;
                    item.Shipment.StatusHistory.Add(new ShipmentStatusHistory
                    {
                        TenantId = manifest.TenantId,
                        FromStatus = ShipmentStatus.Manifested,
                        ToStatus = ShipmentStatus.InTransit,
                        Remarks = $"Dispatched under Manifest {manifest.ManifestNo}",
                        ChangedByUserId = userId,
                        ChangedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();

            var updated = await GetManifestEntityByIdAsync(manifestId);
            return new ManifestResponse
            {
                Success = true,
                Message = "Manifest marked as Dispatched.",
                Data = MapToDto(updated)
            };
        }

        public async Task<ManifestResponse> UnloadManifestAsync(long manifestId, UnloadManifestRequest request, int userId)
        {
            var manifest = await _context.Manifests
                .Include(m => m.Items)
                    .ThenInclude(i => i.Shipment)
                .FirstOrDefaultAsync(m => m.Id == manifestId && m.IsActive);

            if (manifest == null) return new ManifestResponse { Success = false, Message = "Manifest not found." };

            manifest.Status = ManifestStatus.Unloaded;
            manifest.UpdatedBy = userId;
            manifest.UpdatedAt = DateTime.UtcNow;

            var hub = await _context.Locations.FindAsync(request.UnloadedAtHubId);
            var hubName = hub?.Name ?? request.UnloadedAtHubId.ToString();

            var discrepancyMap = request.Items?.ToDictionary(i => i.ManifestItemId) ?? new Dictionary<long, UnloadManifestItemDiscrepancyRequest>();

            foreach (var item in manifest.Items)
            {
                item.UnloadedAtHubId = request.UnloadedAtHubId;
                item.UnloadedDate = request.UnloadedDate ?? DateTime.UtcNow;

                if (discrepancyMap.TryGetValue(item.Id, out var disc))
                {
                    item.UnloadingStatus = disc.UnloadingStatus;
                    item.ReceivedPackages = disc.ReceivedPackages;
                    item.ShortagePackages = disc.ShortagePackages;
                    item.DamagedPackages = disc.DamagedPackages;
                    item.DiscrepancyRemarks = disc.DiscrepancyRemarks;
                }
                else
                {
                    item.UnloadingStatus = ManifestItemStatus.ReceivedIntact;
                    item.ReceivedPackages = item.LoadedPackages;
                }

                // Update current hub of the shipment
                if (item.Shipment != null)
                {
                    item.Shipment.CurrentHubId = request.UnloadedAtHubId;

                    // If this is the final destination hub, mark as OutForDelivery or keep Inward
                    if (item.Shipment.DestinationHubId == request.UnloadedAtHubId)
                    {
                        item.Shipment.Status = ShipmentStatus.InTransit; // Ready for OFD
                    }

                    item.Shipment.StatusHistory.Add(new ShipmentStatusHistory
                    {
                        TenantId = manifest.TenantId,
                        FromStatus = item.Shipment.Status,
                        ToStatus = item.Shipment.Status,
                        Location = hubName,
                        Remarks = $"Unloaded at {hubName} from Manifest {manifest.ManifestNo}. Status: {item.UnloadingStatus}",
                        ChangedByUserId = userId,
                        ChangedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();

            var updated = await GetManifestEntityByIdAsync(manifestId);
            return new ManifestResponse
            {
                Success = true,
                Message = $"Manifest unloaded at {hubName} successfully.",
                Data = MapToDto(updated)
            };
        }

        public async Task<bool> DeleteManifestAsync(long id, int userId)
        {
            var manifest = await _context.Manifests
                .Include(m => m.Items)
                    .ThenInclude(i => i.Shipment)
                .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);

            if (manifest == null) return false;

            manifest.IsActive = false;
            manifest.Status = ManifestStatus.Cancelled;
            manifest.UpdatedBy = userId;
            manifest.UpdatedAt = DateTime.UtcNow;

            // Revert shipment statuses back to Booked if they were only Manifested
            foreach (var item in manifest.Items)
            {
                if (item.Shipment != null && item.Shipment.Status == ShipmentStatus.Manifested)
                {
                    item.Shipment.Status = ShipmentStatus.Booked;
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<ShipmentDto>> GetAvailableShipmentsForManifestAsync(long? originHubId, long? destinationHubId)
        {
            var query = _context.Shipments
                .Include(s => s.Items)
                .Include(s => s.ChargeItems)
                .Include(s => s.InvoiceReferences)
                .Where(s => s.IsActive && (s.Status == ShipmentStatus.Booked || s.Status == ShipmentStatus.Draft));

            if (originHubId.HasValue)
            {
                query = query.Where(s => s.OriginHubId == originHubId.Value || s.CurrentHubId == originHubId.Value);
            }

            if (destinationHubId.HasValue)
            {
                query = query.Where(s => s.DestinationHubId == destinationHubId.Value);
            }

            var shipments = await query.OrderBy(s => s.ShipmentDate).ToListAsync();
            return shipments.Select(s => new ShipmentDto
            {
                Id = s.Id,
                TenantId = s.TenantId,
                ShipmentNo = s.ShipmentNo,
                InvoiceNo = s.InvoiceNo,
                ShipmentDate = s.ShipmentDate,
                FromLocation = s.FromLocation,
                ToLocation = s.ToLocation,
                ConsignorName = s.ConsignorName,
                ConsigneeName = s.ConsigneeName,
                TotalFreight = s.TotalFreight,
                PaymentTerm = s.PaymentTerm,
                Status = s.Status,
                OriginHubId = s.OriginHubId,
                DestinationHubId = s.DestinationHubId,
                Items = (s.Items ?? Enumerable.Empty<ShipmentItem>()).Select(i => new ShipmentItemDto
                {
                    Id = i.Id,
                    Article = i.Article,
                    Weight = i.Weight,
                    Quantity = i.Quantity
                }).ToList()
            }).ToList();
        }

        private async Task<Manifest?> GetManifestEntityByIdAsync(long id)
        {
            return await _context.Manifests
                .Include(m => m.OriginHub)
                .Include(m => m.DestinationHub)
                .Include(m => m.Trip)
                .Include(m => m.CreatedByNavigation)
                .Include(m => m.Items)
                    .ThenInclude(i => i.Shipment)
                .Include(m => m.Items)
                    .ThenInclude(i => i.TargetDestinationHub)
                .Include(m => m.Items)
                    .ThenInclude(i => i.UnloadedAtHub)
                .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);
        }

        private static ManifestDto? MapToDto(Manifest? m)
        {
            if (m == null) return null;

            return new ManifestDto
            {
                Id = m.Id,
                TenantId = m.TenantId,
                ManifestNo = m.ManifestNo,
                ManifestDate = m.ManifestDate,
                OriginHubId = m.OriginHubId,
                OriginHubName = m.OriginHub?.Name,
                DestinationHubId = m.DestinationHubId,
                DestinationHubName = m.DestinationHub?.Name,
                TripId = m.TripId,
                TripNo = m.Trip?.TripNo,
                VehicleNo = m.Trip?.VehicleNo,
                DriverName = m.Trip?.DriverName,
                ConsolidatedEwayBillNo = m.ConsolidatedEwayBillNo,
                ConsolidatedEwayBillDate = m.ConsolidatedEwayBillDate,
                SealNo = m.SealNo,
                LoadingSupervisorName = m.LoadingSupervisorName,
                Remarks = m.Remarks,
                Status = m.Status,
                TotalConsignments = m.TotalConsignments,
                TotalPackages = m.TotalPackages,
                TotalWeightKg = m.TotalWeightKg,
                IsActive = m.IsActive,
                CreatedAt = m.CreatedAt,
                CreatedByName = m.CreatedByNavigation?.FullName ?? m.CreatedByNavigation?.Username,
                Items = (m.Items ?? Enumerable.Empty<ManifestItem>()).Where(i => i.IsActive).Select(i => new ManifestItemDto
                {
                    Id = i.Id,
                    ManifestId = i.ManifestId,
                    ShipmentId = i.ShipmentId,
                    ShipmentNo = i.Shipment?.ShipmentNo ?? i.ShipmentId.ToString(),
                    ConsignorName = i.Shipment?.ConsignorName,
                    ConsigneeName = i.Shipment?.ConsigneeName,
                    FromLocation = i.Shipment?.FromLocation,
                    ToLocation = i.Shipment?.ToLocation,
                    TargetDestinationHubId = i.TargetDestinationHubId,
                    TargetDestinationHubName = i.TargetDestinationHub?.Name,
                    LoadedPackages = i.LoadedPackages,
                    LoadedWeightKg = i.LoadedWeightKg,
                    UnloadingStatus = i.UnloadingStatus,
                    ReceivedPackages = i.ReceivedPackages,
                    ShortagePackages = i.ShortagePackages,
                    DamagedPackages = i.DamagedPackages,
                    UnloadedAtHubId = i.UnloadedAtHubId,
                    UnloadedAtHubName = i.UnloadedAtHub?.Name,
                    UnloadedDate = i.UnloadedDate,
                    DiscrepancyRemarks = i.DiscrepancyRemarks,
                    TotalFreight = i.Shipment?.TotalFreight ?? 0,
                    PaymentTerm = i.Shipment?.PaymentTerm ?? PaymentTerm.ToPay,
                    EwayBillNo = i.Shipment?.EwayBillNo
                }).ToList()
            };
        }
    }
}
