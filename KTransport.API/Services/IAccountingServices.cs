using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;

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
    }

    /// <summary>Read models derived from the ledger: Day Book, Account Ledger, Trial Balance.</summary>
    public interface IAccountingReportService
    {
        Task<List<DayBookEntryDto>> GetDayBookAsync(DateOnly from, DateOnly to);
        Task<AccountLedgerDto?> GetLedgerAsync(long accountId, DateOnly from, DateOnly to);
        Task<TrialBalanceDto> GetTrialBalanceAsync(DateOnly asOf);
    }
}
