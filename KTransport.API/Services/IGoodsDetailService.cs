using KTransport.API.Models;

namespace KTransport.API.Services
{
    public interface IGoodsDetailService
    {
        Task<GoodsDetailResponse> CreateGoodsDetailAsync(CreateGoodsDetailRequest request);
        Task<GoodsDetailResponse> UpdateGoodsDetailAsync(int id, UpdateGoodsDetailRequest request);
        Task<GoodsDetailResponse> GetGoodsDetailByIdAsync(int id);
        Task<GoodsDetailListResponse> GetGoodsDetailsByBillIdAsync(int billId);
        Task<GoodsDetailResponse> DeleteGoodsDetailAsync(int id);
    }
}