using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;
using KTransport.API.Models;

namespace KTransport.API.Services
{
    public interface IVehicleInsuranceClaimService
    {
        Task<List<VehicleInsuranceClaimDto>> GetAllAsync(string? search = null, VehicleClaimStatus? status = null);
        Task<VehicleInsuranceClaimDto?> GetByIdAsync(long id);
        Task<VehicleInsuranceClaimDto> CreateAsync(CreateVehicleInsuranceClaimRequest request);
        Task<VehicleInsuranceClaimDto?> UpdateAsync(long id, UpdateVehicleInsuranceClaimRequest request);
        Task<bool> DeleteAsync(long id);
    }
}
