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
    public class CargoClaimService : ICargoClaimService
    {
        private readonly KTransportDbContext _context;
        private readonly INumberingSequenceService _numberingService;
        private readonly ILogger<CargoClaimService> _logger;

        public CargoClaimService(
            KTransportDbContext context,
            INumberingSequenceService numberingService,
            ILogger<CargoClaimService> logger)
        {
            _context = context;
            _numberingService = numberingService;
            _logger = logger;
        }

        public async Task<List<CargoClaimDto>> GetClaimsAsync(ClaimStatus? status = null, string? search = null)
        {
            var query = _context.CargoClaims.AsNoTracking().Where(c => c.IsActive);

            if (status.HasValue)
            {
                query = query.Where(c => c.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(c =>
                    c.ClaimNo.ToLower().Contains(s) ||
                    (c.ShipmentNo != null && c.ShipmentNo.ToLower().Contains(s)) ||
                    (c.PartyName != null && c.PartyName.ToLower().Contains(s))
                );
            }

            var claims = await query.OrderByDescending(c => c.ClaimDate).ThenByDescending(c => c.Id).ToListAsync();
            return claims.Select(MapToDto).ToList();
        }

        public async Task<CargoClaimDto?> GetClaimByIdAsync(long id)
        {
            var c = await _context.CargoClaims.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return c == null ? null : MapToDto(c);
        }

        public async Task<CargoClaimDto> CreateClaimAsync(CreateClaimRequest request)
        {
            string claimNo = request.ClaimNo?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(claimNo))
            {
                claimNo = await _numberingService.GetNextNumberAsync("CLAIM", "CLM");
            }

            string? shipmentNo = null;
            var shipment = await _context.Shipments.FindAsync(request.ShipmentId);
            if (shipment != null)
            {
                shipmentNo = shipment.ShipmentNo;
            }

            string? partyName = null;
            if (request.PartyId.HasValue)
            {
                var party = await _context.Parties.FindAsync(request.PartyId.Value);
                if (party != null) partyName = party.Name;
            }

            var claim = new CargoClaim
            {
                ClaimNo = claimNo,
                ShipmentId = request.ShipmentId,
                ShipmentNo = shipmentNo,
                PartyId = request.PartyId,
                PartyName = partyName,
                ClaimType = request.ClaimType,
                ClaimAmount = request.ClaimAmount,
                Status = ClaimStatus.Reported,
                ClaimDate = request.ClaimDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                Description = request.Description.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.CargoClaims.Add(claim);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Reported Claim {ClaimNo} for Shipment {ShipmentNo}", claim.ClaimNo, shipmentNo);
            return MapToDto(claim);
        }

        public async Task<CargoClaimDto?> SettleClaimAsync(long id, SettleClaimRequest request)
        {
            var claim = await _context.CargoClaims.FirstOrDefaultAsync(x => x.Id == id);
            if (claim == null) return null;

            claim.Status = request.Status;
            claim.SettledAmount = request.SettledAmount;
            claim.SettlementRemarks = request.SettlementRemarks;
            claim.InvestigationNotes = request.InvestigationNotes ?? claim.InvestigationNotes;
            if (request.Status == ClaimStatus.Settled)
            {
                claim.SettledDate = DateOnly.FromDateTime(DateTime.UtcNow);
            }
            claim.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return MapToDto(claim);
        }

        public async Task<bool> DeleteClaimAsync(long id)
        {
            var claim = await _context.CargoClaims.FirstOrDefaultAsync(x => x.Id == id);
            if (claim == null) return false;

            claim.IsActive = false;
            claim.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        private static CargoClaimDto MapToDto(CargoClaim c)
        {
            return new CargoClaimDto
            {
                Id = c.Id,
                TenantId = c.TenantId,
                ClaimNo = c.ClaimNo,
                ShipmentId = c.ShipmentId,
                ShipmentNo = c.ShipmentNo,
                PartyId = c.PartyId,
                PartyName = c.PartyName,
                ClaimType = c.ClaimType,
                ClaimAmount = c.ClaimAmount,
                SettledAmount = c.SettledAmount,
                Status = c.Status,
                ClaimDate = c.ClaimDate,
                SettledDate = c.SettledDate,
                Description = c.Description,
                InvestigationNotes = c.InvestigationNotes,
                SettlementRemarks = c.SettlementRemarks,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt
            };
        }
    }
}
