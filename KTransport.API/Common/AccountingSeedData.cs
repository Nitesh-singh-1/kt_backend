using System.Collections.Generic;
using KTransport.API.Models;

namespace KTransport.API.Common
{
    /// <summary>
    /// The 28 Tally default groups (15 primary + 13 sub) seeded per tenant as the chart-of-accounts
    /// skeleton (ADR accounting-ledger-architecture.md §2 / domain research §2). Seeded as
    /// IsSystem=true, IsGroup=true — a tenant adds ledgers underneath but cannot rename/delete these.
    ///
    /// NOTE (ADR open item #4): verify names against TallyHelp "Default Groups" before production.
    /// </summary>
    public static class AccountingSeedData
    {
        public record SeedGroup(string Code, string Name, AccountType Type, DrCr Nature, string? ParentCode);

        public static readonly IReadOnlyList<SeedGroup> DefaultGroups = new List<SeedGroup>
        {
            // --- 9 primary, Balance Sheet (capital nature) ---
            new("1000", "Capital Account",        AccountType.Equity,    DrCr.CR, null),
            new("1100", "Current Assets",         AccountType.Asset,     DrCr.DR, null),
            new("2000", "Current Liabilities",    AccountType.Liability, DrCr.CR, null),
            new("1200", "Fixed Assets",           AccountType.Asset,     DrCr.DR, null),
            new("1300", "Investments",            AccountType.Asset,     DrCr.DR, null),
            new("2100", "Loans (Liability)",      AccountType.Liability, DrCr.CR, null),
            new("1400", "Misc. Expenses (Asset)", AccountType.Asset,     DrCr.DR, null),
            new("2900", "Suspense A/c",           AccountType.Liability, DrCr.CR, null),
            new("1500", "Branch / Divisions",     AccountType.Asset,     DrCr.DR, null),

            // --- 6 primary, Profit & Loss ---
            new("4000", "Sales Accounts",         AccountType.Income,    DrCr.CR, null),
            new("5000", "Purchase Accounts",      AccountType.Expense,   DrCr.DR, null),
            new("4100", "Direct Incomes",         AccountType.Income,    DrCr.CR, null),
            new("5100", "Direct Expenses",        AccountType.Expense,   DrCr.DR, null),
            new("4200", "Indirect Incomes",       AccountType.Income,    DrCr.CR, null),
            new("5200", "Indirect Expenses",      AccountType.Expense,   DrCr.DR, null),

            // --- 13 sub-groups (parent by code) ---
            new("1010", "Reserves & Surplus",       AccountType.Equity,    DrCr.CR, "1000"),
            new("1110", "Bank Accounts",            AccountType.Asset,     DrCr.DR, "1100"),
            new("1120", "Cash-in-hand",             AccountType.Asset,     DrCr.DR, "1100"),
            new("1130", "Deposits (Asset)",         AccountType.Asset,     DrCr.DR, "1100"),
            new("1140", "Loans & Advances (Asset)", AccountType.Asset,     DrCr.DR, "1100"),
            new("1150", "Stock-in-hand",            AccountType.Asset,     DrCr.DR, "1100"),
            new("1160", "Sundry Debtors",           AccountType.Asset,     DrCr.DR, "1100"),
            new("2010", "Duties & Taxes",           AccountType.Liability, DrCr.CR, "2000"),
            new("2020", "Provisions",               AccountType.Liability, DrCr.CR, "2000"),
            new("2030", "Sundry Creditors",         AccountType.Liability, DrCr.CR, "2000"),
            new("2110", "Bank OD A/c",              AccountType.Liability, DrCr.CR, "2100"),
            new("2120", "Secured Loans",            AccountType.Liability, DrCr.CR, "2100"),
            new("2130", "Unsecured Loans",          AccountType.Liability, DrCr.CR, "2100"),
        };
    }
}
