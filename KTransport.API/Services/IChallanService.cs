using KTransport.API.DTOs;

namespace KTransport.API.Services
{
    public interface IChallanService
    {
        Task<ChallanResponse> CreateChallanAsync(CreateChallanRequest request, int userId);
        Task<ChallanResponse> UpdateChallanAsync(long id, UpdateChallanRequest request, int userId);
        Task<ChallanResponse> GetChallanByIdAsync(long id);
        Task<ChallanListResponse> GetAllChallansAsync(int page = 1, int pageSize = 10);
        Task<ChallanResponse> DeleteChallanAsync(long id, int userId);
        Task<ChallanListResponse> SearchChallansAsync(string? searchTerm, DateOnly? startDate, DateOnly? endDate, int page = 1, int pageSize = 10);
    }
}