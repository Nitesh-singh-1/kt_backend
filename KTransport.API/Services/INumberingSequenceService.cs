using System.Threading.Tasks;

namespace KTransport.API.Services
{
    public interface INumberingSequenceService
    {
        Task<string> GetNextNumberAsync(string entityType, string? customPrefix = null);
    }
}
