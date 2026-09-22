using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    public class ReportService : IReportService
    {
        private readonly KTransportDbContext _context;

        public ReportService(KTransportDbContext context)
        {
            _context = context;
        }

        public async Task<BookingRegisterReportDto> GetBookingRegisterAsync(DateOnly? fromDate = null, DateOnly? toDate = null)
        {
            var query = _context.Shipments
                .Include(s => s.Items)
                .Include(s => s.ChargeItems)
                .AsNoTracking()
                .Where(s => s.IsActive);

            if (fromDate.HasValue) query = query.Where(s => s.ShipmentDate >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(s => s.ShipmentDate <= toDate.Value);

            var shipments = await query.OrderByDescending(s => s.ShipmentDate).ToListAsync();

            return new BookingRegisterReportDto
            {
                TotalBookings = shipments.Count,
                TotalFreightAmount = shipments.Sum(s => s.TotalFreight),
                TotalOtherCharges = shipments.Sum(s => s.TotalOtherCharges),
                TotalTaxAmount = shipments.Sum(s => s.TotalTaxAmount),
                TotalGrandTotal = shipments.Sum(s => s.GrandTotal),
                TotalPaidAmount = shipments.Sum(s => s.PaidAmount),
                TotalDueAmount = shipments.Sum(s => s.DueAmount),
                Records = shipments.Select(s => new ShipmentDto
                {
                    Id = s.Id,
                    TenantId = s.TenantId,
                    ShipmentNo = s.ShipmentNo,
                    InvoiceNo = s.InvoiceNo,
                    ShipmentDate = s.ShipmentDate,
                    InvoiceDate = s.InvoiceDate,
                    FromLocation = s.FromLocation,
                    ToLocation = s.ToLocation,
                    TruckNo = s.TruckNo,
                    TaxTreatment = s.TaxTreatment,
                    GstPaidBy = s.GstPaidBy,
                    ConsignorName = s.ConsignorName,
                    ConsignorGstNo = s.ConsignorGstNo,
                    ConsignorMobile = s.ConsignorMobile,
                    ConsignorAddress = s.ConsignorAddress,
                    ConsigneeName = s.ConsigneeName,
                    ConsigneeGstNo = s.ConsigneeGstNo,
                    ConsigneeMobile = s.ConsigneeMobile,
                    ConsigneeAddress = s.ConsigneeAddress,
                    GoodsValue = s.GoodsValue,
                    PaymentTerm = s.PaymentTerm,
                    TotalFreight = s.TotalFreight,
                    TotalOtherCharges = s.TotalOtherCharges,
                    TotalTaxAmount = s.TotalTaxAmount,
                    GrandTotal = s.GrandTotal,
                    PaidAmount = s.PaidAmount,
                    DueAmount = s.DueAmount,
                    Status = s.Status,
                    Remarks = s.Remarks,
                    BookingClerk = s.BookingClerk,
                    IsActive = s.IsActive,
                    CreatedAt = s.CreatedAt
                }).ToList()
            };
        }

        public async Task<TripProfitabilityReportDto> GetTripProfitabilityReportAsync(DateOnly? fromDate = null, DateOnly? toDate = null)
        {
            var query = _context.Trips
                .Include(t => t.Expenses)
                .AsNoTracking()
                .Where(t => t.IsActive);

            if (fromDate.HasValue) query = query.Where(t => t.TripDate >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(t => t.TripDate <= toDate.Value);

            var trips = await query.OrderByDescending(t => t.TripDate).ToListAsync();
            var lorryHires = await _context.LorryHireContracts.AsNoTracking().Where(l => l.IsActive).ToListAsync();

            var list = new List<TripProfitabilityItemDto>();

            foreach (var t in trips)
            {
                var hire = lorryHires.FirstOrDefault(l => l.TripId == t.Id);
                decimal vendorCost = hire?.TotalHireAmount ?? 0;
                decimal tripExpenses = t.Expenses?.Sum(e => e.Amount) ?? t.TotalExpenses;
                decimal totalCost = t.DriverAdvanceCash + t.DriverAdvanceFuel + tripExpenses + vendorCost;
                decimal profit = t.TotalFreightRevenue - totalCost;
                decimal marginPct = t.TotalFreightRevenue > 0 ? (profit / t.TotalFreightRevenue * 100m) : 0;

                list.Add(new TripProfitabilityItemDto
                {
                    TripId = t.Id,
                    TripNo = t.TripNo,
                    TripDate = t.TripDate,
                    VehicleNo = t.VehicleNo,
                    DriverName = t.DriverName,
                    OriginLocation = t.OriginLocationName,
                    DestinationLocation = t.DestinationLocationName,
                    Revenue = t.TotalFreightRevenue,
                    TotalCost = totalCost,
                    NetProfit = profit,
                    ProfitMarginPct = Math.Round(marginPct, 2)
                });
            }

            decimal totalRevenue = list.Sum(x => x.Revenue);
            decimal totalExpenses = trips.Sum(t => t.Expenses?.Sum(e => e.Amount) ?? t.TotalExpenses);
            decimal totalDriverAdvance = trips.Sum(t => t.DriverAdvanceCash);
            decimal totalDiesel = trips.Sum(t => t.DriverAdvanceFuel);
            decimal totalLorryHire = lorryHires.Sum(l => l.TotalHireAmount);
            decimal netProfit = totalRevenue - (totalDriverAdvance + totalDiesel + totalExpenses + totalLorryHire);
            decimal overallMargin = totalRevenue > 0 ? (netProfit / totalRevenue * 100m) : 0;

            return new TripProfitabilityReportDto
            {
                TotalTrips = trips.Count,
                TotalFreightRevenue = totalRevenue,
                TotalDriverCashAdvance = totalDriverAdvance,
                TotalDieselAdvance = totalDiesel,
                TotalOnRoadExpenses = totalExpenses,
                TotalLorryHireVendorCost = totalLorryHire,
                NetTripProfit = netProfit,
                ProfitMarginPercentage = Math.Round(overallMargin, 2),
                TripDetails = list
            };
        }

        public async Task<TaxSummaryReportDto> GetTaxSummaryReportAsync(DateOnly? fromDate = null, DateOnly? toDate = null)
        {
            var query = _context.Shipments.AsNoTracking().Where(s => s.IsActive);

            if (fromDate.HasValue) query = query.Where(s => s.ShipmentDate >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(s => s.ShipmentDate <= toDate.Value);

            var list = await query.ToListAsync();

            return new TaxSummaryReportDto
            {
                TotalShipmentsCount = list.Count,
                TotalTaxableFreight = list.Where(s => s.TaxTreatment == TaxTreatment.GST_Regular).Sum(s => s.TotalFreight),
                TotalNonTaxableFreight = list.Where(s => s.TaxTreatment == TaxTreatment.NonTaxable || s.TaxTreatment == TaxTreatment.Exempt).Sum(s => s.TotalFreight),
                TotalGstRcmFreight = list.Where(s => s.TaxTreatment == TaxTreatment.GST_RCM).Sum(s => s.TotalFreight),
                TotalTaxCollected = list.Sum(s => s.TotalTaxAmount)
            };
        }

        public async Task<List<PartyOutstandingReportDto>> GetPartyOutstandingReportAsync()
        {
            var parties = await _context.Parties.AsNoTracking().Where(p => p.IsActive).ToListAsync();
            var invoices = await _context.Invoices.AsNoTracking().Where(i => i.IsActive).ToListAsync();
            var shipments = await _context.Shipments.AsNoTracking().Where(s => s.IsActive && s.DueAmount > 0).ToListAsync();

            var result = new List<PartyOutstandingReportDto>();

            foreach (var p in parties)
            {
                var partyInvoices = invoices.Where(i => i.PartyId == p.Id).ToList();
                var partyShipments = shipments.Where(s => s.ConsignorPartyId == p.Id || s.ConsigneePartyId == p.Id).ToList();

                decimal invoiceBilled = partyInvoices.Sum(i => i.GrandTotal);
                decimal invoicePaid = partyInvoices.Sum(i => i.PaidAmount);
                decimal invoiceDue = partyInvoices.Sum(i => i.DueAmount);

                decimal unbilledShipmentDue = partyShipments.Where(s => s.InvoiceId == null).Sum(s => s.DueAmount);

                decimal totalDue = invoiceDue + unbilledShipmentDue;

                if (totalDue > 0 || invoiceBilled > 0)
                {
                    result.Add(new PartyOutstandingReportDto
                    {
                        PartyId = p.Id,
                        PartyName = p.Name,
                        Mobile = p.Mobile,
                        GstNo = p.GstNo,
                        TotalBilledAmount = invoiceBilled,
                        TotalPaidAmount = invoicePaid,
                        TotalOutstandingDue = totalDue,
                        PendingShipmentsCount = partyShipments.Count
                    });
                }
            }

            return result.OrderByDescending(x => x.TotalOutstandingDue).ToList();
        }

        public async Task<List<VendorPayableReportDto>> GetVendorPayableReportAsync()
        {
            var vendors = await _context.Vendors.AsNoTracking().Where(v => v.IsActive).ToListAsync();
            var contracts = await _context.LorryHireContracts.AsNoTracking().Where(c => c.IsActive).ToListAsync();

            var result = new List<VendorPayableReportDto>();

            foreach (var v in vendors)
            {
                var vendorContracts = contracts.Where(c => c.VendorId == v.Id).ToList();
                decimal totalHire = vendorContracts.Sum(c => c.TotalHireAmount);
                decimal totalAdvance = vendorContracts.Sum(c => c.AdvanceCashPaid + c.DieselAdvanceAmount);
                decimal totalTds = vendorContracts.Sum(c => c.TdsAmount);
                decimal totalBalance = vendorContracts.Sum(c => c.BalancePayable - c.PaidBalanceAmount);

                if (totalHire > 0 || totalBalance > 0)
                {
                    result.Add(new VendorPayableReportDto
                    {
                        VendorId = v.Id,
                        VendorName = v.Name,
                        Mobile = v.Mobile,
                        PanNo = v.PanNo,
                        TotalHireAmount = totalHire,
                        TotalAdvancePaid = totalAdvance,
                        TotalTdsDeducted = totalTds,
                        TotalBalancePayable = Math.Max(0, totalBalance),
                        ActiveContractsCount = vendorContracts.Count(c => c.PaymentStatus != "Paid")
                    });
                }
            }

            return result.OrderByDescending(x => x.TotalBalancePayable).ToList();
        }
    }
}
