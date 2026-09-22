using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;
using KTransport.API.Models;

namespace KTransport.API.Services
{
    public interface ICargoClaimService
    {
        Task<List<CargoClaimDto>> GetClaimsAsync(ClaimStatus? status = null, string? search = null);
        Task<CargoClaimDto?> GetClaimByIdAsync(long id);
        Task<CargoClaimDto> CreateClaimAsync(CreateClaimRequest request);
        Task<CargoClaimDto?> SettleClaimAsync(long id, SettleClaimRequest request);
        Task<bool> DeleteClaimAsync(long id);
    }
}
