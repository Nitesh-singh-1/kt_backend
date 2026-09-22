using System.Threading.Tasks;
using KTransport.API.DTOs;

namespace KTransport.API.Services
{
    public interface ITrackingService
    {
        Task<PublicTrackingDto?> GetTrackingInfoAsync(string trackingNo);
    }
}
