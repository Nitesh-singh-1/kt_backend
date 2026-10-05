using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;
using KTransport.API.Models;

namespace KTransport.API.Services
{
    public interface IInvoiceService
    {
        Task<List<InvoiceDto>> GetInvoicesAsync(string? search = null, InvoicePaymentStatus? paymentStatus = null, long? partyId = null);
        Task<List<InvoiceLookupDto>> GetInvoiceLookupAsync(string? query = null, long? partyId = null);
        Task<InvoiceDto?> GetInvoiceByIdAsync(long id);
        Task<InvoiceDto> CreateInvoiceAsync(CreateInvoiceRequest request, int? userId = null);
        Task<InvoiceDto?> UpdateInvoiceAsync(long id, UpdateInvoiceRequest request, int? userId = null);
        Task<InvoiceDto?> RecordPaymentAsync(long id, RecordPaymentRequest request, int? userId = null);
        Task<bool> VoidInvoiceAsync(long id, int? userId = null);

        // Bulk billing & Bill Book of unbilled consignments
        Task<List<UnbilledShipmentDto>> GetUnbilledShipmentsAsync(string? search = null);
        Task<List<PartyUnbilledSummaryDto>> GetUnbilledPartiesSummaryAsync(string? search = null);
        Task<List<UnbilledShipmentDto>> GetUnbilledShipmentsByPartyAsync(string? partyName = null, long? partyId = null);
        Task<BulkBillResultDto> BulkBillAsync(BulkBillRequest request, int? userId = null);
        Task<BillBookInvoiceResponseDto> CreateBillBookInvoiceAsync(CreateBillBookRequestDto request, int? userId = null);
    }
}
