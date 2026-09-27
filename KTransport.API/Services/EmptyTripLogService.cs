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
    public class EmptyTripLogService : IEmptyTripLogService
    {
        private readonly KTransportDbContext _context;
        private readonly ILogger<EmptyTripLogService> _logger;

        public EmptyTripLogService(KTransportDbContext context, ILogger<EmptyTripLogService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<EmptyTripLogDto>> GetAllAsync(string? search = null)
        {
            var query = _context.EmptyTripLogs.AsNoTracking().Where(e => e.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(e =>
                    (e.VehicleNo != null && e.VehicleNo.ToLower().Contains(s)) ||
                    (e.DriverName != null && e.DriverName.ToLower().Contains(s)) ||
                    (e.FromLocation != null && e.FromLocation.ToLower().Contains(s)) ||
                    (e.ToLocation != null && e.ToLocation.ToLower().Contains(s)));
            }

            var list = await query.OrderByDescending(e => e.TripDate).ThenByDescending(e => e.Id).ToListAsync();
            return list.Select(Map).ToList();
        }

        public async Task<EmptyTripLogDto?> GetByIdAsync(long id)
        {
            var e = await _context.EmptyTripLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return e == null ? null : Map(e);
        }

        public async Task<EmptyTripLogDto> CreateAsync(CreateEmptyTripLogRequest request)
        {
            var log = new EmptyTripLog
            {
                VehicleId = request.VehicleId,
                VehicleNo = request.VehicleNo.Trim().ToUpperInvariant(),
                DriverName = request.DriverName?.Trim(),
                FromLocation = request.FromLocation?.Trim(),
                ToLocation = request.ToLocation?.Trim(),
                TripDate = request.TripDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                DistanceKm = request.DistanceKm,
                FuelCost = request.FuelCost,
                Reason = request.Reason?.Trim(),
                Remarks = request.Remarks?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.EmptyTripLogs.Add(log);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Logged empty trip for {Vehicle} (ID: {Id})", log.VehicleNo, log.Id);
            return Map(log);
        }

        public async Task<EmptyTripLogDto?> UpdateAsync(long id, UpdateEmptyTripLogRequest request)
        {
            var e = await _context.EmptyTripLogs.FirstOrDefaultAsync(x => x.Id == id);
            if (e == null) return null;

            if (request.VehicleId.HasValue) e.VehicleId = request.VehicleId;
            if (!string.IsNullOrWhiteSpace(request.VehicleNo)) e.VehicleNo = request.VehicleNo.Trim().ToUpperInvariant();
            if (request.DriverName != null) e.DriverName = request.DriverName.Trim();
            if (request.FromLocation != null) e.FromLocation = request.FromLocation.Trim();
            if (request.ToLocation != null) e.ToLocation = request.ToLocation.Trim();
            if (request.TripDate.HasValue) e.TripDate = request.TripDate.Value;
            if (request.DistanceKm.HasValue) e.DistanceKm = request.DistanceKm.Value;
            if (request.FuelCost.HasValue) e.FuelCost = request.FuelCost.Value;
            if (request.Reason != null) e.Reason = request.Reason.Trim();
            if (request.Remarks != null) e.Remarks = request.Remarks.Trim();
            if (request.IsActive.HasValue) e.IsActive = request.IsActive.Value;

            e.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Map(e);
        }

        public async Task<bool> DeleteAsync(long id)
        {
            var e = await _context.EmptyTripLogs.FirstOrDefaultAsync(x => x.Id == id);
            if (e == null) return false;

            e.IsActive = false;
            e.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        private static EmptyTripLogDto Map(EmptyTripLog e) => new()
        {
            Id = e.Id,
            TenantId = e.TenantId,
            VehicleId = e.VehicleId,
            VehicleNo = e.VehicleNo,
            DriverName = e.DriverName,
            FromLocation = e.FromLocation,
            ToLocation = e.ToLocation,
            TripDate = e.TripDate,
            DistanceKm = e.DistanceKm,
            FuelCost = e.FuelCost,
            Reason = e.Reason,
            Remarks = e.Remarks,
            IsActive = e.IsActive,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        };
    }
}
