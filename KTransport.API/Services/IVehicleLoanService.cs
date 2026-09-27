using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;

namespace KTransport.API.Services
{
    public interface IVehicleLoanService
    {
        Task<List<VehicleLoanDto>> GetAllAsync(string? search = null, bool activeOnly = false);
        Task<VehicleLoanDto?> GetByIdAsync(long id);
        Task<VehicleLoanDto> CreateAsync(CreateVehicleLoanRequest request);
        Task<VehicleLoanDto?> UpdateAsync(long id, UpdateVehicleLoanRequest request);
        Task<VehicleLoanDto?> RecordEmiPaidAsync(long id);
        Task<bool> DeleteAsync(long id);
    }
}
