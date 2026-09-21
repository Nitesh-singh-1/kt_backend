using System;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KTransport.API.Services
{
    public class NumberingSequenceService : INumberingSequenceService
    {
        private readonly ILogger<NumberingSequenceService> _logger;
        private readonly KTransportDbContext _context;
        private readonly ITenantContext _tenantContext;

        public NumberingSequenceService(
            ILogger<NumberingSequenceService> logger, 
            KTransportDbContext context, 
            ITenantContext tenantContext)
        {
            _logger = logger;
            _context = context;
            _tenantContext = tenantContext;
        }

        public async Task<string> GetNextNumberAsync(string entityType, string? customPrefix = null)
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var currentYear = DateTime.UtcNow.Year;
            var normalizedType = entityType.Trim().ToUpperInvariant();

            var sequence = await _context.Set<NumberingSequence>()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.EntityType == normalizedType && s.Year == currentYear);

            if (sequence == null)
            {
                var defaultPrefix = customPrefix ?? (normalizedType == "CHALLAN" ? "CH" : "GR");
                sequence = new NumberingSequence
                {
                    TenantId = tenantId,
                    EntityType = normalizedType,
                    Prefix = defaultPrefix,
                    CurrentValue = 1,
                    Padding = 4,
                    Year = currentYear,
                    FormatPattern = "{PREFIX}-{YEAR}-{SEQ}"
                };

                _context.Set<NumberingSequence>().Add(sequence);
                await _context.SaveChangesAsync();

                return FormatSequence(sequence);
            }

            sequence.CurrentValue++;
            if (!string.IsNullOrEmpty(customPrefix))
            {
                sequence.Prefix = customPrefix;
            }

            await _context.SaveChangesAsync();
            return FormatSequence(sequence);
        }

        private static string FormatSequence(NumberingSequence seq)
        {
            var seqFormatted = seq.CurrentValue.ToString().PadLeft(seq.Padding, '0');
            return seq.FormatPattern
                .Replace("{PREFIX}", seq.Prefix)
                .Replace("{YEAR}", seq.Year.ToString())
                .Replace("{SEQ}", seqFormatted);
        }
    }
}
