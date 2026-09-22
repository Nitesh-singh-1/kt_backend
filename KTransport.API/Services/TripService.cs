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
    public class TripService : ITripService
    {
        private readonly KTransportDbContext _context;
        private readonly INumberingSequenceService _numberingService;
        private readonly ILogger<TripService> _logger;

        public TripService(
            KTransportDbContext context,
            INumberingSequenceService numberingService,
            ILogger<TripService> logger)
        {
            _context = context;
            _numberingService = numberingService;
            _logger = logger;
        }

        public async Task<List<TripDto>> GetTripsAsync(string? search = null, TripStatus? status = null)
        {
            var query = _context.Trips
                .Include(t => t.Shipments)
                .Include(t => t.Expenses)
                .AsNoTracking()
                .Where(t => t.IsActive);

            if (status.HasValue)
            {
                query = query.Where(t => t.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(t =>
                    t.TripNo.ToLower().Contains(s) ||
                    (t.VehicleNo != null && t.VehicleNo.ToLower().Contains(s)) ||
                    (t.DriverName != null && t.DriverName.ToLower().Contains(s)) ||
                    (t.OriginLocationName != null && t.OriginLocationName.ToLower().Contains(s)) ||
                    (t.DestinationLocationName != null && t.DestinationLocationName.ToLower().Contains(s))
                );
            }

            var trips = await query.OrderByDescending(t => t.TripDate).ThenByDescending(t => t.Id).ToListAsync();
            return trips.Select(MapToDto).ToList();
        }

        public async Task<List<TripLookupDto>> GetTripLookupAsync(string? query = null)
        {
            var dbQuery = _context.Trips.AsNoTracking().Where(t => t.IsActive);

            if (!string.IsNullOrWhiteSpace(query))
            {
                var s = query.Trim().ToLower();
                dbQuery = dbQuery.Where(t =>
                    t.TripNo.ToLower().Contains(s) ||
                    (t.VehicleNo != null && t.VehicleNo.ToLower().Contains(s))
                );
            }

            return await dbQuery
                .OrderByDescending(t => t.TripDate)
                .Take(50)
                .Select(t => new TripLookupDto
                {
                    Id = t.Id,
                    TripNo = t.TripNo,
                    TripDate = t.TripDate,
                    VehicleNo = t.VehicleNo,
                    DriverName = t.DriverName,
                    OriginLocationName = t.OriginLocationName,
                    DestinationLocationName = t.DestinationLocationName,
                    Status = t.Status
                })
                .ToListAsync();
        }

        public async Task<TripDto?> GetTripByIdAsync(long id)
        {
            var trip = await _context.Trips
                .Include(t => t.Shipments)
                .Include(t => t.Expenses)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            return trip == null ? null : MapToDto(trip);
        }

        public async Task<TripDto> CreateTripAsync(CreateTripRequest request, int? userId = null)
        {
            string tripNo = request.TripNo?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(tripNo))
            {
                tripNo = await _numberingService.GetNextNumberAsync("TRIP", "TRIP");
            }

            // Resolve vehicle and driver details
            string? vehicleNo = request.VehicleNo;
            if (request.VehicleId.HasValue)
            {
                var v = await _context.Vehicles.FindAsync(request.VehicleId.Value);
                if (v != null) vehicleNo = v.VehicleNo;
            }

            string? driverName = request.DriverName;
            string? driverMobile = request.DriverMobile;
            if (request.DriverId.HasValue)
            {
                var d = await _context.Drivers.FindAsync(request.DriverId.Value);
                if (d != null)
                {
                    driverName ??= d.Name;
                    driverMobile ??= d.Mobile;
                }
            }

            string? originName = request.OriginLocationName;
            if (request.OriginLocationId.HasValue)
            {
                var loc = await _context.Locations.FindAsync(request.OriginLocationId.Value);
                if (loc != null) originName ??= loc.Name;
            }

            string? destName = request.DestinationLocationName;
            if (request.DestinationLocationId.HasValue)
            {
                var loc = await _context.Locations.FindAsync(request.DestinationLocationId.Value);
                if (loc != null) destName ??= loc.Name;
            }

            var trip = new Trip
            {
                TripNo = tripNo,
                TripDate = request.TripDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                VehicleId = request.VehicleId,
                VehicleNo = vehicleNo,
                DriverId = request.DriverId,
                DriverName = driverName,
                DriverMobile = driverMobile,
                OriginLocationId = request.OriginLocationId,
                OriginLocationName = originName,
                DestinationLocationId = request.DestinationLocationId,
                DestinationLocationName = destName,
                Status = TripStatus.Draft,
                StartOdometer = request.StartOdometer,
                SealNo = request.SealNo,
                Remarks = request.Remarks,
                DriverAdvanceCash = request.DriverAdvanceCash,
                DriverAdvanceFuel = request.DriverAdvanceFuel,
                CreatedBy = userId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Trips.Add(trip);
            await _context.SaveChangesAsync();

            // Load initial shipments if provided
            if (request.ShipmentIdsToLoad != null && request.ShipmentIdsToLoad.Any())
            {
                await LoadShipmentsInternalAsync(trip, request.ShipmentIdsToLoad);
            }

            _logger.LogInformation("Created Trip {TripNo} (ID: {Id})", trip.TripNo, trip.Id);
            return (await GetTripByIdAsync(trip.Id))!;
        }

        public async Task<TripDto?> UpdateTripAsync(long id, UpdateTripRequest request, int? userId = null)
        {
            var trip = await _context.Trips.FirstOrDefaultAsync(t => t.Id == id);
            if (trip == null) return null;

            if (request.TripDate.HasValue) trip.TripDate = request.TripDate.Value;
            trip.VehicleId = request.VehicleId;
            trip.VehicleNo = request.VehicleNo ?? trip.VehicleNo;
            trip.DriverId = request.DriverId;
            trip.DriverName = request.DriverName ?? trip.DriverName;
            trip.DriverMobile = request.DriverMobile ?? trip.DriverMobile;
            trip.OriginLocationId = request.OriginLocationId;
            trip.OriginLocationName = request.OriginLocationName ?? trip.OriginLocationName;
            trip.DestinationLocationId = request.DestinationLocationId;
            trip.DestinationLocationName = request.DestinationLocationName ?? trip.DestinationLocationName;
            trip.StartOdometer = request.StartOdometer;
            trip.EndOdometer = request.EndOdometer;
            trip.SealNo = request.SealNo;
            trip.Remarks = request.Remarks;
            trip.DriverAdvanceCash = request.DriverAdvanceCash;
            trip.DriverAdvanceFuel = request.DriverAdvanceFuel;
            trip.UpdatedBy = userId;
            trip.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return (await GetTripByIdAsync(trip.Id))!;
        }

        public async Task<TripDto?> LoadShipmentsAsync(long tripId, LoadShipmentsRequest request, int? userId = null)
        {
            var trip = await _context.Trips.Include(t => t.Shipments).FirstOrDefaultAsync(t => t.Id == tripId);
            if (trip == null) return null;

            await LoadShipmentsInternalAsync(trip, request.ShipmentIds);
            return (await GetTripByIdAsync(trip.Id))!;
        }

        private async Task LoadShipmentsInternalAsync(Trip trip, List<long> shipmentIds)
        {
            var shipments = await _context.Shipments
                .Include(s => s.Items)
                .Where(s => shipmentIds.Contains(s.Id))
                .ToListAsync();

            foreach (var s in shipments)
            {
                if (trip.Shipments.Any(ts => ts.ShipmentId == s.Id)) continue;

                decimal weight = s.Items?.Sum(i => i.Weight) ?? 0;
                int packages = s.Items?.Sum(i => i.Quantity) ?? 1;

                trip.Shipments.Add(new TripShipment
                {
                    TripId = trip.Id,
                    ShipmentId = s.Id,
                    ShipmentNo = s.ShipmentNo,
                    LoadedWeight = weight,
                    LoadedPackages = packages,
                    FreightAmount = s.GrandTotal,
                    LoadedAt = DateTime.UtcNow
                });

                // Update shipment status to Manifested
                s.Status = ShipmentStatus.Manifested;
                s.TruckNo = trip.VehicleNo;
            }

            trip.TotalWeightTons = trip.Shipments.Sum(x => x.LoadedWeight) / 1000m;
            trip.TotalPackages = trip.Shipments.Sum(x => x.LoadedPackages);
            trip.TotalFreightRevenue = trip.Shipments.Sum(x => x.FreightAmount);
            trip.Status = TripStatus.Loading;

            await _context.SaveChangesAsync();
        }

        public async Task<TripDto?> RemoveShipmentAsync(long tripId, long shipmentId, int? userId = null)
        {
            var trip = await _context.Trips.Include(t => t.Shipments).FirstOrDefaultAsync(t => t.Id == tripId);
            if (trip == null) return null;

            var tripShipment = trip.Shipments.FirstOrDefault(ts => ts.ShipmentId == shipmentId);
            if (tripShipment != null)
            {
                _context.TripShipments.Remove(tripShipment);

                var s = await _context.Shipments.FindAsync(shipmentId);
                if (s != null) s.Status = ShipmentStatus.Booked;

                trip.TotalWeightTons = trip.Shipments.Where(x => x.ShipmentId != shipmentId).Sum(x => x.LoadedWeight) / 1000m;
                trip.TotalPackages = trip.Shipments.Where(x => x.ShipmentId != shipmentId).Sum(x => x.LoadedPackages);
                trip.TotalFreightRevenue = trip.Shipments.Where(x => x.ShipmentId != shipmentId).Sum(x => x.FreightAmount);

                await _context.SaveChangesAsync();
            }

            return (await GetTripByIdAsync(trip.Id))!;
        }

        public async Task<TripDto?> DispatchTripAsync(long tripId, int? userId = null)
        {
            var trip = await _context.Trips.Include(t => t.Shipments).FirstOrDefaultAsync(t => t.Id == tripId);
            if (trip == null) return null;

            trip.Status = TripStatus.Dispatched;
            trip.DepartureTime = DateTime.UtcNow;
            trip.UpdatedAt = DateTime.UtcNow;

            // Move all linked shipments to InTransit and log status history
            var shipmentIds = trip.Shipments.Select(ts => ts.ShipmentId).ToList();
            var shipments = await _context.Shipments.Where(s => shipmentIds.Contains(s.Id)).ToListAsync();

            foreach (var s in shipments)
            {
                s.Status = ShipmentStatus.InTransit;
                _context.ShipmentStatusHistories.Add(new ShipmentStatusHistory
                {
                    ShipmentId = s.Id,
                    FromStatus = ShipmentStatus.Manifested,
                    ToStatus = ShipmentStatus.InTransit,
                    Location = trip.OriginLocationName ?? "Dispatch Hub",
                    Remarks = $"Dispatched on Trip {trip.TripNo} via Truck {trip.VehicleNo}",
                    ChangedByUserId = userId,
                    ChangedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Dispatched Trip {TripNo}", trip.TripNo);
            return (await GetTripByIdAsync(trip.Id))!;
        }

        public async Task<TripDto?> ArriveTripAsync(long tripId, int? userId = null)
        {
            var trip = await _context.Trips.Include(t => t.Shipments).FirstOrDefaultAsync(t => t.Id == tripId);
            if (trip == null) return null;

            trip.Status = TripStatus.Arrived;
            trip.ArrivalTime = DateTime.UtcNow;
            trip.UpdatedAt = DateTime.UtcNow;

            var shipmentIds = trip.Shipments.Select(ts => ts.ShipmentId).ToList();
            var shipments = await _context.Shipments.Where(s => shipmentIds.Contains(s.Id)).ToListAsync();

            foreach (var s in shipments)
            {
                s.Status = ShipmentStatus.OutForDelivery;
                _context.ShipmentStatusHistories.Add(new ShipmentStatusHistory
                {
                    ShipmentId = s.Id,
                    FromStatus = ShipmentStatus.InTransit,
                    ToStatus = ShipmentStatus.OutForDelivery,
                    Location = trip.DestinationLocationName ?? "Destination Hub",
                    Remarks = $"Arrived on Trip {trip.TripNo}. Out for Delivery.",
                    ChangedByUserId = userId,
                    ChangedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Trip {TripNo} arrived at destination", trip.TripNo);
            return (await GetTripByIdAsync(trip.Id))!;
        }

        public async Task<TripDto?> CompleteTripAsync(long tripId, decimal endOdometer, int? userId = null)
        {
            var trip = await _context.Trips.Include(t => t.Expenses).FirstOrDefaultAsync(t => t.Id == tripId);
            if (trip == null) return null;

            trip.Status = TripStatus.Completed;
            trip.EndOdometer = endOdometer;
            trip.TotalExpenses = trip.Expenses.Sum(e => e.Amount);
            trip.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Completed Trip {TripNo}", trip.TripNo);
            return (await GetTripByIdAsync(trip.Id))!;
        }

        public async Task<TripDto?> CancelTripAsync(long tripId, string? reason = null, int? userId = null)
        {
            var trip = await _context.Trips.Include(t => t.Shipments).FirstOrDefaultAsync(t => t.Id == tripId);
            if (trip == null) return null;

            trip.Status = TripStatus.Cancelled;
            trip.Remarks = $"{trip.Remarks} | Cancelled: {reason}".Trim();
            trip.UpdatedAt = DateTime.UtcNow;

            var shipmentIds = trip.Shipments.Select(ts => ts.ShipmentId).ToList();
            var shipments = await _context.Shipments.Where(s => shipmentIds.Contains(s.Id)).ToListAsync();
            foreach (var s in shipments)
            {
                s.Status = ShipmentStatus.Booked;
            }

            await _context.SaveChangesAsync();
            return (await GetTripByIdAsync(trip.Id))!;
        }

        public async Task<TripExpenseDto> AddTripExpenseAsync(long tripId, AddTripExpenseRequest request, int? userId = null)
        {
            var expense = new TripExpense
            {
                TripId = tripId,
                ExpenseType = request.ExpenseType,
                Amount = request.Amount,
                ReceiptNo = request.ReceiptNo,
                PaymentMode = request.PaymentMode,
                PaidTo = request.PaidTo,
                Remarks = request.Remarks,
                ExpenseDate = request.ExpenseDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                CreatedAt = DateTime.UtcNow
            };

            _context.TripExpenses.Add(expense);

            var trip = await _context.Trips.FindAsync(tripId);
            if (trip != null)
            {
                trip.TotalExpenses += request.Amount;
            }

            await _context.SaveChangesAsync();

            return new TripExpenseDto
            {
                Id = expense.Id,
                TripId = expense.TripId,
                ExpenseType = expense.ExpenseType,
                Amount = expense.Amount,
                ReceiptNo = expense.ReceiptNo,
                PaymentMode = expense.PaymentMode,
                PaidTo = expense.PaidTo,
                Remarks = expense.Remarks,
                ExpenseDate = expense.ExpenseDate,
                CreatedAt = expense.CreatedAt
            };
        }

        public async Task<bool> DeleteTripExpenseAsync(long tripId, long expenseId)
        {
            var expense = await _context.TripExpenses.FirstOrDefaultAsync(e => e.Id == expenseId && e.TripId == tripId);
            if (expense == null) return false;

            var trip = await _context.Trips.FindAsync(tripId);
            if (trip != null)
            {
                trip.TotalExpenses = Math.Max(0, trip.TotalExpenses - expense.Amount);
            }

            _context.TripExpenses.Remove(expense);
            await _context.SaveChangesAsync();
            return true;
        }

        private static TripDto MapToDto(Trip t)
        {
            return new TripDto
            {
                Id = t.Id,
                TenantId = t.TenantId,
                TripNo = t.TripNo,
                TripDate = t.TripDate,
                VehicleId = t.VehicleId,
                VehicleNo = t.VehicleNo,
                DriverId = t.DriverId,
                DriverName = t.DriverName,
                DriverMobile = t.DriverMobile,
                OriginLocationId = t.OriginLocationId,
                OriginLocationName = t.OriginLocationName,
                DestinationLocationId = t.DestinationLocationId,
                DestinationLocationName = t.DestinationLocationName,
                Status = t.Status,
                DepartureTime = t.DepartureTime,
                ArrivalTime = t.ArrivalTime,
                StartOdometer = t.StartOdometer,
                EndOdometer = t.EndOdometer,
                SealNo = t.SealNo,
                Remarks = t.Remarks,
                TotalWeightTons = t.TotalWeightTons,
                TotalPackages = t.TotalPackages,
                TotalFreightRevenue = t.TotalFreightRevenue,
                DriverAdvanceCash = t.DriverAdvanceCash,
                DriverAdvanceFuel = t.DriverAdvanceFuel,
                TotalExpenses = t.TotalExpenses,
                IsActive = t.IsActive,
                CreatedAt = t.CreatedAt,
                Shipments = t.Shipments.Select(s => new TripShipmentDto
                {
                    Id = s.Id,
                    TripId = s.TripId,
                    ShipmentId = s.ShipmentId,
                    ShipmentNo = s.ShipmentNo,
                    LoadedWeight = s.LoadedWeight,
                    LoadedPackages = s.LoadedPackages,
                    FreightAmount = s.FreightAmount,
                    LoadedAt = s.LoadedAt,
                    UnloadedAt = s.UnloadedAt,
                    Remarks = s.Remarks
                }).ToList(),
                Expenses = t.Expenses.Select(e => new TripExpenseDto
                {
                    Id = e.Id,
                    TripId = e.TripId,
                    ExpenseType = e.ExpenseType,
                    Amount = e.Amount,
                    ReceiptNo = e.ReceiptNo,
                    PaymentMode = e.PaymentMode,
                    PaidTo = e.PaidTo,
                    Remarks = e.Remarks,
                    ExpenseDate = e.ExpenseDate,
                    CreatedAt = e.CreatedAt
                }).ToList()
            };
        }
    }
}
