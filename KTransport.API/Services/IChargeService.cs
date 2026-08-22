using KTransport.API.DTOs;

namespace KTransport.API.Services
{
    public interface IChargeService
    {
        Task<ChargeResponse> CreateChargeAsync(CreateChargeRequest request);
        Task<ChargeResponse> UpdateChargeAsync(int id, UpdateChargeRequest request);
        Task<ChargeResponse> GetChargeByIdAsync(int id);
        Task<ChargeResponse> GetChargeByBillIdAsync(int billId);
        Task<ChargeResponse> DeleteChargeAsync(int id);
    }
}