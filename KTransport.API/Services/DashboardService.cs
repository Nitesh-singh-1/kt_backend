using System.Globalization;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly ILogger<DashboardService> _logger;
        private readonly KTransportDbContext _context;

        public DashboardService(ILogger<DashboardService> logger, KTransportDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<DashboardResponse> GetDashboardStatsAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving dashboard statistics");

                var today = DateTime.Today;
                var todayDateOnly = DateOnly.FromDateTime(today);
                var startOfMonth = new DateTime(today.Year, today.Month, 1);
                var startOfMonthDateOnly = DateOnly.FromDateTime(startOfMonth);

                // Primary source: Modern multi-tenant Shipments
                var shipments = await _context.Shipments
                    .AsNoTracking()
                    .Where(s => s.IsActive && s.Status != ShipmentStatus.Cancelled)
                    .ToListAsync();

                if (shipments.Count > 0)
                {
                    var totalGrEntries = shipments.Count;
                    var pendingDeliveries = shipments.Count(s => s.Status != ShipmentStatus.Delivered);
                    var completedDeliveries = shipments.Count(s => s.Status == ShipmentStatus.Delivered);
                    var totalRevenue = shipments.Sum(s => s.GrandTotal);

                    var todayEntries = shipments.Count(s => s.ShipmentDate == todayDateOnly || s.CreatedAt.Date == today);
                    var thisMonthEntries = shipments.Count(s => s.ShipmentDate >= startOfMonthDateOnly || s.CreatedAt.Date >= startOfMonth);

                    var pendingAmount = shipments.Sum(s => s.DueAmount);
                    var collectedAmount = shipments.Sum(s => s.PaidAmount);

                    var recentBills = shipments
                        .OrderByDescending(s => s.CreatedAt)
                        .ThenByDescending(s => s.Id)
                        .Take(10)
                        .Select(s => new RecentBillDto
                        {
                            Id = s.Id,
                            GrNo = s.ShipmentNo,
                            ConsigneeName = s.ConsigneeName ?? "—",
                            FromLocation = s.FromLocation ?? "—",
                            ToLocation = s.ToLocation ?? "—",
                            DeliveryStatus = s.Status == ShipmentStatus.InTransit ? "In Transit" :
                                             s.Status == ShipmentStatus.OutForDelivery ? "Out For Delivery" :
                                             s.Status.ToString(),
                            TotalAmount = s.GrandTotal,
                            CreatedAt = s.CreatedAt
                        })
                        .ToList();

                    var statusBreakdown = shipments
                        .GroupBy(s => s.Status.ToString())
                        .Select(g => new DeliveryStatusCount
                        {
                            Status = g.Key,
                            Count = g.Count()
                        })
                        .OrderByDescending(x => x.Count)
                        .ToList();

                    var dashboardStats = new DashboardStats
                    {
                        TotalGrEntries = totalGrEntries,
                        PendingDeliveries = pendingDeliveries,
                        CompletedDeliveries = completedDeliveries,
                        TotalRevenue = totalRevenue,
                        TodayEntries = todayEntries,
                        ThisMonthEntries = thisMonthEntries,
                        PendingAmount = pendingAmount,
                        CollectedAmount = collectedAmount,
                        RecentBills = recentBills,
                        DeliveryStatusBreakdown = statusBreakdown
                    };

                    return new DashboardResponse
                    {
                        Success = true,
                        Message = "Dashboard statistics retrieved successfully",
                        Data = dashboardStats
                    };
                }

                // Fallback to legacy GST bills if no modern shipments exist
                var allBills = await _context.GstBills
                    .AsNoTracking()
                    .Where(b => b.IsActive == true)
                    .ToListAsync();

                // 1. Total GR Entries
                var legacyGrEntries = allBills.Count;

                // 2. Pending Deliveries (status != "Delivered" or "Completed")
                var legacyPendingDeliveries = allBills
                    .Count(b => b.DeliveryStatus != null && 
                           !b.DeliveryStatus.Equals("Delivered", StringComparison.OrdinalIgnoreCase) &&
                           !b.DeliveryStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase));

                // 3. Completed Deliveries (status = "Delivered" or "Completed")
                var legacyCompletedDeliveries = allBills
                    .Count(b => b.DeliveryStatus != null && 
                           (b.DeliveryStatus.Equals("Delivered", StringComparison.OrdinalIgnoreCase) ||
                            b.DeliveryStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase)));

                // 4. Total Revenue (sum of all total amounts)
                var legacyTotalRevenue = allBills.Sum(b => b.TotalAmount ?? 0);

                // Additional stats
                var legacyTodayEntries = allBills.Count(b => b.CreatedAt.HasValue && b.CreatedAt.Value.Date == today);
                var legacyThisMonthEntries = allBills.Count(b => b.CreatedAt.HasValue && b.CreatedAt.Value >= startOfMonth);

                // Pending and collected amounts
                var legacyPendingAmount = allBills.Sum(b => b.ToPay ?? 0);
                var legacyCollectedAmount = allBills.Sum(b => b.Paid ?? 0);

                // Recent bills (last 10)
                var legacyRecentBills = allBills
                    .OrderByDescending(b => b.CreatedAt)
                    .Take(10)
                    .Select(b => new RecentBillDto
                    {
                        Id = b.Id,
                        GrNo = b.GrNo,
                        ConsigneeName = b.ConsigneeName,
                        FromLocation = b.FromLocation,
                        ToLocation = b.ToLocation,
                        DeliveryStatus = b.DeliveryStatus,
                        TotalAmount = b.TotalAmount,
                        CreatedAt = b.CreatedAt
                    })
                    .ToList();

                // Delivery status breakdown
                var legacyStatusBreakdown = allBills
                    .GroupBy(b => b.DeliveryStatus ?? "Unknown")
                    .Select(g => new DeliveryStatusCount
                    {
                        Status = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList();

                var legacyDashboardStats = new DashboardStats
                {
                    TotalGrEntries = legacyGrEntries,
                    PendingDeliveries = legacyPendingDeliveries,
                    CompletedDeliveries = legacyCompletedDeliveries,
                    TotalRevenue = legacyTotalRevenue,
                    TodayEntries = legacyTodayEntries,
                    ThisMonthEntries = legacyThisMonthEntries,
                    PendingAmount = legacyPendingAmount,
                    CollectedAmount = legacyCollectedAmount,
                    RecentBills = legacyRecentBills,
                    DeliveryStatusBreakdown = legacyStatusBreakdown
                };

                return new DashboardResponse
                {
                    Success = true,
                    Message = "Dashboard statistics retrieved successfully",
                    Data = legacyDashboardStats
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dashboard statistics");
                return new DashboardResponse
                {
                    Success = false,
                    Message = "An error occurred while retrieving dashboard statistics"
                };
            }
        }

        public async Task<RevenueResponse> GetRevenueStatsAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving revenue statistics");

                var today = DateTime.Today;
                var todayDateOnly = DateOnly.FromDateTime(today);
                var startOfMonth = new DateTime(today.Year, today.Month, 1);
                var startOfMonthDateOnly = DateOnly.FromDateTime(startOfMonth);
                var startOfYear = new DateTime(today.Year, 1, 1);
                var startOfYearDateOnly = DateOnly.FromDateTime(startOfYear);

                var shipments = await _context.Shipments
                    .AsNoTracking()
                    .Where(s => s.IsActive && s.Status != ShipmentStatus.Cancelled)
                    .ToListAsync();

                if (shipments.Count > 0)
                {
                    var totalRevenue = shipments.Sum(s => s.GrandTotal);
                    var todayRevenue = shipments
                        .Where(s => s.ShipmentDate == todayDateOnly || s.CreatedAt.Date == today)
                        .Sum(s => s.GrandTotal);
                    var thisMonthRevenue = shipments
                        .Where(s => s.ShipmentDate >= startOfMonthDateOnly || s.CreatedAt.Date >= startOfMonth)
                        .Sum(s => s.GrandTotal);
                    var thisYearRevenue = shipments
                        .Where(s => s.ShipmentDate >= startOfYearDateOnly || s.CreatedAt.Date >= startOfYear)
                        .Sum(s => s.GrandTotal);

                    var paidAmount = shipments
                        .Where(s => s.PaymentTerm == PaymentTerm.Paid)
                        .Sum(s => s.GrandTotal);
                    var toPayAmount = shipments
                        .Where(s => s.PaymentTerm == PaymentTerm.ToPay)
                        .Sum(s => s.GrandTotal);
                    var tbbAmount = shipments
                        .Where(s => s.PaymentTerm == PaymentTerm.TBB)
                        .Sum(s => s.GrandTotal);

                    var revenueStats = new RevenueStats
                    {
                        TotalRevenue = totalRevenue,
                        TodayRevenue = todayRevenue,
                        ThisMonthRevenue = thisMonthRevenue,
                        ThisYearRevenue = thisYearRevenue,
                        PaidAmount = paidAmount,
                        ToPayAmount = toPayAmount,
                        TbbAmount = tbbAmount,
                        PaymentReceived = shipments.Sum(s => s.PaidAmount),
                        OutstandingDue = shipments.Sum(s => s.DueAmount)
                    };

                    return new RevenueResponse
                    {
                        Success = true,
                        Message = "Revenue statistics retrieved successfully",
                        Data = revenueStats
                    };
                }

                var allBills = await _context.GstBills
                    .AsNoTracking()
                    .Where(b => b.IsActive == true)
                    .ToListAsync();

                var legacyTotalRevenue = allBills.Sum(b => b.TotalAmount ?? 0);
                var legacyTodayRevenue = allBills
                    .Where(b => b.CreatedAt.HasValue && b.CreatedAt.Value.Date == today)
                    .Sum(b => b.TotalAmount ?? 0);
                var legacyThisMonthRevenue = allBills
                    .Where(b => b.CreatedAt.HasValue && b.CreatedAt.Value >= startOfMonth)
                    .Sum(b => b.TotalAmount ?? 0);
                var legacyThisYearRevenue = allBills
                    .Where(b => b.CreatedAt.HasValue && b.CreatedAt.Value >= startOfYear)
                    .Sum(b => b.TotalAmount ?? 0);

                var legacyPaidAmount = allBills.Sum(b => b.Paid ?? 0);
                var legacyToPayAmount = allBills.Sum(b => b.ToPay ?? 0);
                var legacyTbbAmount = allBills.Sum(b => b.Tbb ?? 0);

                var legacyRevenueStats = new RevenueStats
                {
                    TotalRevenue = legacyTotalRevenue,
                    TodayRevenue = legacyTodayRevenue,
                    ThisMonthRevenue = legacyThisMonthRevenue,
                    ThisYearRevenue = legacyThisYearRevenue,
                    PaidAmount = legacyPaidAmount,
                    ToPayAmount = legacyToPayAmount,
                    TbbAmount = legacyTbbAmount,
                    PaymentReceived = legacyPaidAmount,
                    OutstandingDue = legacyToPayAmount
                };

                return new RevenueResponse
                {
                    Success = true,
                    Message = "Revenue statistics retrieved successfully",
                    Data = legacyRevenueStats
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving revenue statistics");
                return new RevenueResponse
                {
                    Success = false,
                    Message = "An error occurred while retrieving revenue statistics"
                };
            }
        }

        public async Task<BusinessAnalyticsDto> GetBusinessAnalyticsAsync(int months = 6)
        {
            if (months < 1) months = 1;
            if (months > 24) months = 24;

            var today = DateTime.Today;
            var startMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-(months - 1));
            var startDate = DateOnly.FromDateTime(startMonth);
            var endDate = DateOnly.FromDateTime(today);

            // Pull the window into memory (bounded by the selected months) so we can group by month in C#.
            var invoices = await _context.Invoices
                .AsNoTracking()
                .Where(i => i.IsActive && i.InvoiceDate >= startDate && i.InvoiceDate <= endDate)
                .Select(i => new
                {
                    i.InvoiceDate,
                    i.PartyName,
                    i.GrandTotal,
                    i.PaidAmount,
                    i.DueAmount,
                    i.PaymentStatus,
                    i.PaymentMode
                })
                .ToListAsync();

            var live = invoices.Where(i => i.PaymentStatus != InvoicePaymentStatus.Cancelled).ToList();

            var totalBilled = live.Sum(i => i.GrandTotal);
            var totalCollected = live.Sum(i => i.PaidAmount);
            var totalOutstanding = live.Sum(i => i.DueAmount);

            var expenseRows = await _context.TripExpenses
                .AsNoTracking()
                .Where(e => e.ExpenseDate >= startDate && e.ExpenseDate <= endDate)
                .Select(e => new { e.Amount, e.PaymentMode })
                .ToListAsync();

            var operatingExpenses = expenseRows.Sum(e => e.Amount);
            var grossMargin = totalBilled - operatingExpenses;

            // Monthly trend (billed vs collected) for each month in the window.
            var trend = new List<MonthlyTrendPointDto>();
            for (var m = 0; m < months; m++)
            {
                var month = startMonth.AddMonths(m);
                var monthInvoices = live.Where(i => i.InvoiceDate.Year == month.Year && i.InvoiceDate.Month == month.Month).ToList();
                trend.Add(new MonthlyTrendPointDto
                {
                    Month = month.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                    Label = month.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                    Billed = monthInvoices.Sum(i => i.GrandTotal),
                    Collected = monthInvoices.Sum(i => i.PaidAmount)
                });
            }

            var topCustomers = live
                .GroupBy(i => string.IsNullOrWhiteSpace(i.PartyName) ? "Unknown Party" : i.PartyName!)
                .Select(g => new TopCustomerDto
                {
                    Name = g.Key,
                    Billed = g.Sum(x => x.GrandTotal),
                    Collected = g.Sum(x => x.PaidAmount),
                    Outstanding = g.Sum(x => x.DueAmount),
                    InvoiceCount = g.Count()
                })
                .OrderByDescending(c => c.Billed)
                .Take(5)
                .ToList();

            var statusBreakdown = live
                .GroupBy(i => i.PaymentStatus)
                .Select(g => new StatusAmountDto
                {
                    Status = g.Key.ToString(),
                    Count = g.Count(),
                    Amount = g.Sum(x => x.GrandTotal)
                })
                .OrderBy(s => s.Status)
                .ToList();

            // Vehicle-wise profitability from trips (revenue vs expenses incl. driver advances) in the window.
            var trips = await _context.Trips
                .AsNoTracking()
                .Where(t => t.IsActive && t.TripDate >= startDate && t.TripDate <= endDate)
                .Select(t => new
                {
                    t.VehicleNo,
                    t.OriginLocationName,
                    t.TotalFreightRevenue,
                    t.TotalExpenses,
                    t.DriverAdvanceCash,
                    t.DriverAdvanceFuel
                })
                .ToListAsync();

            var vehicleProfitability = trips
                .GroupBy(t => string.IsNullOrWhiteSpace(t.VehicleNo) ? "Unassigned" : t.VehicleNo!)
                .Select(g =>
                {
                    var revenue = g.Sum(x => x.TotalFreightRevenue);
                    var expenses = g.Sum(x => x.TotalExpenses + x.DriverAdvanceCash + x.DriverAdvanceFuel);
                    var profit = revenue - expenses;
                    return new VehicleProfitDto
                    {
                        VehicleNo = g.Key,
                        Trips = g.Count(),
                        Revenue = revenue,
                        Expenses = expenses,
                        Profit = profit,
                        MarginPct = revenue > 0 ? Math.Round(profit / revenue * 100m, 1) : 0m
                    };
                })
                .OrderByDescending(v => v.Revenue)
                .Take(10)
                .ToList();

            // Branch-wise profitability (trips grouped by originating branch/location).
            var branchProfitability = trips
                .GroupBy(t => string.IsNullOrWhiteSpace(t.OriginLocationName) ? "Unassigned" : t.OriginLocationName!)
                .Select(g =>
                {
                    var revenue = g.Sum(x => x.TotalFreightRevenue);
                    var expenses = g.Sum(x => x.TotalExpenses + x.DriverAdvanceCash + x.DriverAdvanceFuel);
                    var profit = revenue - expenses;
                    return new BranchProfitDto
                    {
                        Branch = g.Key,
                        Trips = g.Count(),
                        Revenue = revenue,
                        Expenses = expenses,
                        Profit = profit,
                        MarginPct = revenue > 0 ? Math.Round(profit / revenue * 100m, 1) : 0m
                    };
                })
                .OrderByDescending(b => b.Revenue)
                .Take(10)
                .ToList();

            // Cash & bank flow: inflow from invoice collections by mode, outflow from trip expenses by mode.
            var inflowByMode = live
                .Where(i => i.PaidAmount > 0)
                .GroupBy(i => string.IsNullOrWhiteSpace(i.PaymentMode) ? "Unspecified" : i.PaymentMode!)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.PaidAmount));

            var outflowByMode = expenseRows
                .GroupBy(e => string.IsNullOrWhiteSpace(e.PaymentMode) ? "Unspecified" : e.PaymentMode!)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            var cashFlowByMode = inflowByMode.Keys.Union(outflowByMode.Keys)
                .Select(mode =>
                {
                    inflowByMode.TryGetValue(mode, out var inf);
                    outflowByMode.TryGetValue(mode, out var outf);
                    return new CashFlowModeDto { Mode = mode, Inflow = inf, Outflow = outf, Net = inf - outf };
                })
                .OrderByDescending(c => c.Inflow + c.Outflow)
                .ToList();

            var totalInflow = inflowByMode.Values.Sum();
            var totalOutflow = outflowByMode.Values.Sum();

            return new BusinessAnalyticsDto
            {
                Months = months,
                PeriodLabel = $"{startMonth:MMM yyyy} – {today:MMM yyyy}",
                TotalBilled = totalBilled,
                TotalCollected = totalCollected,
                TotalOutstanding = totalOutstanding,
                InvoiceCount = live.Count,
                CancelledCount = invoices.Count - live.Count,
                CollectionRatePct = totalBilled > 0 ? Math.Round(totalCollected / totalBilled * 100m, 1) : 0m,
                OperatingExpenses = operatingExpenses,
                GrossMargin = grossMargin,
                GrossMarginPct = totalBilled > 0 ? Math.Round(grossMargin / totalBilled * 100m, 1) : 0m,
                TotalInflow = totalInflow,
                TotalOutflow = totalOutflow,
                NetCashFlow = totalInflow - totalOutflow,
                MonthlyTrend = trend,
                TopCustomers = topCustomers,
                PaymentStatusBreakdown = statusBreakdown,
                VehicleProfitability = vehicleProfitability,
                BranchProfitability = branchProfitability,
                CashFlowByMode = cashFlowByMode
            };
        }

        public async Task<DashboardResponse> GetDashboardStatsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                _logger.LogInformation("Retrieving dashboard statistics for date range: {StartDate} to {EndDate}", startDate, endDate);

                var start = DateOnly.FromDateTime(startDate.Date);
                var end = DateOnly.FromDateTime(endDate.Date);

                var shipmentsInRange = await _context.Shipments
                    .AsNoTracking()
                    .Where(s => s.IsActive && s.Status != ShipmentStatus.Cancelled &&
                        ((s.ShipmentDate >= start && s.ShipmentDate <= end) ||
                         (s.CreatedAt.Date >= startDate.Date && s.CreatedAt.Date <= endDate.Date)))
                    .ToListAsync();

                if (shipmentsInRange.Count > 0)
                {
                    var totalGrEntries = shipmentsInRange.Count;
                    var pendingDeliveries = shipmentsInRange.Count(s => s.Status != ShipmentStatus.Delivered);
                    var completedDeliveries = shipmentsInRange.Count(s => s.Status == ShipmentStatus.Delivered);
                    var totalRevenue = shipmentsInRange.Sum(s => s.GrandTotal);
                    var pendingAmount = shipmentsInRange.Sum(s => s.DueAmount);
                    var collectedAmount = shipmentsInRange.Sum(s => s.PaidAmount);

                    var recentBills = shipmentsInRange
                        .OrderByDescending(s => s.CreatedAt)
                        .ThenByDescending(s => s.Id)
                        .Take(10)
                        .Select(s => new RecentBillDto
                        {
                            Id = s.Id,
                            GrNo = s.ShipmentNo,
                            ConsigneeName = s.ConsigneeName ?? "—",
                            FromLocation = s.FromLocation ?? "—",
                            ToLocation = s.ToLocation ?? "—",
                            DeliveryStatus = s.Status == ShipmentStatus.InTransit ? "In Transit" :
                                             s.Status == ShipmentStatus.OutForDelivery ? "Out For Delivery" :
                                             s.Status.ToString(),
                            TotalAmount = s.GrandTotal,
                            CreatedAt = s.CreatedAt
                        })
                        .ToList();

                    var statusBreakdown = shipmentsInRange
                        .GroupBy(s => s.Status.ToString())
                        .Select(g => new DeliveryStatusCount
                        {
                            Status = g.Key,
                            Count = g.Count()
                        })
                        .OrderByDescending(x => x.Count)
                        .ToList();

                    var dashboardStats = new DashboardStats
                    {
                        TotalGrEntries = totalGrEntries,
                        PendingDeliveries = pendingDeliveries,
                        CompletedDeliveries = completedDeliveries,
                        TotalRevenue = totalRevenue,
                        TodayEntries = 0,
                        ThisMonthEntries = 0,
                        PendingAmount = pendingAmount,
                        CollectedAmount = collectedAmount,
                        RecentBills = recentBills,
                        DeliveryStatusBreakdown = statusBreakdown
                    };

                    return new DashboardResponse
                    {
                        Success = true,
                        Message = $"Dashboard statistics for {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd} retrieved successfully",
                        Data = dashboardStats
                    };
                }

                var billsInRange = await _context.GstBills
                    .AsNoTracking()
                    .Where(b => b.IsActive == true && 
                           b.CreatedAt.HasValue && 
                           b.CreatedAt.Value.Date >= startDate.Date && 
                           b.CreatedAt.Value.Date <= endDate.Date)
                    .ToListAsync();

                var legacyGrEntries = billsInRange.Count;

                var legacyPendingDeliveries = billsInRange
                    .Count(b => b.DeliveryStatus != null && 
                           !b.DeliveryStatus.Equals("Delivered", StringComparison.OrdinalIgnoreCase) &&
                           !b.DeliveryStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase));

                var legacyCompletedDeliveries = billsInRange
                    .Count(b => b.DeliveryStatus != null && 
                           (b.DeliveryStatus.Equals("Delivered", StringComparison.OrdinalIgnoreCase) ||
                            b.DeliveryStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase)));

                var legacyTotalRevenue = billsInRange.Sum(b => b.TotalAmount ?? 0);
                var legacyPendingAmount = billsInRange.Sum(b => b.ToPay ?? 0);
                var legacyCollectedAmount = billsInRange.Sum(b => b.Paid ?? 0);

                var legacyRecentBills = billsInRange
                    .OrderByDescending(b => b.CreatedAt)
                    .Take(10)
                    .Select(b => new RecentBillDto
                    {
                        Id = b.Id,
                        GrNo = b.GrNo,
                        ConsigneeName = b.ConsigneeName,
                        FromLocation = b.FromLocation,
                        ToLocation = b.ToLocation,
                        DeliveryStatus = b.DeliveryStatus,
                        TotalAmount = b.TotalAmount,
                        CreatedAt = b.CreatedAt
                    })
                    .ToList();

                var legacyStatusBreakdown = billsInRange
                    .GroupBy(b => b.DeliveryStatus ?? "Unknown")
                    .Select(g => new DeliveryStatusCount
                    {
                        Status = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList();

                var legacyDashboardStats = new DashboardStats
                {
                    TotalGrEntries = legacyGrEntries,
                    PendingDeliveries = legacyPendingDeliveries,
                    CompletedDeliveries = legacyCompletedDeliveries,
                    TotalRevenue = legacyTotalRevenue,
                    TodayEntries = 0,
                    ThisMonthEntries = 0,
                    PendingAmount = legacyPendingAmount,
                    CollectedAmount = legacyCollectedAmount,
                    RecentBills = legacyRecentBills,
                    DeliveryStatusBreakdown = legacyStatusBreakdown
                };

                return new DashboardResponse
                {
                    Success = true,
                    Message = $"Dashboard statistics for {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd} retrieved successfully",
                    Data = legacyDashboardStats
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dashboard statistics by date range");
                return new DashboardResponse
                {
                    Success = false,
                    Message = "An error occurred while retrieving dashboard statistics"
                };
            }
        }
    }
}