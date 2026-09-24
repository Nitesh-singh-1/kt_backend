using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;

namespace KTransport.API.Services
{
    public interface IManifestService
    {
        Task<ManifestResponse> CreateManifestAsync(CreateManifestRequest request, int userId);
        Task<ManifestResponse> UpdateManifestAsync(long id, UpdateManifestRequest request, int userId);
        Task<ManifestResponse> GetManifestByIdAsync(long id);
        Task<ManifestResponse> GetManifestByNoAsync(string manifestNo);
        Task<ManifestListResponse> GetManifestsAsync(ManifestFilterRequest filter);
        Task<ManifestResponse> AssignTripAsync(long manifestId, long tripId, int userId);
        Task<ManifestResponse> DispatchManifestAsync(long manifestId, int userId);
        Task<ManifestResponse> UnloadManifestAsync(long manifestId, UnloadManifestRequest request, int userId);
        Task<bool> DeleteManifestAsync(long id, int userId);
        Task<List<ShipmentDto>> GetAvailableShipmentsForManifestAsync(long? originHubId, long? destinationHubId);
    }
}
