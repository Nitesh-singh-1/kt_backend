using System.Collections.Generic;
using KTransport.API.Models;

namespace KTransport.API.Common
{
    /// <summary>
    /// Phase 2 auto-posting well-known LEAF ledgers (and the one Driver-Advances sub-group),
    /// seeded per tenant under the Phase 1 Tally groups (ADR accounting-autoposting-phase2.md Q3).
    /// Seeded as <c>IsSystem = true</c> so a tenant cannot rename/delete them; the posting engine
    /// resolves accounts by the stable well-known KEY -> <see cref="Account.Code"/> (NOT by name).
    /// The ledger <c>Code</c> IS the well-known key, so the mapping is data, not code.
    ///
    /// This registry is the single source of truth, used by BOTH the data migration (existing-module
    /// tenants) and the runtime idempotent seeder (new tenants). Per-party/vendor/driver sub-ledgers
    /// are NOT here — they are auto-created on demand keyed by (parent group, SourcePartyId).
    /// </summary>
    public static class WellKnownLedgers
    {
        /// <param name="Key">Stable well-known key == the seeded account Code.</param>
        /// <param name="Name">Display name.</param>
        /// <param name="ParentGroupCode">Code of the Phase 1 system group this sits under.</param>
        /// <param name="IsGroup">true only for DRIVER_ADVANCES (a sub-group holding per-driver ledgers).</param>
        public record SeedLedger(string Key, string Name, string ParentGroupCode, bool IsGroup = false);

        // Well-known keys (also the account Code) — referenced by the posting engine.
        public const string FreightIncome = "FREIGHT_INCOME";
        public const string OtherChargesIncome = "OTHER_CHARGES_INCOME";
        public const string DiscountAllowed = "DISCOUNT_ALLOWED";
        public const string OutputGst = "OUTPUT_GST";
        public const string InputGst = "INPUT_GST";
        public const string Tds194CPayable = "TDS_194C_PAYABLE";
        public const string Cash = "CASH";
        public const string Bank = "BANK";
        public const string LorryHireExpense = "LORRY_HIRE_EXPENSE";
        public const string VendorDeductionsIncome = "VENDOR_DEDUCTIONS_INCOME";
        public const string DriverAdvances = "DRIVER_ADVANCES"; // group
        public const string WalkinDebtor = "WALKIN_DEBTOR";

        public const string FuelExpense = "FUEL_EXPENSE";
        public const string TollExpense = "TOLL_EXPENSE";
        public const string DriverAllowanceExpense = "DRIVER_ALLOWANCE_EXPENSE";
        public const string PoliceKharchaExpense = "POLICE_KHARCHA_EXPENSE";
        public const string VehicleRepairExpense = "VEHICLE_REPAIR_EXPENSE";
        public const string LoadingExpense = "LOADING_EXPENSE";
        public const string UnloadingExpense = "UNLOADING_EXPENSE";
        public const string ParkingExpense = "PARKING_EXPENSE";
        public const string MiscExpense = "MISC_EXPENSE";

        // Parent group codes from AccountingSeedData.DefaultGroups.
        private const string GSales = "4000";          // Sales Accounts
        private const string GDirectIncome = "4100";   // Direct Incomes
        private const string GIndirectIncome = "4200"; // Indirect Incomes
        private const string GDirectExpense = "5100";  // Direct Expenses
        private const string GIndirectExpense = "5200";// Indirect Expenses
        private const string GDutiesTaxes = "2010";    // Duties & Taxes
        private const string GCashInHand = "1120";     // Cash-in-hand
        private const string GBankAccounts = "1110";   // Bank Accounts
        private const string GLoansAdvances = "1140";  // Loans & Advances (Asset)
        private const string GSundryDebtors = "1160";  // Sundry Debtors

        public static readonly IReadOnlyList<SeedLedger> All = new List<SeedLedger>
        {
            new(FreightIncome,          "Freight Income",                 GSales),
            new(OtherChargesIncome,     "Other Charges Income",           GDirectIncome),
            new(DiscountAllowed,        "Discount Allowed",               GIndirectExpense),
            new(OutputGst,              "Output GST",                     GDutiesTaxes),
            new(InputGst,               "Input GST (ITC)",                GDutiesTaxes),  // reserved; Phase 4
            new(Tds194CPayable,         "TDS 194C Payable",               GDutiesTaxes),
            new(Cash,                   "Cash",                           GCashInHand),
            new(Bank,                   "Bank",                           GBankAccounts),
            new(LorryHireExpense,       "Lorry Hire Expense",             GDirectExpense),
            new(VendorDeductionsIncome, "Vendor Deductions / Other Income", GIndirectIncome),
            new(WalkinDebtor,           "Walk-in / Cash Customers",       GSundryDebtors),
            new(DriverAdvances,         "Driver Advances",                GLoansAdvances, IsGroup: true),

            // One head per TripExpenseType (ADR Q3): direct vs indirect expense placement.
            new(FuelExpense,            "Fuel Expense",                   GDirectExpense),
            new(TollExpense,            "Toll Expense",                   GDirectExpense),
            new(DriverAllowanceExpense, "Driver Allowance Expense",       GDirectExpense),
            new(LoadingExpense,         "Loading Expense",                GDirectExpense),
            new(UnloadingExpense,       "Unloading Expense",              GDirectExpense),
            new(ParkingExpense,         "Parking Expense",                GDirectExpense),
            new(PoliceKharchaExpense,   "Police Kharcha Expense",         GIndirectExpense),
            new(VehicleRepairExpense,   "Vehicle Repair Expense",         GIndirectExpense),
            new(MiscExpense,            "Miscellaneous Expense",          GIndirectExpense),
        };

        /// <summary>Maps a trip <see cref="TripExpenseType"/> to its well-known expense-head key.</summary>
        public static string ForExpenseType(TripExpenseType type) => type switch
        {
            TripExpenseType.Fuel => FuelExpense,
            TripExpenseType.Toll => TollExpense,
            TripExpenseType.DriverAllowance => DriverAllowanceExpense,
            TripExpenseType.PoliceKharcha => PoliceKharchaExpense,
            TripExpenseType.VehicleRepair => VehicleRepairExpense,
            TripExpenseType.LoadingCharges => LoadingExpense,
            TripExpenseType.UnloadingCharges => UnloadingExpense,
            TripExpenseType.Parking => ParkingExpense,
            _ => MiscExpense,
        };

        /// <summary>
        /// Payment-mode -> cash/bank well-known key (ADR Q3 configurable map). "Driver" is handled
        /// by the caller (credits the driver's advance sub-ledger), so it is not resolved here.
        /// Unknown modes fall back to CASH; the caller flags the queue row for review.
        /// </summary>
        public static bool TryMapPaymentModeToLedger(string? mode, out string key)
        {
            key = Cash;
            if (string.IsNullOrWhiteSpace(mode)) return true; // default cash, recognised
            switch (mode.Trim().ToLowerInvariant())
            {
                case "cash":
                    key = Cash; return true;
                case "card":
                case "upi":
                case "fastag":
                case "neft":
                case "rtgs":
                case "cheque":
                case "bank":
                case "banktransfer":
                case "bank_transfer":
                case "net banking":
                case "netbanking":
                    key = Bank; return true;
                default:
                    key = Cash; return false; // unknown -> cash + flag
            }
        }
    }
}
