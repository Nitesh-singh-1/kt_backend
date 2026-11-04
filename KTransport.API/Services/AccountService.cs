using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KTransport.API.Services
{
    public class AccountService : IAccountService
    {
        private readonly KTransportDbContext _context;
        private readonly ILogger<AccountService> _logger;

        public AccountService(KTransportDbContext context, ILogger<AccountService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task EnsureSeededAsync()
        {
            // Idempotent: only seed codes that are missing for this tenant.
            var existingCodes = (await _context.Accounts.Select(a => a.Code).ToListAsync()).ToHashSet();
            if (AccountingSeedData.DefaultGroups.All(g => existingCodes.Contains(g.Code)))
                return;

            // Primaries first (no parent), then sub-groups so parent ids resolve.
            var byCode = new Dictionary<string, Account>();

            foreach (var g in AccountingSeedData.DefaultGroups.Where(g => g.ParentCode == null))
            {
                if (existingCodes.Contains(g.Code)) continue;
                var acc = NewSystemGroup(g, null);
                _context.Accounts.Add(acc);
                byCode[g.Code] = acc;
            }
            await _context.SaveChangesAsync();

            // Reload parent ids (including any that already existed before this run).
            var parents = await _context.Accounts
                .Where(a => a.IsSystem && a.ParentId == null)
                .ToDictionaryAsync(a => a.Code, a => a.Id);

            foreach (var g in AccountingSeedData.DefaultGroups.Where(g => g.ParentCode != null))
            {
                if (existingCodes.Contains(g.Code)) continue;
                var parentId = parents.TryGetValue(g.ParentCode!, out var pid) ? pid : (long?)null;
                _context.Accounts.Add(NewSystemGroup(g, parentId));
            }
            await _context.SaveChangesAsync();

            _logger.LogInformation("Seeded Tally default chart of accounts ({Count} groups).", AccountingSeedData.DefaultGroups.Count);
        }

        private static Account NewSystemGroup(AccountingSeedData.SeedGroup g, long? parentId) => new()
        {
            ParentId = parentId,
            Code = g.Code,
            Name = g.Name,
            AccountType = g.Type,
            Nature = g.Nature,
            IsGroup = true,
            IsSystem = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        public async Task<List<AccountDto>> GetAccountsAsync(string? search = null, string? type = null, bool includeInactive = false)
        {
            await EnsureSeededAsync();

            var query = _context.Accounts.AsNoTracking().AsQueryable();
            if (!includeInactive) query = query.Where(a => a.IsActive);

            if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<AccountType>(type, true, out var at))
                query = query.Where(a => a.AccountType == at);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(a => a.Name.ToLower().Contains(s) || a.Code.ToLower().Contains(s));
            }

            var list = await query.OrderBy(a => a.Code).ToListAsync();
            return list.Select(Map).ToList();
        }

        public async Task<List<AccountNodeDto>> GetTreeAsync()
        {
            await EnsureSeededAsync();
            var all = await _context.Accounts.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.Code).ToListAsync();

            var nodes = all.ToDictionary(a => a.Id, a => new AccountNodeDto
            {
                Id = a.Id, TenantId = a.TenantId, ParentId = a.ParentId, Code = a.Code, Name = a.Name,
                AccountType = a.AccountType.ToString(), Nature = a.Nature.ToString(), IsGroup = a.IsGroup,
                IsSystem = a.IsSystem, SourcePartyId = a.SourcePartyId, IsActive = a.IsActive,
                CreatedAt = a.CreatedAt, UpdatedAt = a.UpdatedAt
            });

            var roots = new List<AccountNodeDto>();
            foreach (var n in nodes.Values)
            {
                if (n.ParentId.HasValue && nodes.TryGetValue(n.ParentId.Value, out var parent))
                    parent.Children.Add(n);
                else
                    roots.Add(n);
            }
            return roots.OrderBy(r => r.Code).ToList();
        }

        public async Task<AccountDto?> GetByIdAsync(long id)
        {
            var a = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return a == null ? null : Map(a);
        }

        public async Task<AccountDto> CreateAsync(CreateAccountRequest request)
        {
            await EnsureSeededAsync();

            Account? parent = null;
            if (request.ParentId.HasValue)
            {
                parent = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == request.ParentId.Value)
                    ?? throw AccountingException.Unprocessable("Parent group not found.");
                if (!parent.IsGroup)
                    throw AccountingException.Unprocessable("A ledger can only be created under a group, not under another ledger.");
            }

            var type = request.AccountType ?? parent?.AccountType
                ?? throw AccountingException.Unprocessable("Account type is required when there is no parent group to inherit from.");
            var nature = request.Nature ?? parent?.Nature ?? (type is AccountType.Asset or AccountType.Expense ? DrCr.DR : DrCr.CR);

            var code = string.IsNullOrWhiteSpace(request.Code)
                ? await GenerateCodeAsync(parent)
                : request.Code.Trim();

            if (await _context.Accounts.AnyAsync(a => a.Code == code))
                throw AccountingException.Conflict($"An account with code '{code}' already exists.");

            var account = new Account
            {
                ParentId = request.ParentId,
                Code = code,
                Name = request.Name.Trim(),
                AccountType = type,
                Nature = nature,
                IsGroup = false,          // tenant-created nodes are postable ledgers
                IsSystem = false,
                SourcePartyId = request.SourcePartyId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();
            return Map(account);
        }

        public async Task<AccountDto?> UpdateAsync(long id, UpdateAccountRequest request)
        {
            var a = await _context.Accounts.FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return null;
            if (a.IsSystem)
                throw AccountingException.Unprocessable("System groups cannot be modified.");

            if (!string.IsNullOrWhiteSpace(request.Name)) a.Name = request.Name.Trim();
            if (request.IsActive.HasValue) a.IsActive = request.IsActive.Value;
            a.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Map(a);
        }

        public async Task<bool> DeleteAsync(long id)
        {
            var a = await _context.Accounts.FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return false;
            if (a.IsSystem)
                throw AccountingException.Unprocessable("System groups cannot be deleted.");
            if (await _context.Accounts.AnyAsync(c => c.ParentId == id))
                throw AccountingException.Unprocessable("Cannot delete an account that has child accounts.");
            if (await _context.VoucherLines.AnyAsync(l => l.AccountId == id))
                throw AccountingException.Unprocessable("Cannot delete an account that has postings; deactivate it instead.");

            _context.Accounts.Remove(a);
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<string> GenerateCodeAsync(Account? parent)
        {
            var prefix = parent?.Code ?? "9";
            var childCount = parent == null
                ? await _context.Accounts.CountAsync(a => a.ParentId == null)
                : await _context.Accounts.CountAsync(a => a.ParentId == parent.Id);
            // e.g. "1160-1", "1160-2" — unique and readable; retry on the rare collision.
            for (var n = childCount + 1; ; n++)
            {
                var candidate = $"{prefix}-{n}";
                if (!await _context.Accounts.AnyAsync(a => a.Code == candidate))
                    return candidate;
            }
        }

        private static AccountDto Map(Account a) => new()
        {
            Id = a.Id, TenantId = a.TenantId, ParentId = a.ParentId, Code = a.Code, Name = a.Name,
            AccountType = a.AccountType.ToString(), Nature = a.Nature.ToString(), IsGroup = a.IsGroup,
            IsSystem = a.IsSystem, SourcePartyId = a.SourcePartyId, IsActive = a.IsActive,
            CreatedAt = a.CreatedAt, UpdatedAt = a.UpdatedAt
        };
    }
}
