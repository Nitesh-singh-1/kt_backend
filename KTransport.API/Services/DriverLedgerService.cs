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
    public class DriverLedgerService : IDriverLedgerService
    {
        private readonly KTransportDbContext _context;
        private readonly ILogger<DriverLedgerService> _logger;

        public DriverLedgerService(KTransportDbContext context, ILogger<DriverLedgerService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<DriverLedgerEntryDto>> GetEntriesAsync(string? search = null, string? driverName = null)
        {
            var query = _context.DriverLedgerEntries.AsNoTracking().Where(e => e.IsActive);

            if (!string.IsNullOrWhiteSpace(driverName))
            {
                var d = driverName.Trim().ToLower();
                query = query.Where(e => e.DriverName.ToLower() == d);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(e =>
                    e.DriverName.ToLower().Contains(s) ||
                    (e.Reason != null && e.Reason.ToLower().Contains(s)));
            }

            var list = await query.OrderByDescending(e => e.EntryDate).ThenByDescending(e => e.Id).ToListAsync();
            return list.Select(Map).ToList();
        }

        public async Task<List<DriverOutstandingDto>> GetOutstandingAsync()
        {
            var entries = await _context.DriverLedgerEntries.AsNoTracking()
                .Where(e => e.IsActive)
                .Select(e => new { e.DriverName, e.IsAdvance, e.Amount })
                .ToListAsync();

            return entries
                .GroupBy(e => e.DriverName)
                .Select(g =>
                {
                    var advance = g.Where(x => x.IsAdvance).Sum(x => x.Amount);
                    var recovered = g.Where(x => !x.IsAdvance).Sum(x => x.Amount);
                    return new DriverOutstandingDto
                    {
                        DriverName = g.Key,
                        TotalAdvance = advance,
                        TotalRecovered = recovered,
                        Outstanding = advance - recovered,
                        EntryCount = g.Count()
                    };
                })
                .OrderByDescending(d => d.Outstanding)
                .ToList();
        }

        public async Task<DriverLedgerEntryDto> CreateAsync(CreateDriverLedgerEntryRequest request)
        {
            var entry = new DriverLedgerEntry
            {
                DriverId = request.DriverId,
                DriverName = request.DriverName.Trim(),
                EntryDate = request.EntryDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                IsAdvance = request.IsAdvance,
                Amount = request.Amount,
                Reason = request.Reason?.Trim(),
                Remarks = request.Remarks?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.DriverLedgerEntries.Add(entry);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Driver ledger entry for {Driver}: {Type} {Amount}", entry.DriverName, entry.IsAdvance ? "advance" : "recovery", entry.Amount);
            return Map(entry);
        }

        public async Task<DriverLedgerEntryDto?> UpdateAsync(long id, UpdateDriverLedgerEntryRequest request)
        {
            var e = await _context.DriverLedgerEntries.FirstOrDefaultAsync(x => x.Id == id);
            if (e == null) return null;

            if (request.DriverId.HasValue) e.DriverId = request.DriverId;
            if (!string.IsNullOrWhiteSpace(request.DriverName)) e.DriverName = request.DriverName.Trim();
            if (request.EntryDate.HasValue) e.EntryDate = request.EntryDate.Value;
            if (request.IsAdvance.HasValue) e.IsAdvance = request.IsAdvance.Value;
            if (request.Amount.HasValue) e.Amount = request.Amount.Value;
            if (request.Reason != null) e.Reason = request.Reason.Trim();
            if (request.Remarks != null) e.Remarks = request.Remarks.Trim();
            if (request.IsActive.HasValue) e.IsActive = request.IsActive.Value;

            e.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Map(e);
        }

        public async Task<bool> DeleteAsync(long id)
        {
            var e = await _context.DriverLedgerEntries.FirstOrDefaultAsync(x => x.Id == id);
            if (e == null) return false;

            e.IsActive = false;
            e.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        private static DriverLedgerEntryDto Map(DriverLedgerEntry e) => new()
        {
            Id = e.Id,
            TenantId = e.TenantId,
            DriverId = e.DriverId,
            DriverName = e.DriverName,
            EntryDate = e.EntryDate,
            IsAdvance = e.IsAdvance,
            Amount = e.Amount,
            Reason = e.Reason,
            Remarks = e.Remarks,
            IsActive = e.IsActive,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        };
    }
}
