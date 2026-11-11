using System;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.DTOs.Gst;
using KTransport.API.Services.Gst;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers;

/// <summary>
/// DEPRECATED (TASK-033) — Backward-compat shim for one release.
/// Proxies to the tenant's principal <c>GstRegistration</c> and its
/// active <c>GstRegistrationGstProfile</c>. Will be removed in the next
/// release after the frontend migrates to <c>/api/gst-registrations</c>.
/// </summary>
[ApiController]
[Route("api/company-gst-profile")]
[Authorize]
[Obsolete("Use /api/gst-registrations/{id}/profile. Removed in next release.")]
public class CompanyGstProfileController : ControllerBase
{
#pragma warning disable CS0618
    private readonly IGstProfileService _profiles;

    public CompanyGstProfileController(IGstProfileService profiles)
    {
        _profiles = profiles;
    }
#pragma warning restore CS0618

    /// <summary>DEPRECATED — Returns the principal registration's active profile in the legacy shape.</summary>
    [HttpGet]
    [Obsolete]
    public async Task<IActionResult> GetActive([FromQuery] DateOnly? onDate)
    {
        var dto = await _profiles.GetActiveCompanyAsync(onDate);
        return dto == null
            ? NotFound(new { success = false, code = "NOT_FOUND", message = "No principal registration or no active profile." })
            : Ok(dto);
    }

    /// <summary>DEPRECATED — Reverse-chronological history of the principal registration's profile.</summary>
    [HttpGet("history")]
    [Obsolete]
    public async Task<IActionResult> History()
    {
        var rows = await _profiles.ListCompanyHistoryAsync();
        return Ok(rows);
    }

    /// <summary>DEPRECATED — Writes to the principal registration (creates it from state/stateCode if none exists yet).</summary>
    [HttpPost]
    [Obsolete]
    [RequireSuperUser]
    public async Task<IActionResult> Upsert([FromBody] CompanyGstProfileWriteDto body)
    {
        try
        {
            var dto = await _profiles.UpsertCompanyAsync(body);
            return CreatedAtAction(nameof(GetActive), new { }, dto);
        }
        catch (GstRegistrationException ex)
        {
            return StatusCode(ex.StatusCode, new { success = false, code = ex.Code, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, code = "GST_PROFILE_INVALID", message = ex.Message });
        }
    }
}
