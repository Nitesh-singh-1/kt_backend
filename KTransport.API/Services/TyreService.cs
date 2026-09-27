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
    public class TyreService : ITyreService
    {
        private readonly KTransportDbContext _context;
        private readonly ILogger<TyreService> _logger;

        public TyreService(KTransportDbContext context, ILogger<TyreService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<TyreDto>> GetAllAsync(string? search = null, TyreStatus? status = null, long? vehicleId = null)
        {
            var query = _context.Tyres.AsNoTracking().Where(t => t.IsActive);

            if (status.HasValue)
                query = query.Where(t => t.Status == status.Value);

            if (vehicleId.HasValue)
                query = query.Where(t => t.VehicleId == vehicleId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(t =>
                    t.SerialNo.ToLower().Contains(s) ||
                    (t.Brand != null && t.Brand.ToLower().Contains(s)) ||
                    (t.VehicleNo != null && t.VehicleNo.ToLower().Contains(s)));
            }

            var list = await query.OrderByDescending(t => t.Id).ToListAsync();
            return list.Select(Map).ToList();
        }

        public async Task<TyreDto?> GetByIdAsync(long id)
        {
            var t = await _context.Tyres.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return t == null ? null : Map(t);
        }

        public async Task<TyreDto> CreateAsync(CreateTyreRequest request)
        {
            var tyre = new Tyre
            {
                SerialNo = request.SerialNo.Trim().ToUpperInvariant(),
                Brand = request.Brand?.Trim(),
                Size = request.Size?.Trim(),
                VehicleId = request.VehicleId,
                VehicleNo = request.VehicleNo?.Trim().ToUpperInvariant(),
                Position = request.Position?.Trim(),
                PurchaseDate = request.PurchaseDate,
                PurchaseCost = request.PurchaseCost,
                PurchaseOdometer = request.PurchaseOdometer,
                CurrentOdometer = request.CurrentOdometer,
                RetreadCount = request.RetreadCount,
                Status = request.Status,
                Remarks = request.Remarks?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Tyres.Add(tyre);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created tyre {SerialNo} (ID: {Id})", tyre.SerialNo, tyre.Id);
            return Map(tyre);
        }

        public async Task<TyreDto?> UpdateAsync(long id, UpdateTyreRequest request)
        {
            var t = await _context.Tyres.FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return null;

            if (!string.IsNullOrWhiteSpace(request.SerialNo)) t.SerialNo = request.SerialNo.Trim().ToUpperInvariant();
            if (request.Brand != null) t.Brand = request.Brand.Trim();
            if (request.Size != null) t.Size = request.Size.Trim();
            if (request.VehicleId.HasValue) t.VehicleId = request.VehicleId;
            if (request.VehicleNo != null) t.VehicleNo = request.VehicleNo.Trim().ToUpperInvariant();
            if (request.Position != null) t.Position = request.Position.Trim();
            if (request.PurchaseDate.HasValue) t.PurchaseDate = request.PurchaseDate;
            if (request.PurchaseCost.HasValue) t.PurchaseCost = request.PurchaseCost.Value;
            if (request.PurchaseOdometer.HasValue) t.PurchaseOdometer = request.PurchaseOdometer.Value;
            if (request.CurrentOdometer.HasValue) t.CurrentOdometer = request.CurrentOdometer.Value;
            if (request.RetreadCount.HasValue) t.RetreadCount = request.RetreadCount.Value;
            if (request.Status.HasValue) t.Status = request.Status.Value;
            if (request.DisposalDate.HasValue) t.DisposalDate = request.DisposalDate;
            if (request.Remarks != null) t.Remarks = request.Remarks.Trim();
            if (request.IsActive.HasValue) t.IsActive = request.IsActive.Value;

            t.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Map(t);
        }

        public async Task<bool> DeleteAsync(long id)
        {
            var t = await _context.Tyres.FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return false;

            t.IsActive = false;
            t.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        private static TyreDto Map(Tyre t) => new()
        {
            Id = t.Id,
            TenantId = t.TenantId,
            SerialNo = t.SerialNo,
            Brand = t.Brand,
            Size = t.Size,
            VehicleId = t.VehicleId,
            VehicleNo = t.VehicleNo,
            Position = t.Position,
            PurchaseDate = t.PurchaseDate,
            PurchaseCost = t.PurchaseCost,
            PurchaseOdometer = t.PurchaseOdometer,
            CurrentOdometer = t.CurrentOdometer,
            RetreadCount = t.RetreadCount,
            Status = t.Status,
            DisposalDate = t.DisposalDate,
            Remarks = t.Remarks,
            IsActive = t.IsActive,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        };
    }
}
