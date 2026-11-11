using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs.Gst;

namespace KTransport.API.Services.Gst;

/// <summary>
/// Versioned profile upserts per <c>GstRegistration</c>.
/// Writing a new row CLOSES the current active row at
/// (new.EffectiveFrom - 1) and inserts the new one. Historical rows are
/// never mutated.
/// </summary>
public interface IGstRegistrationProfileService
{
    Task<GstRegistrationProfileDto?> GetActiveAsync(long gstRegistrationId, DateOnly? onDate);
    Task<IReadOnlyList<GstRegistrationProfileDto>> ListHistoryAsync(long gstRegistrationId);
    Task<GstRegistrationProfileDto> UpsertAsync(long gstRegistrationId, UpsertGstRegistrationProfileRequest req);
}
