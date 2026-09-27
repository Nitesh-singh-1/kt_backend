using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;

namespace KTransport.API.Services
{
    public interface ISparePartService
    {
        Task<List<SparePartDto>> GetAllAsync(string? search = null, string? category = null, bool lowStockOnly = false);
        Task<SparePartDto?> GetByIdAsync(long id);
        Task<SparePartDto> CreateAsync(CreateSparePartRequest request);
        Task<SparePartDto?> UpdateAsync(long id, UpdateSparePartRequest request);
        Task<SparePartDto?> AdjustStockAsync(long id, SparePartStockMovementRequest request);
        Task<bool> DeleteAsync(long id);
    }
}
