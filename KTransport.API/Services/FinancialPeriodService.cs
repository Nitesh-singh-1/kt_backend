using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    public class FinancialPeriodService : IFinancialPeriodService
    {
        private readonly KTransportDbContext _context;

        public FinancialPeriodService(KTransportDbContext context)
        {
            _context = context;
        }

        public async Task<List<FinancialPeriodDto>> GetPeriodsAsync()
        {
            var list = await _context.FinancialPeriods.AsNoTracking()
                .OrderByDescending(p => p.StartDate).ToListAsync();
            return list.Select(Map).ToList();
        }

        public async Task<FinancialPeriodDto> CreateAsync(CreatePeriodRequest request)
        {
            var label = request.FyLabel.Trim();
            if (await _context.FinancialPeriods.AnyAsync(p => p.FyLabel == label))
                throw AccountingException.Conflict($"Financial year '{label}' already exists.");

            var (start, end) = DeriveDates(label, request.StartDate, request.EndDate);

            var period = new FinancialPeriod
            {
                FyLabel = label,
                StartDate = start,
                EndDate = end,
                Status = PeriodStatus.Open,
                CreatedAt = DateTime.UtcNow
            };
            _context.FinancialPeriods.Add(period);
            await _context.SaveChangesAsync();
            return Map(period);
        }

        public async Task<FinancialPeriodDto?> CloseAsync(long id)
        {
            var p = await _context.FinancialPeriods.FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return null;
            p.Status = PeriodStatus.Closed;
            p.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Map(p);
        }

        public async Task<FinancialPeriodDto?> ReopenAsync(long id)
        {
            var p = await _context.FinancialPeriods.FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return null;
            p.Status = PeriodStatus.Open;
            p.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Map(p);
        }

        public async Task<long> ResolvePostablePeriodIdAsync(DateOnly date)
        {
            var period = await _context.FinancialPeriods
                .FirstOrDefaultAsync(p => date >= p.StartDate && date <= p.EndDate);

            if (period == null)
            {
                // Lazily create the FY that contains this date so first-time posting just works.
                var label = FyLabelFor(date);
                var (start, end) = DeriveDates(label, null, null);
                period = new FinancialPeriod
                {
                    FyLabel = label,
                    StartDate = start,
                    EndDate = end,
                    Status = PeriodStatus.Open,
                    CreatedAt = DateTime.UtcNow
                };
                _context.FinancialPeriods.Add(period);
                await _context.SaveChangesAsync();
            }

            if (period.Status == PeriodStatus.Closed)
                throw AccountingException.Unprocessable($"Financial year '{period.FyLabel}' is closed; back-dated posting is not allowed.");

            return period.Id;
        }

        /// <summary>Indian FY label for a date, e.g. 2026-04-02 -> "2026-27", 2026-02-10 -> "2025-26".</summary>
        private static string FyLabelFor(DateOnly date)
        {
            var startYear = date.Month >= 4 ? date.Year : date.Year - 1;
            return $"{startYear}-{(startYear + 1) % 100:D2}";
        }

        private static (DateOnly start, DateOnly end) DeriveDates(string label, DateOnly? start, DateOnly? end)
        {
            if (start.HasValue && end.HasValue) return (start.Value, end.Value);
            var startYear = int.Parse(label.Substring(0, 4));
            return (new DateOnly(startYear, 4, 1), new DateOnly(startYear + 1, 3, 31));
        }

        private static FinancialPeriodDto Map(FinancialPeriod p) => new()
        {
            Id = p.Id, FyLabel = p.FyLabel, StartDate = p.StartDate, EndDate = p.EndDate,
            Status = p.Status.ToString(), CreatedAt = p.CreatedAt, UpdatedAt = p.UpdatedAt
        };
    }
}
