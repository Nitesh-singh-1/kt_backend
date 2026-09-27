using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;

namespace KTransport.API.Services
{
    public interface IVendorRateContractService
    {
        Task<List<VendorRateContractDto>> GetAllAsync(string? search = null, long? vendorId = null);
        Task<VendorRateContractDto?> GetByIdAsync(long id);
        Task<VendorRateContractDto> CreateAsync(CreateVendorRateContractRequest request);
        Task<VendorRateContractDto?> UpdateAsync(long id, UpdateVendorRateContractRequest request);
        Task<bool> DeleteAsync(long id);
    }
}
