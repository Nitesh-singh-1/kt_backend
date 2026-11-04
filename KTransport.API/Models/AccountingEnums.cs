namespace KTransport.API.Models
{
    /// <summary>Top-level classification of a ledger account (drives P&L vs Balance-Sheet placement).</summary>
    public enum AccountType
    {
        Asset,
        Liability,
        Equity,
        Income,
        Expense
    }

    /// <summary>The side an amount sits on. Stored as the two-letter "DR"/"CR" via HasConversion&lt;string&gt;().</summary>
    public enum DrCr
    {
        DR,
        CR
    }

    /// <summary>The eight Tally accounting voucher types (ADR accounting-ledger-architecture.md §1).</summary>
    public enum VoucherType
    {
        Contra,
        Payment,
        Receipt,
        Journal,
        Sales,
        Purchase,
        CreditNote,
        DebitNote
    }

    /// <summary>Lifecycle of a voucher. Posted is immutable; a correction is a Reversed mirror.</summary>
    public enum VoucherStatus
    {
        Draft,
        Posted,
        Reversed
    }

    /// <summary>A financial year is Open (postable) or Closed (back-dated posting rejected).</summary>
    public enum PeriodStatus
    {
        Open,
        Closed
    }
}
