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
    public class VehicleInsuranceClaimService : IVehicleInsuranceClaimService
    {
        private readonly KTransportDbContext _context;
        private readonly ILogger<VehicleInsuranceClaimService> _logger;

        public VehicleInsuranceClaimService(KTransportDbContext context, ILogger<VehicleInsuranceClaimService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<VehicleInsuranceClaimDto>> GetAllAsync(string? search = null, VehicleClaimStatus? status = null)
        {
            var query = _context.VehicleInsuranceClaims.AsNoTracking().Where(c => c.IsActive);

            if (status.HasValue)
                query = query.Where(c => c.Status == status.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(c =>
                    (c.VehicleNo != null && c.VehicleNo.ToLower().Contains(s)) ||
                    (c.ClaimNo != null && c.ClaimNo.ToLower().Contains(s)) ||
                    (c.InsurerName != null && c.InsurerName.ToLower().Contains(s)));
            }

            var list = await query.OrderByDescending(c => c.ClaimDate).ThenByDescending(c => c.Id).ToListAsync();
            return list.Select(Map).ToList();
        }

        public async Task<VehicleInsuranceClaimDto?> GetByIdAsync(long id)
        {
            var c = await _context.VehicleInsuranceClaims.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return c == null ? null : Map(c);
        }

        public async Task<VehicleInsuranceClaimDto> CreateAsync(CreateVehicleInsuranceClaimRequest request)
        {
            var claim = new VehicleInsuranceClaim
            {
                VehicleId = request.VehicleId,
                VehicleNo = request.VehicleNo.Trim().ToUpperInvariant(),
                ClaimNo = request.ClaimNo?.Trim(),
                InsurerName = request.InsurerName?.Trim(),
                PolicyNo = request.PolicyNo?.Trim(),
                ClaimType = request.ClaimType?.Trim(),
                IncidentDate = request.IncidentDate,
                ClaimDate = request.ClaimDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                ClaimAmount = request.ClaimAmount,
                ApprovedAmount = request.ApprovedAmount,
                Status = request.Status,
                SurveyorName = request.SurveyorName?.Trim(),
                Remarks = request.Remarks?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.VehicleInsuranceClaims.Add(claim);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created vehicle insurance claim for {Vehicle} (ID: {Id})", claim.VehicleNo, claim.Id);
            return Map(claim);
        }

        public async Task<VehicleInsuranceClaimDto?> UpdateAsync(long id, UpdateVehicleInsuranceClaimRequest request)
        {
            var c = await _context.VehicleInsuranceClaims.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return null;

            if (request.VehicleId.HasValue) c.VehicleId = request.VehicleId;
            if (!string.IsNullOrWhiteSpace(request.VehicleNo)) c.VehicleNo = request.VehicleNo.Trim().ToUpperInvariant();
            if (request.ClaimNo != null) c.ClaimNo = request.ClaimNo.Trim();
            if (request.InsurerName != null) c.InsurerName = request.InsurerName.Trim();
            if (request.PolicyNo != null) c.PolicyNo = request.PolicyNo.Trim();
            if (request.ClaimType != null) c.ClaimType = request.ClaimType.Trim();
            if (request.IncidentDate.HasValue) c.IncidentDate = request.IncidentDate;
            if (request.ClaimDate.HasValue) c.ClaimDate = request.ClaimDate.Value;
            if (request.ClaimAmount.HasValue) c.ClaimAmount = request.ClaimAmount.Value;
            if (request.ApprovedAmount.HasValue) c.ApprovedAmount = request.ApprovedAmount.Value;
            if (request.Status.HasValue) c.Status = request.Status.Value;
            if (request.SurveyorName != null) c.SurveyorName = request.SurveyorName.Trim();
            if (request.Remarks != null) c.Remarks = request.Remarks.Trim();
            if (request.IsActive.HasValue) c.IsActive = request.IsActive.Value;

            c.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Map(c);
        }

        public async Task<bool> DeleteAsync(long id)
        {
            var c = await _context.VehicleInsuranceClaims.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return false;

            c.IsActive = false;
            c.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        private static VehicleInsuranceClaimDto Map(VehicleInsuranceClaim c) => new()
        {
            Id = c.Id,
            TenantId = c.TenantId,
            VehicleId = c.VehicleId,
            VehicleNo = c.VehicleNo,
            ClaimNo = c.ClaimNo,
            InsurerName = c.InsurerName,
            PolicyNo = c.PolicyNo,
            ClaimType = c.ClaimType,
            IncidentDate = c.IncidentDate,
            ClaimDate = c.ClaimDate,
            ClaimAmount = c.ClaimAmount,
            ApprovedAmount = c.ApprovedAmount,
            Status = c.Status,
            SurveyorName = c.SurveyorName,
            Remarks = c.Remarks,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };
    }
}
