using System;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    public class VerificationCodeService : IVerificationCodeService
    {
        private readonly KTransportDbContext _context;

        public VerificationCodeService(KTransportDbContext context)
        {
            _context = context;
        }

        public async Task<string> GenerateAsync(string username, string purpose, int validMinutes)
        {
            var normalizedUser = username.Trim().ToLowerInvariant();

            // Invalidate any earlier unconsumed codes for this username/purpose so only the newest is valid.
            var previous = await _context.VerificationCodes
                .Where(v => v.Username == normalizedUser && v.Purpose == purpose && v.ConsumedAt == null)
                .ToListAsync();

            var now = DateTime.UtcNow;
            foreach (var p in previous)
            {
                p.ConsumedAt = now;
            }

            var code = Random.Shared.Next(100000, 1000000).ToString();

            _context.VerificationCodes.Add(new VerificationCode
            {
                Username = normalizedUser,
                Purpose = purpose,
                CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
                ExpiresAt = now.AddMinutes(validMinutes),
                CreatedAt = now
            });

            await _context.SaveChangesAsync();
            return code;
        }

        public async Task<VerificationResult> VerifyAsync(string username, string purpose, string code, int maxAttempts = 5)
        {
            var normalizedUser = username.Trim().ToLowerInvariant();

            var entry = await _context.VerificationCodes
                .Where(v => v.Username == normalizedUser && v.Purpose == purpose && v.ConsumedAt == null)
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefaultAsync();

            if (entry == null)
            {
                return VerificationResult.NotFound;
            }

            if (entry.ExpiresAt < DateTime.UtcNow)
            {
                return VerificationResult.Expired;
            }

            if (entry.AttemptCount >= maxAttempts)
            {
                // Burn the code so it can't be brute-forced further.
                entry.ConsumedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return VerificationResult.TooManyAttempts;
            }

            var matches = false;
            try
            {
                matches = BCrypt.Net.BCrypt.Verify(code.Trim(), entry.CodeHash);
            }
            catch
            {
                matches = false;
            }

            if (!matches)
            {
                entry.AttemptCount++;
                await _context.SaveChangesAsync();
                return VerificationResult.Invalid;
            }

            entry.ConsumedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return VerificationResult.Success;
        }
    }
}
