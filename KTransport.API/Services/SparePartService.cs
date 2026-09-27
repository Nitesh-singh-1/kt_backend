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
    public class SparePartService : ISparePartService
    {
        private readonly KTransportDbContext _context;
        private readonly ILogger<SparePartService> _logger;

        public SparePartService(KTransportDbContext context, ILogger<SparePartService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<SparePartDto>> GetAllAsync(string? search = null, string? category = null, bool lowStockOnly = false)
        {
            var query = _context.SpareParts.AsNoTracking().Where(p => p.IsActive);

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(p => p.Category == category);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(p =>
                    p.PartName.ToLower().Contains(s) ||
                    (p.PartNo != null && p.PartNo.ToLower().Contains(s)) ||
                    (p.Supplier != null && p.Supplier.ToLower().Contains(s)));
            }

            if (lowStockOnly)
                query = query.Where(p => p.ReorderLevel > 0 && p.StockQuantity <= p.ReorderLevel);

            var list = await query.OrderBy(p => p.PartName).ToListAsync();
            return list.Select(Map).ToList();
        }

        public async Task<SparePartDto?> GetByIdAsync(long id)
        {
            var p = await _context.SpareParts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return p == null ? null : Map(p);
        }

        public async Task<SparePartDto> CreateAsync(CreateSparePartRequest request)
        {
            var part = new SparePart
            {
                PartName = request.PartName.Trim(),
                PartNo = request.PartNo?.Trim(),
                Category = request.Category?.Trim(),
                Unit = request.Unit?.Trim(),
                StockQuantity = request.StockQuantity,
                ReorderLevel = request.ReorderLevel,
                UnitCost = request.UnitCost,
                StoreLocation = request.StoreLocation?.Trim(),
                Supplier = request.Supplier?.Trim(),
                Remarks = request.Remarks?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.SpareParts.Add(part);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created spare part {PartName} (ID: {Id})", part.PartName, part.Id);
            return Map(part);
        }

        public async Task<SparePartDto?> UpdateAsync(long id, UpdateSparePartRequest request)
        {
            var p = await _context.SpareParts.FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return null;

            if (!string.IsNullOrWhiteSpace(request.PartName)) p.PartName = request.PartName.Trim();
            if (request.PartNo != null) p.PartNo = request.PartNo.Trim();
            if (request.Category != null) p.Category = request.Category.Trim();
            if (request.Unit != null) p.Unit = request.Unit.Trim();
            if (request.StockQuantity.HasValue) p.StockQuantity = request.StockQuantity.Value;
            if (request.ReorderLevel.HasValue) p.ReorderLevel = request.ReorderLevel.Value;
            if (request.UnitCost.HasValue) p.UnitCost = request.UnitCost.Value;
            if (request.StoreLocation != null) p.StoreLocation = request.StoreLocation.Trim();
            if (request.Supplier != null) p.Supplier = request.Supplier.Trim();
            if (request.Remarks != null) p.Remarks = request.Remarks.Trim();
            if (request.IsActive.HasValue) p.IsActive = request.IsActive.Value;

            p.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Map(p);
        }

        public async Task<SparePartDto?> AdjustStockAsync(long id, SparePartStockMovementRequest request)
        {
            var p = await _context.SpareParts.FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return null;

            p.StockQuantity += request.QuantityDelta;
            if (p.StockQuantity < 0) p.StockQuantity = 0; // never go negative
            p.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Adjusted stock for spare part {Id} by {Delta} ({Reason})", id, request.QuantityDelta, request.Reason);
            return Map(p);
        }

        public async Task<bool> DeleteAsync(long id)
        {
            var p = await _context.SpareParts.FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return false;

            p.IsActive = false;
            p.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        private static SparePartDto Map(SparePart p) => new()
        {
            Id = p.Id,
            TenantId = p.TenantId,
            PartName = p.PartName,
            PartNo = p.PartNo,
            Category = p.Category,
            Unit = p.Unit,
            StockQuantity = p.StockQuantity,
            ReorderLevel = p.ReorderLevel,
            UnitCost = p.UnitCost,
            StoreLocation = p.StoreLocation,
            Supplier = p.Supplier,
            Remarks = p.Remarks,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }
}
