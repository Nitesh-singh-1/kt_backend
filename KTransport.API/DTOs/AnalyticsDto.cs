using System.Collections.Generic;

namespace KTransport.API.DTOs
{
    /// <summary>Business analytics / KPI snapshot over a rolling window of months.</summary>
    public class BusinessAnalyticsDto
    {
        public int Months { get; set; }
        public string PeriodLabel { get; set; } = null!;

        // Sales vs recovery
        public decimal TotalBilled { get; set; }
        public decimal TotalCollected { get; set; }
        public decimal TotalOutstanding { get; set; }
        public int InvoiceCount { get; set; }
        public int CancelledCount { get; set; }
        /// <summary>Collected / Billed as a percentage (0–100).</summary>
        public decimal CollectionRatePct { get; set; }

        // Profitability
        public decimal OperatingExpenses { get; set; }
        public decimal GrossMargin { get; set; }
        public decimal GrossMarginPct { get; set; }

        // Cash & bank flow
        public decimal TotalInflow { get; set; }
        public decimal TotalOutflow { get; set; }
        public decimal NetCashFlow { get; set; }

        public List<MonthlyTrendPointDto> MonthlyTrend { get; set; } = new();
        public List<TopCustomerDto> TopCustomers { get; set; } = new();
        public List<StatusAmountDto> PaymentStatusBreakdown { get; set; } = new();
        public List<VehicleProfitDto> VehicleProfitability { get; set; } = new();
        public List<BranchProfitDto> BranchProfitability { get; set; } = new();
        public List<CashFlowModeDto> CashFlowByMode { get; set; } = new();
    }

    public class BranchProfitDto
    {
        public string Branch { get; set; } = null!;
        public int Trips { get; set; }
        public decimal Revenue { get; set; }
        public decimal Expenses { get; set; }
        public decimal Profit { get; set; }
        public decimal MarginPct { get; set; }
    }

    public class CashFlowModeDto
    {
        public string Mode { get; set; } = null!;
        public decimal Inflow { get; set; }
        public decimal Outflow { get; set; }
        public decimal Net { get; set; }
    }

    public class VehicleProfitDto
    {
        public string VehicleNo { get; set; } = null!;
        public int Trips { get; set; }
        public decimal Revenue { get; set; }
        public decimal Expenses { get; set; }
        public decimal Profit { get; set; }
        /// <summary>Profit / Revenue as a percentage (0–100); can be negative.</summary>
        public decimal MarginPct { get; set; }
    }

    public class MonthlyTrendPointDto
    {
        public string Month { get; set; } = null!;  // "yyyy-MM"
        public string Label { get; set; } = null!;  // "Apr 2026"
        public decimal Billed { get; set; }
        public decimal Collected { get; set; }
    }

    public class TopCustomerDto
    {
        public string Name { get; set; } = null!;
        public decimal Billed { get; set; }
        public decimal Collected { get; set; }
        public decimal Outstanding { get; set; }
        public int InvoiceCount { get; set; }
    }

    public class StatusAmountDto
    {
        public string Status { get; set; } = null!;
        public int Count { get; set; }
        public decimal Amount { get; set; }
    }
}
