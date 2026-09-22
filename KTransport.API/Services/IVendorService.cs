using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;

namespace KTransport.API.Services
{
    public interface IVendorService
    {
        // Vendor Master
        Task<List<VendorDto>> GetVendorsAsync(string? search = null);
        Task<List<VendorLookupDto>> GetVendorLookupAsync(string? query = null);
        Task<VendorDto?> GetVendorByIdAsync(long id);
        Task<VendorDto> CreateVendorAsync(CreateVendorRequest request);
        Task<VendorDto?> UpdateVendorAsync(long id, CreateVendorRequest request);
        Task<bool> DeleteVendorAsync(long id);

        // Lorry Hire Contracts / Memos
        Task<List<LorryHireContractDto>> GetLorryHireContractsAsync(string? search = null, long? vendorId = null);
        Task<LorryHireContractDto?> GetLorryHireContractByIdAsync(long id);
        Task<LorryHireContractDto> CreateLorryHireContractAsync(CreateLorryHireRequest request, int? userId = null);
        Task<LorryHireContractDto?> RecordLorryHirePaymentAsync(long contractId, RecordLorryHirePaymentRequest request, int? userId = null);
        Task<bool> DeleteLorryHireContractAsync(long id);
    }
}
