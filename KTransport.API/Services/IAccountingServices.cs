using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;
using KTransport.API.Models;

namespace KTransport.API.Services
{
    /// <summary>Chart of accounts: the 28 Tally system groups + tenant-added ledgers.</summary>
    public interface IAccountService
    {
        /// <summary>Idempotently seeds the 28 Tally default groups for the current tenant.</summary>
        Task EnsureSeededAsync();

        Task<List<AccountDto>> GetAccountsAsync(string? search = null, string? type = null, bool includeInactive = false);
        Task<List<AccountNodeDto>> GetTreeAsync();
        Task<AccountDto?> GetByIdAsync(long id);
        Task<AccountDto> CreateAsync(CreateAccountRequest request);
        Task<AccountDto?> UpdateAsync(long id, UpdateAccountRequest request);
        Task<bool> DeleteAsync(long id);
    }

    /// <summary>Financial years (Apr–Mar) and their open/closed state.</summary>
    public interface IFinancialPeriodService
    {
        Task<List<FinancialPeriodDto>> GetPeriodsAsync();
        Task<FinancialPeriodDto> CreateAsync(CreatePeriodRequest request);
        Task<FinancialPeriodDto?> CloseAsync(long id);
        Task<FinancialPeriodDto?> ReopenAsync(long id);

        /// <summary>Resolves (and lazily creates) the Open period that contains <paramref name="date"/>.
        /// Throws if the containing period is Closed.</summary>
        Task<long> ResolvePostablePeriodIdAsync(DateOnly date);
    }

    /// <summary>The double-entry voucher engine: create (balanced), list, get, reverse.</summary>
    public interface IVoucherService
    {
        Task<VoucherListResponse> GetVouchersAsync(string? type, DateOnly? from, DateOnly? to, long? accountId, string? search, int page, int pageSize);
        Task<VoucherDto?> GetByIdAsync(long id);
        Task<VoucherDto> CreateAsync(CreateVoucherRequest request, int? userId);
        Task<VoucherDto?> ReverseAsync(long id, ReverseVoucherRequest request, int? userId);

        /// <summary>
        /// Phase 2 auto-posting entry point: persists a balanced, Posted voucher that carries
        /// <paramref name="sourceType"/>/<paramref name="sourceId"/> (the public <see cref="CreateAsync"/>
        /// never sets a source). Same validation as CreateAsync (≥2 lines, each &gt; 0, ΣDR = ΣCR,
        /// accounts active + postable, open period). The caller is responsible for idempotency.
        /// </summary>
        Task<VoucherDto> CreateFromSourceAsync(VoucherType type, DateOnly date, string? narration,
            IReadOnlyList<DTOs.VoucherLineInput> lines, string sourceType, long sourceId, int? userId);
    }

    /// <summary>Per-financial-year opening balances (the migration entry point for existing tenants).</summary>
    public interface IOpeningBalanceService
    {
        /// <summary>The recorded opening rows for a period, or null if the period does not exist.</summary>
        Task<OpeningBalanceSetDto?> GetAsync(long periodId);

        /// <summary>Full-replace the period's opening rows. Validates balanced + postable accounts.
        /// Returns null if the period does not exist; throws <see cref="Common.AccountingException"/>
        /// (422/409) on a business-rule violation.</summary>
        Task<OpeningBalanceSetDto?> SetAsync(SetOpeningBalancesRequest request);
    }

    /// <summary>Read models derived from the ledger: Day Book, Account Ledger, Trial Balance.</summary>
    public interface IAccountingReportService
    {
        Task<List<DayBookEntryDto>> GetDayBookAsync(DateOnly from, DateOnly to);
        Task<AccountLedgerDto?> GetLedgerAsync(long accountId, DateOnly from, DateOnly to);
        Task<TrialBalanceDto> GetTrialBalanceAsync(DateOnly asOf);
    }
}
