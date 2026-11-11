using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs.Gst;

namespace KTransport.API.Services.Gst;

public interface IGstRegistrationService
{
    Task<IReadOnlyList<GstRegistrationDto>> ListAsync();
    Task<GstRegistrationDto?> GetAsync(long id);
    Task<GstRegistrationDto> CreateAsync(CreateGstRegistrationRequest req);
    Task<GstRegistrationDto?> UpdateAsync(long id, UpdateGstRegistrationRequest req);
    Task<bool> DeactivateAsync(long id);
    Task<GstRegistrationDto?> MakePrincipalAsync(long id);

    /// <summary>Resolve the tenant's single principal registration, or null if none exists yet.</summary>
    Task<long?> GetPrincipalRegistrationIdAsync();
}
