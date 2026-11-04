using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using KTransport.API.Models;

namespace KTransport.API.DTOs
{
    // ---------------- Accounts (chart of accounts) ----------------

    public class AccountDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public long? ParentId { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string AccountType { get; set; } = null!;
        public string Nature { get; set; } = null!;
        public bool IsGroup { get; set; }
        public bool IsSystem { get; set; }
        public long? SourcePartyId { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class AccountNodeDto : AccountDto
    {
        public List<AccountNodeDto> Children { get; set; } = new();
    }

    public class CreateAccountRequest
    {
        public long? ParentId { get; set; }

        /// <summary>Optional — auto-generated under the parent group when omitted.</summary>
        public string? Code { get; set; }

        [Required(ErrorMessage = "Account name is required.")]
        [MaxLength(150)]
        public string Name { get; set; } = null!;

        /// <summary>Optional — inherited from the parent group when omitted.</summary>
        public AccountType? AccountType { get; set; }

        /// <summary>Optional — inherited from the parent group when omitted.</summary>
        public DrCr? Nature { get; set; }

        /// <summary>Phase 2 linkage; normally null for a manually created ledger.</summary>
        public long? SourcePartyId { get; set; }
    }

    public class UpdateAccountRequest
    {
        [MaxLength(150)]
        public string? Name { get; set; }
        public bool? IsActive { get; set; }
    }

    // ---------------- Financial periods ----------------

    public class FinancialPeriodDto
    {
        public long Id { get; set; }
        public string FyLabel { get; set; } = null!;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreatePeriodRequest
    {
        /// <summary>e.g. "2026-27". Start/End default to 1 Apr – 31 Mar of that FY.</summary>
        [Required(ErrorMessage = "Financial year label is required (e.g. 2026-27).")]
        [RegularExpression(@"^\d{4}-\d{2}$", ErrorMessage = "FY label must look like 2026-27.")]
        public string FyLabel { get; set; } = null!;

        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
    }

    // ---------------- Vouchers ----------------

    public class VoucherLineInput
    {
        [Required] public long AccountId { get; set; }
        [Required] public DrCr DrCr { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Line amount must be greater than zero.")]
        public decimal Amount { get; set; }

        [MaxLength(500)]
        public string? LineNarration { get; set; }
    }

    public class CreateVoucherRequest
    {
        [Required] public VoucherType VoucherType { get; set; }
        [Required] public DateOnly Date { get; set; }

        [MaxLength(1000)]
        public string? Narration { get; set; }

        [Required]
        [MinLength(2, ErrorMessage = "A voucher needs at least two lines.")]
        public List<VoucherLineInput> Lines { get; set; } = new();
    }

    public class ReverseVoucherRequest
    {
        /// <summary>Reversal date — defaults to the original voucher's date.</summary>
        public DateOnly? Date { get; set; }

        [MaxLength(1000)]
        public string? Narration { get; set; }
    }

    public class VoucherLineDto
    {
        public long Id { get; set; }
        public long AccountId { get; set; }
        public string AccountCode { get; set; } = null!;
        public string AccountName { get; set; } = null!;
        public string DrCr { get; set; } = null!;
        public decimal Amount { get; set; }
        public string? LineNarration { get; set; }
    }

    public class VoucherDto
    {
        public long Id { get; set; }
        public string VoucherType { get; set; } = null!;
        public string VoucherNo { get; set; } = null!;
        public DateOnly Date { get; set; }
        public long PeriodId { get; set; }
        public string? Narration { get; set; }
        public string Status { get; set; } = null!;
        public string? SourceType { get; set; }
        public long? SourceId { get; set; }
        public long? ReversalOfId { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public List<VoucherLineDto> Lines { get; set; } = new();
    }

    public class VoucherListItemDto
    {
        public long Id { get; set; }
        public string VoucherType { get; set; } = null!;
        public string VoucherNo { get; set; } = null!;
        public DateOnly Date { get; set; }
        public string? Narration { get; set; }
        public string Status { get; set; } = null!;
        public decimal Amount { get; set; }
    }

    public class VoucherListResponse
    {
        public List<VoucherListItemDto> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }

    // ---------------- Reports ----------------

    public class DayBookEntryDto
    {
        public long VoucherId { get; set; }
        public string VoucherType { get; set; } = null!;
        public string VoucherNo { get; set; } = null!;
        public DateOnly Date { get; set; }
        public string AccountCode { get; set; } = null!;
        public string AccountName { get; set; } = null!;
        public string DrCr { get; set; } = null!;
        public decimal Amount { get; set; }
        public string? Narration { get; set; }
    }

    public class LedgerLineDto
    {
        public DateOnly Date { get; set; }
        public string VoucherType { get; set; } = null!;
        public string VoucherNo { get; set; } = null!;
        public string? Narration { get; set; }
        public decimal Dr { get; set; }
        public decimal Cr { get; set; }
        public decimal RunningBalance { get; set; }
    }

    public class AccountLedgerDto
    {
        public long AccountId { get; set; }
        public string AccountCode { get; set; } = null!;
        public string AccountName { get; set; } = null!;
        public decimal OpeningDr { get; set; }
        public decimal OpeningCr { get; set; }
        public List<LedgerLineDto> Lines { get; set; } = new();
        public decimal ClosingDr { get; set; }
        public decimal ClosingCr { get; set; }
    }

    public class TrialBalanceRowDto
    {
        public long AccountId { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string AccountType { get; set; } = null!;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    public class TrialBalanceDto
    {
        public DateOnly AsOf { get; set; }
        public List<TrialBalanceRowDto> Rows { get; set; } = new();
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public bool Balanced { get; set; }
    }
}
