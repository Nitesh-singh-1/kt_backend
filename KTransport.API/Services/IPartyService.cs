using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;
using KTransport.API.Models;

namespace KTransport.API.Services
{
    public interface IPartyService
    {
        Task<List<PartyDto>> GetPartiesAsync(string? search = null, PartyType? partyType = null, bool activeOnly = true);
        Task<List<PartyLookupDto>> GetPartyLookupAsync(string? query = null, PartyType? partyType = null);
        Task<PartyDto?> GetPartyByIdAsync(long id);
        Task<PartyDto> CreatePartyAsync(CreatePartyRequest request);
        Task<PartyDto?> UpdatePartyAsync(long id, UpdatePartyRequest request);
        Task<bool> DeletePartyAsync(long id);
    }
}
