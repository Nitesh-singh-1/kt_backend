using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;

namespace KTransport.API.Services
{
    public interface IReportService
    {
        Task<BookingRegisterReportDto> GetBookingRegisterAsync(DateOnly? fromDate = null, DateOnly? toDate = null);
        Task<TripProfitabilityReportDto> GetTripProfitabilityReportAsync(DateOnly? fromDate = null, DateOnly? toDate = null);
        Task<TaxSummaryReportDto> GetTaxSummaryReportAsync(DateOnly? fromDate = null, DateOnly? toDate = null);
        Task<List<PartyOutstandingReportDto>> GetPartyOutstandingReportAsync();
        Task<List<VendorPayableReportDto>> GetVendorPayableReportAsync();
    }
}
