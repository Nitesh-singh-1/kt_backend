using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;

namespace KTransport.API.Services
{
    public interface IRateCardService
    {
        Task<List<FreightRateCardDto>> GetRateCardsAsync(string? search = null, long? partyId = null);
        Task<FreightRateCardDto?> GetRateCardByIdAsync(long id);
        Task<FreightRateCardDto> CreateRateCardAsync(CreateRateCardRequest request);
        Task<FreightRateCardDto?> UpdateRateCardAsync(long id, CreateRateCardRequest request);
        Task<bool> DeleteRateCardAsync(long id);
        Task<CalculatedFreightResponse> CalculateFreightAsync(CalculateFreightRequest request);
    }
}
