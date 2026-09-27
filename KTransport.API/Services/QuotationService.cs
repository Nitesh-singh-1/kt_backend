using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KTransport.API.Services
{
    public class QuotationService : IQuotationService
    {
        private readonly KTransportDbContext _context;
        private readonly ILogger<QuotationService> _logger;

        public QuotationService(KTransportDbContext context, ILogger<QuotationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<QuotationDto>> GetQuotationsAsync(string? search = null, QuotationStatus? status = null)
        {
            var query = _context.Quotations.AsNoTracking().Where(q => q.IsActive);

            if (status.HasValue)
                query = query.Where(q => q.Status == status.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(q =>
                    q.QuoteNo.ToLower().Contains(s) ||
                    (q.PartyName != null && q.PartyName.ToLower().Contains(s)) ||
                    (q.FromLocation != null && q.FromLocation.ToLower().Contains(s)) ||
                    (q.ToLocation != null && q.ToLocation.ToLower().Contains(s))
                );
            }

            var list = await query.OrderByDescending(q => q.Id).ToListAsync();
            return list.Select(Map).ToList();
        }

        public async Task<QuotationDto?> GetQuotationByIdAsync(long id)
        {
            var q = await _context.Quotations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return q == null ? null : Map(q);
        }

        public async Task<QuotationDto> CreateQuotationAsync(CreateQuotationRequest request)
        {
            var quote = new Quotation
            {
                QuoteNo = await GenerateQuoteNoAsync(),
                QuoteDate = request.QuoteDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                ValidUntil = request.ValidUntil,
                PartyId = request.PartyId,
                PartyName = request.PartyName.Trim(),
                PartyMobile = request.PartyMobile?.Trim(),
                PartyGstNo = request.PartyGstNo?.Trim().ToUpperInvariant(),
                FromLocation = request.FromLocation?.Trim(),
                ToLocation = request.ToLocation?.Trim(),
                VehicleType = request.VehicleType?.Trim(),
                GoodsDescription = request.GoodsDescription?.Trim(),
                WeightKg = request.WeightKg,
                RatePerUnit = request.RatePerUnit,
                RateBasis = request.RateBasis?.Trim(),
                EstimatedFreight = request.EstimatedFreight,
                Status = QuotationStatus.Draft,
                Terms = request.Terms?.Trim(),
                Notes = request.Notes?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Quotations.Add(quote);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created quotation {QuoteNo} (ID: {Id})", quote.QuoteNo, quote.Id);
            return Map(quote);
        }

        public async Task<QuotationDto?> UpdateQuotationAsync(long id, UpdateQuotationRequest request)
        {
            var quote = await _context.Quotations.FirstOrDefaultAsync(q => q.Id == id);
            if (quote == null) return null;

            if (request.QuoteDate.HasValue) quote.QuoteDate = request.QuoteDate.Value;
            if (request.ValidUntil.HasValue) quote.ValidUntil = request.ValidUntil;
            if (request.PartyId.HasValue) quote.PartyId = request.PartyId;
            if (!string.IsNullOrWhiteSpace(request.PartyName)) quote.PartyName = request.PartyName.Trim();
            if (request.PartyMobile != null) quote.PartyMobile = request.PartyMobile.Trim();
            if (request.PartyGstNo != null) quote.PartyGstNo = request.PartyGstNo.Trim().ToUpperInvariant();
            if (request.FromLocation != null) quote.FromLocation = request.FromLocation.Trim();
            if (request.ToLocation != null) quote.ToLocation = request.ToLocation.Trim();
            if (request.VehicleType != null) quote.VehicleType = request.VehicleType.Trim();
            if (request.GoodsDescription != null) quote.GoodsDescription = request.GoodsDescription.Trim();
            if (request.WeightKg.HasValue) quote.WeightKg = request.WeightKg;
            if (request.RatePerUnit.HasValue) quote.RatePerUnit = request.RatePerUnit;
            if (request.RateBasis != null) quote.RateBasis = request.RateBasis.Trim();
            if (request.EstimatedFreight.HasValue) quote.EstimatedFreight = request.EstimatedFreight.Value;
            if (request.Terms != null) quote.Terms = request.Terms.Trim();
            if (request.Notes != null) quote.Notes = request.Notes.Trim();
            if (request.IsActive.HasValue) quote.IsActive = request.IsActive.Value;

            quote.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated quotation {QuoteNo} (ID: {Id})", quote.QuoteNo, quote.Id);
            return Map(quote);
        }

        public async Task<QuotationDto?> UpdateStatusAsync(long id, UpdateQuotationStatusRequest request)
        {
            var quote = await _context.Quotations.FirstOrDefaultAsync(q => q.Id == id);
            if (quote == null) return null;

            quote.Status = request.Status;
            if (request.Status == QuotationStatus.Converted && !string.IsNullOrWhiteSpace(request.ConvertedRef))
                quote.ConvertedRef = request.ConvertedRef.Trim();

            quote.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Quotation {QuoteNo} status -> {Status}", quote.QuoteNo, quote.Status);
            return Map(quote);
        }

        public async Task<bool> DeleteQuotationAsync(long id)
        {
            var quote = await _context.Quotations.FirstOrDefaultAsync(q => q.Id == id);
            if (quote == null) return false;

            quote.IsActive = false;
            quote.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<string> GenerateQuoteNoAsync()
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"QUO-{year}-";
            // The global query filter already scopes this count to the current tenant.
            var tenantCount = await _context.Quotations
                .Where(q => q.QuoteNo.StartsWith(prefix))
                .CountAsync();
            var next = tenantCount + 1;
            return $"{prefix}{next.ToString("D4", CultureInfo.InvariantCulture)}";
        }

        private static QuotationDto Map(Quotation q) => new()
        {
            Id = q.Id,
            TenantId = q.TenantId,
            QuoteNo = q.QuoteNo,
            QuoteDate = q.QuoteDate,
            ValidUntil = q.ValidUntil,
            PartyId = q.PartyId,
            PartyName = q.PartyName,
            PartyMobile = q.PartyMobile,
            PartyGstNo = q.PartyGstNo,
            FromLocation = q.FromLocation,
            ToLocation = q.ToLocation,
            VehicleType = q.VehicleType,
            GoodsDescription = q.GoodsDescription,
            WeightKg = q.WeightKg,
            RatePerUnit = q.RatePerUnit,
            RateBasis = q.RateBasis,
            EstimatedFreight = q.EstimatedFreight,
            Status = q.Status,
            ConvertedRef = q.ConvertedRef,
            Terms = q.Terms,
            Notes = q.Notes,
            IsActive = q.IsActive,
            CreatedAt = q.CreatedAt,
            UpdatedAt = q.UpdatedAt
        };
    }
}
