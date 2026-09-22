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
    public class RateCardService : IRateCardService
    {
        private readonly KTransportDbContext _context;
        private readonly ILogger<RateCardService> _logger;

        public RateCardService(KTransportDbContext context, ILogger<RateCardService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<FreightRateCardDto>> GetRateCardsAsync(string? search = null, long? partyId = null)
        {
            var query = _context.FreightRateCards.AsNoTracking().Where(r => r.IsActive);

            if (partyId.HasValue)
            {
                query = query.Where(r => r.PartyId == partyId.Value || r.PartyId == null);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(r =>
                    r.FromLocation.ToLower().Contains(s) ||
                    r.ToLocation.ToLower().Contains(s) ||
                    (r.PartyName != null && r.PartyName.ToLower().Contains(s))
                );
            }

            var cards = await query.OrderBy(r => r.FromLocation).ThenBy(r => r.ToLocation).ToListAsync();
            return cards.Select(MapToDto).ToList();
        }

        public async Task<FreightRateCardDto?> GetRateCardByIdAsync(long id)
        {
            var r = await _context.FreightRateCards.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return r == null ? null : MapToDto(r);
        }

        public async Task<FreightRateCardDto> CreateRateCardAsync(CreateRateCardRequest request)
        {
            string? partyName = request.PartyName;
            if (request.PartyId.HasValue)
            {
                var party = await _context.Parties.FindAsync(request.PartyId.Value);
                if (party != null) partyName = party.Name;
            }

            var card = new FreightRateCard
            {
                PartyId = request.PartyId,
                PartyName = partyName,
                FromLocation = request.FromLocation.Trim(),
                ToLocation = request.ToLocation.Trim(),
                CommodityType = request.CommodityType?.Trim(),
                RateType = request.RateType,
                BaseRate = request.BaseRate,
                MinFreightAmount = request.MinFreightAmount,
                HamaliRatePerKg = request.HamaliRatePerKg,
                DoorDeliveryCharge = request.DoorDeliveryCharge,
                StationaryCharge = request.StationaryCharge,
                EffectiveFrom = request.EffectiveFrom,
                EffectiveTo = request.EffectiveTo,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.FreightRateCards.Add(card);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created Rate Card for route {From} -> {To}", card.FromLocation, card.ToLocation);
            return MapToDto(card);
        }

        public async Task<FreightRateCardDto?> UpdateRateCardAsync(long id, CreateRateCardRequest request)
        {
            var card = await _context.FreightRateCards.FirstOrDefaultAsync(x => x.Id == id);
            if (card == null) return null;

            card.PartyId = request.PartyId;
            card.PartyName = request.PartyName;
            card.FromLocation = request.FromLocation.Trim();
            card.ToLocation = request.ToLocation.Trim();
            card.CommodityType = request.CommodityType?.Trim();
            card.RateType = request.RateType;
            card.BaseRate = request.BaseRate;
            card.MinFreightAmount = request.MinFreightAmount;
            card.HamaliRatePerKg = request.HamaliRatePerKg;
            card.DoorDeliveryCharge = request.DoorDeliveryCharge;
            card.StationaryCharge = request.StationaryCharge;
            card.EffectiveFrom = request.EffectiveFrom;
            card.EffectiveTo = request.EffectiveTo;
            card.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return MapToDto(card);
        }

        public async Task<bool> DeleteRateCardAsync(long id)
        {
            var card = await _context.FreightRateCards.FirstOrDefaultAsync(x => x.Id == id);
            if (card == null) return false;

            card.IsActive = false;
            card.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<CalculatedFreightResponse> CalculateFreightAsync(CalculateFreightRequest request)
        {
            var from = request.FromLocation.Trim().ToLower();
            var to = request.ToLocation.Trim().ToLower();

            // 1. Try to find customer-specific rate card first
            FreightRateCard? card = null;
            if (request.PartyId.HasValue)
            {
                card = await _context.FreightRateCards
                    .AsNoTracking()
                    .Where(r => r.IsActive && r.PartyId == request.PartyId.Value &&
                                r.FromLocation.ToLower() == from && r.ToLocation.ToLower() == to)
                    .FirstOrDefaultAsync();
            }

            // 2. Fall back to standard route rate card
            if (card == null)
            {
                card = await _context.FreightRateCards
                    .AsNoTracking()
                    .Where(r => r.IsActive && r.PartyId == null &&
                                r.FromLocation.ToLower() == from && r.ToLocation.ToLower() == to)
                    .FirstOrDefaultAsync();
            }

            if (card == null)
            {
                return new CalculatedFreightResponse
                {
                    MatchedRateCard = false,
                    FreightAmount = 0,
                    TotalEstimatedFreight = 0,
                    CalculationBreakdown = $"No rate card found for route {request.FromLocation} to {request.ToLocation}."
                };
            }

            decimal freight = 0;
            switch (card.RateType)
            {
                case RateType.PerKg:
                    freight = request.WeightKg * card.BaseRate;
                    break;
                case RateType.PerTon:
                    freight = (request.WeightKg / 1000m) * card.BaseRate;
                    break;
                case RateType.PerPackage:
                    freight = request.PackagesCount * card.BaseRate;
                    break;
                case RateType.PerTrip:
                case RateType.Fixed:
                    freight = card.BaseRate;
                    break;
            }

            if (card.MinFreightAmount > 0 && freight < card.MinFreightAmount)
            {
                freight = card.MinFreightAmount;
            }

            decimal hamali = request.IncludeHamali ? (request.WeightKg * card.HamaliRatePerKg) : 0;
            decimal dd = request.IncludeDoorDelivery ? card.DoorDeliveryCharge : 0;
            decimal st = card.StationaryCharge;

            decimal total = freight + hamali + dd + st;

            return new CalculatedFreightResponse
            {
                MatchedRateCard = true,
                RateCardId = card.Id,
                BaseRate = card.BaseRate,
                RateType = card.RateType,
                FreightAmount = freight,
                HamaliAmount = hamali,
                DoorDeliveryAmount = dd,
                StationaryAmount = st,
                TotalEstimatedFreight = total,
                CalculationBreakdown = $"Rate: {card.BaseRate} ({card.RateType}) + Hamali: {hamali} + DD: {dd} + ST: {st} = {total}"
            };
        }

        private static FreightRateCardDto MapToDto(FreightRateCard r)
        {
            return new FreightRateCardDto
            {
                Id = r.Id,
                TenantId = r.TenantId,
                PartyId = r.PartyId,
                PartyName = r.PartyName,
                FromLocation = r.FromLocation,
                ToLocation = r.ToLocation,
                CommodityType = r.CommodityType,
                RateType = r.RateType,
                BaseRate = r.BaseRate,
                MinFreightAmount = r.MinFreightAmount,
                HamaliRatePerKg = r.HamaliRatePerKg,
                DoorDeliveryCharge = r.DoorDeliveryCharge,
                StationaryCharge = r.StationaryCharge,
                EffectiveFrom = r.EffectiveFrom,
                EffectiveTo = r.EffectiveTo,
                IsActive = r.IsActive,
                CreatedAt = r.CreatedAt
            };
        }
    }
}
