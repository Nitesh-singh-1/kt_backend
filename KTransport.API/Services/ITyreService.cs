using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;
using KTransport.API.Models;

namespace KTransport.API.Services
{
    public interface ITyreService
    {
        Task<List<TyreDto>> GetAllAsync(string? search = null, TyreStatus? status = null, long? vehicleId = null);
        Task<TyreDto?> GetByIdAsync(long id);
        Task<TyreDto> CreateAsync(CreateTyreRequest request);
        Task<TyreDto?> UpdateAsync(long id, UpdateTyreRequest request);
        Task<bool> DeleteAsync(long id);
    }
}
