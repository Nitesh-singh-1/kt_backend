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
    public class VendorRateContractService : IVendorRateContractService
    {
        private readonly KTransportDbContext _context;
        private readonly ILogger<VendorRateContractService> _logger;

        public VendorRateContractService(KTransportDbContext context, ILogger<VendorRateContractService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<VendorRateContractDto>> GetAllAsync(string? search = null, long? vendorId = null)
        {
            var query = _context.VendorRateContracts.AsNoTracking().Where(c => c.IsActive);

            if (vendorId.HasValue)
                query = query.Where(c => c.VendorId == vendorId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(c =>
                    (c.VendorName != null && c.VendorName.ToLower().Contains(s)) ||
                    c.FromLocation.ToLower().Contains(s) ||
                    c.ToLocation.ToLower().Contains(s));
            }

            var list = await query.OrderByDescending(c => c.Id).ToListAsync();
            return list.Select(Map).ToList();
        }

        public async Task<VendorRateContractDto?> GetByIdAsync(long id)
        {
            var c = await _context.VendorRateContracts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return c == null ? null : Map(c);
        }

        public async Task<VendorRateContractDto> CreateAsync(CreateVendorRateContractRequest request)
        {
            var contract = new VendorRateContract
            {
                VendorId = request.VendorId,
                VendorName = request.VendorName.Trim(),
                FromLocation = request.FromLocation.Trim(),
                ToLocation = request.ToLocation.Trim(),
                VehicleType = request.VehicleType?.Trim(),
                RateType = request.RateType,
                HireRate = request.HireRate,
                MinGuaranteeAmount = request.MinGuaranteeAmount,
                LoadingCharge = request.LoadingCharge,
                UnloadingCharge = request.UnloadingCharge,
                EffectiveFrom = request.EffectiveFrom,
                EffectiveTo = request.EffectiveTo,
                Remarks = request.Remarks?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.VendorRateContracts.Add(contract);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created vendor rate contract {From}->{To} for {Vendor} (ID: {Id})",
                contract.FromLocation, contract.ToLocation, contract.VendorName, contract.Id);
            return Map(contract);
        }

        public async Task<VendorRateContractDto?> UpdateAsync(long id, UpdateVendorRateContractRequest request)
        {
            var c = await _context.VendorRateContracts.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return null;

            if (request.VendorId.HasValue) c.VendorId = request.VendorId;
            if (!string.IsNullOrWhiteSpace(request.VendorName)) c.VendorName = request.VendorName.Trim();
            if (!string.IsNullOrWhiteSpace(request.FromLocation)) c.FromLocation = request.FromLocation.Trim();
            if (!string.IsNullOrWhiteSpace(request.ToLocation)) c.ToLocation = request.ToLocation.Trim();
            if (request.VehicleType != null) c.VehicleType = request.VehicleType.Trim();
            if (request.RateType.HasValue) c.RateType = request.RateType.Value;
            if (request.HireRate.HasValue) c.HireRate = request.HireRate.Value;
            if (request.MinGuaranteeAmount.HasValue) c.MinGuaranteeAmount = request.MinGuaranteeAmount.Value;
            if (request.LoadingCharge.HasValue) c.LoadingCharge = request.LoadingCharge.Value;
            if (request.UnloadingCharge.HasValue) c.UnloadingCharge = request.UnloadingCharge.Value;
            if (request.EffectiveFrom.HasValue) c.EffectiveFrom = request.EffectiveFrom;
            if (request.EffectiveTo.HasValue) c.EffectiveTo = request.EffectiveTo;
            if (request.Remarks != null) c.Remarks = request.Remarks.Trim();
            if (request.IsActive.HasValue) c.IsActive = request.IsActive.Value;

            c.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Map(c);
        }

        public async Task<bool> DeleteAsync(long id)
        {
            var c = await _context.VendorRateContracts.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return false;

            c.IsActive = false;
            c.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        private static VendorRateContractDto Map(VendorRateContract c) => new()
        {
            Id = c.Id,
            TenantId = c.TenantId,
            VendorId = c.VendorId,
            VendorName = c.VendorName,
            FromLocation = c.FromLocation,
            ToLocation = c.ToLocation,
            VehicleType = c.VehicleType,
            RateType = c.RateType,
            HireRate = c.HireRate,
            MinGuaranteeAmount = c.MinGuaranteeAmount,
            LoadingCharge = c.LoadingCharge,
            UnloadingCharge = c.UnloadingCharge,
            EffectiveFrom = c.EffectiveFrom,
            EffectiveTo = c.EffectiveTo,
            Remarks = c.Remarks,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };
    }
}
