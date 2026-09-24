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
    public class PartyService : IPartyService
    {
        private readonly KTransportDbContext _context;
        private readonly ILogger<PartyService> _logger;

        public PartyService(KTransportDbContext context, ILogger<PartyService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<PartyDto>> GetPartiesAsync(string? search = null, PartyType? partyType = null, bool activeOnly = true)
        {
            var query = _context.Parties.AsNoTracking();

            if (activeOnly)
            {
                query = query.Where(p => p.IsActive);
            }

            if (partyType.HasValue)
            {
                query = query.Where(p => p.PartyType == partyType.Value || p.PartyType == PartyType.Both);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(s) ||
                    (p.Code != null && p.Code.ToLower().Contains(s)) ||
                    (p.GstNo != null && p.GstNo.ToLower().Contains(s)) ||
                    (p.Mobile != null && p.Mobile.Contains(s)) ||
                    (p.City != null && p.City.ToLower().Contains(s))
                );
            }

            var parties = await query.OrderBy(p => p.Name).ToListAsync();
            return parties.Select(MapToDto).ToList();
        }

        public async Task<List<PartyLookupDto>> GetPartyLookupAsync(string? query = null, PartyType? partyType = null)
        {
            var dbQuery = _context.Parties.AsNoTracking().Where(p => p.IsActive);

            if (partyType.HasValue)
            {
                dbQuery = dbQuery.Where(p => p.PartyType == partyType.Value || p.PartyType == PartyType.Both);
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var s = query.Trim().ToLower();
                dbQuery = dbQuery.Where(p =>
                    p.Name.ToLower().Contains(s) ||
                    (p.Code != null && p.Code.ToLower().Contains(s)) ||
                    (p.GstNo != null && p.GstNo.ToLower().Contains(s)) ||
                    (p.Mobile != null && p.Mobile.Contains(s)) ||
                    (p.City != null && p.City.ToLower().Contains(s))
                );
            }

            return await dbQuery
                .OrderBy(p => p.Name)
                .Take(50)
                .Select(p => new PartyLookupDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Code = p.Code,
                    GstNo = p.GstNo,
                    Mobile = p.Mobile,
                    Address = p.Address,
                    City = p.City,
                    State = p.State,
                    Pincode = p.Pincode,
                    PartyType = p.PartyType,
                    DefaultPaymentTerm = p.DefaultPaymentTerm
                })
                .ToListAsync();
        }

        public async Task<PartyDto?> GetPartyByIdAsync(long id)
        {
            var party = await _context.Parties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            return party == null ? null : MapToDto(party);
        }

        public async Task<PartyDto> CreatePartyAsync(CreatePartyRequest request)
        {
            var code = request.Code?.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                code = GeneratePartyAlias(request.Name);
            }

            var party = new Party
            {
                Name = request.Name.Trim(),
                Code = code,
                GstNo = request.GstNo?.Trim()?.ToUpperInvariant(),
                PanNo = request.PanNo?.Trim()?.ToUpperInvariant(),
                Mobile = request.Mobile?.Trim(),
                Phone = request.Phone?.Trim(),
                Email = request.Email?.Trim(),
                Address = request.Address?.Trim(),
                City = request.City?.Trim(),
                State = request.State?.Trim(),
                Pincode = request.Pincode?.Trim(),
                PartyType = request.PartyType,
                DefaultPaymentTerm = request.DefaultPaymentTerm,
                CreditLimit = request.CreditLimit,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Parties.Add(party);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new party: {Name} (ID: {Id}, Code: {Code})", party.Name, party.Id, party.Code);
            return MapToDto(party);
        }

        private static string GeneratePartyAlias(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "PARTY-01";
            var clean = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9\s]", "").Trim();
            var words = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 1)
            {
                var w = words[0].ToUpperInvariant();
                return w.Length <= 6 ? w : w[..6];
            }
            if (words.Length == 2)
            {
                var w1 = words[0].ToUpperInvariant();
                var w2 = words[1].ToUpperInvariant();
                var p1 = w1.Length <= 3 ? w1 : w1[..3];
                var p2 = w2.Length <= 3 ? w2 : w2[..3];
                return $"{p1}-{p2}";
            }
            // 3 or more words: Take initials + last word prefix
            var initials = string.Concat(words.Take(3).Select(w => char.ToUpperInvariant(w[0])));
            var lastWord = words[^1].ToUpperInvariant();
            var lastPrefix = lastWord.Length <= 4 ? lastWord : lastWord[..4];
            return $"{initials}-{lastPrefix}";
        }

        public async Task<PartyDto?> UpdatePartyAsync(long id, UpdatePartyRequest request)
        {
            var party = await _context.Parties.FirstOrDefaultAsync(p => p.Id == id);
            if (party == null) return null;

            party.Name = request.Name.Trim();
            party.Code = request.Code?.Trim();
            party.GstNo = request.GstNo?.Trim();
            party.PanNo = request.PanNo?.Trim();
            party.Mobile = request.Mobile?.Trim();
            party.Phone = request.Phone?.Trim();
            party.Email = request.Email?.Trim();
            party.Address = request.Address?.Trim();
            party.City = request.City?.Trim();
            party.State = request.State?.Trim();
            party.Pincode = request.Pincode?.Trim();
            party.PartyType = request.PartyType;
            party.DefaultPaymentTerm = request.DefaultPaymentTerm;
            party.CreditLimit = request.CreditLimit;
            party.IsActive = request.IsActive;
            party.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated party ID: {Id}", party.Id);
            return MapToDto(party);
        }

        public async Task<bool> DeletePartyAsync(long id)
        {
            var party = await _context.Parties.FirstOrDefaultAsync(p => p.Id == id);
            if (party == null) return false;

            party.IsActive = false;
            party.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Soft-deleted party ID: {Id}", id);
            return true;
        }

        private static PartyDto MapToDto(Party p)
        {
            return new PartyDto
            {
                Id = p.Id,
                TenantId = p.TenantId,
                Name = p.Name,
                Code = p.Code,
                GstNo = p.GstNo,
                PanNo = p.PanNo,
                Mobile = p.Mobile,
                Phone = p.Phone,
                Email = p.Email,
                Address = p.Address,
                City = p.City,
                State = p.State,
                Pincode = p.Pincode,
                PartyType = p.PartyType,
                DefaultPaymentTerm = p.DefaultPaymentTerm,
                CreditLimit = p.CreditLimit,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            };
        }
    }
}
