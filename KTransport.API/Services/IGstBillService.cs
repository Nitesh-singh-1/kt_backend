using KTransport.API.Models;

namespace KTransport.API.Services
{
    public interface IGstBillService
    {
        Task<GstBillResponse> CreateGstBillAsync(CreateGstBillRequest request, int userId);
        Task<GstBillResponse> UpdateGstBillAsync(int id, UpdateGstBillRequest request, int userId);
        Task<GstBillResponse> GetGstBillByIdAsync(int id);
        Task<GstBillResponse> GetGstBillByGrNoAsync(string grNo);
        Task<GstBillListResponse> GetAllGstBillsAsync(int page = 1, int pageSize = 10);
        Task<GstBillResponse> DeleteGstBillAsync(int id, int userId);
    }
}