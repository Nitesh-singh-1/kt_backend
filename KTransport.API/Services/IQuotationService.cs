using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;
using KTransport.API.Models;

namespace KTransport.API.Services
{
    public interface IQuotationService
    {
        Task<List<QuotationDto>> GetQuotationsAsync(string? search = null, QuotationStatus? status = null);
        Task<QuotationDto?> GetQuotationByIdAsync(long id);
        Task<QuotationDto> CreateQuotationAsync(CreateQuotationRequest request);
        Task<QuotationDto?> UpdateQuotationAsync(long id, UpdateQuotationRequest request);
        Task<QuotationDto?> UpdateStatusAsync(long id, UpdateQuotationStatusRequest request);
        Task<bool> DeleteQuotationAsync(long id);
    }
}
