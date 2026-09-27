using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;

namespace KTransport.API.Services
{
    public interface IEmptyTripLogService
    {
        Task<List<EmptyTripLogDto>> GetAllAsync(string? search = null);
        Task<EmptyTripLogDto?> GetByIdAsync(long id);
        Task<EmptyTripLogDto> CreateAsync(CreateEmptyTripLogRequest request);
        Task<EmptyTripLogDto?> UpdateAsync(long id, UpdateEmptyTripLogRequest request);
        Task<bool> DeleteAsync(long id);
    }
}
