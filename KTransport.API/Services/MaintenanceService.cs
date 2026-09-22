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
    public class MaintenanceService : IMaintenanceService
    {
        private readonly KTransportDbContext _context;
        private readonly ILogger<MaintenanceService> _logger;

        public MaintenanceService(KTransportDbContext context, ILogger<MaintenanceService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<VehicleMaintenanceDto>> GetMaintenanceRecordsAsync(long? vehicleId = null, MaintenanceType? maintenanceType = null)
        {
            var query = _context.VehicleMaintenances.AsNoTracking().Where(m => m.IsActive);

            if (vehicleId.HasValue)
            {
                query = query.Where(m => m.VehicleId == vehicleId.Value);
            }

            if (maintenanceType.HasValue)
            {
                query = query.Where(m => m.MaintenanceType == maintenanceType.Value);
            }

            var list = await query.OrderByDescending(m => m.ServiceDate).ThenByDescending(m => m.Id).ToListAsync();
            return list.Select(MapToDto).ToList();
        }

        public async Task<VehicleMaintenanceDto?> GetMaintenanceByIdAsync(long id)
        {
            var m = await _context.VehicleMaintenances.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return m == null ? null : MapToDto(m);
        }

        public async Task<VehicleMaintenanceDto> CreateMaintenanceAsync(CreateMaintenanceRequest request)
        {
            string? vehicleNo = null;
            var v = await _context.Vehicles.FindAsync(request.VehicleId);
            if (v != null) vehicleNo = v.VehicleNo;

            var maintenance = new VehicleMaintenance
            {
                VehicleId = request.VehicleId,
                VehicleNo = vehicleNo,
                MaintenanceType = request.MaintenanceType,
                Cost = request.Cost,
                ServiceDate = request.ServiceDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                OdometerReading = request.OdometerReading,
                NextServiceDueKm = request.NextServiceDueKm,
                NextServiceDueDate = request.NextServiceDueDate,
                WorkshopName = request.WorkshopName?.Trim(),
                InvoiceNo = request.InvoiceNo?.Trim(),
                Description = request.Description?.Trim(),
                Remarks = request.Remarks?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.VehicleMaintenances.Add(maintenance);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Recorded maintenance for Vehicle {VehicleNo}, Cost: {Cost}", vehicleNo, maintenance.Cost);
            return MapToDto(maintenance);
        }

        public async Task<bool> DeleteMaintenanceAsync(long id)
        {
            var m = await _context.VehicleMaintenances.FirstOrDefaultAsync(x => x.Id == id);
            if (m == null) return false;

            m.IsActive = false;
            m.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        private static VehicleMaintenanceDto MapToDto(VehicleMaintenance m)
        {
            return new VehicleMaintenanceDto
            {
                Id = m.Id,
                TenantId = m.TenantId,
                VehicleId = m.VehicleId,
                VehicleNo = m.VehicleNo,
                MaintenanceType = m.MaintenanceType,
                Cost = m.Cost,
                ServiceDate = m.ServiceDate,
                OdometerReading = m.OdometerReading,
                NextServiceDueKm = m.NextServiceDueKm,
                NextServiceDueDate = m.NextServiceDueDate,
                WorkshopName = m.WorkshopName,
                InvoiceNo = m.InvoiceNo,
                Description = m.Description,
                Remarks = m.Remarks,
                IsActive = m.IsActive,
                CreatedAt = m.CreatedAt
            };
        }
    }
}
