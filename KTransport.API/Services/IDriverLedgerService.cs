using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;

namespace KTransport.API.Services
{
    public interface IDriverLedgerService
    {
        Task<List<DriverLedgerEntryDto>> GetEntriesAsync(string? search = null, string? driverName = null);
        Task<List<DriverOutstandingDto>> GetOutstandingAsync();
        Task<DriverLedgerEntryDto> CreateAsync(CreateDriverLedgerEntryRequest request);
        Task<DriverLedgerEntryDto?> UpdateAsync(long id, UpdateDriverLedgerEntryRequest request);
        Task<bool> DeleteAsync(long id);
    }
}
