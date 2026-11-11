using System;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.DTOs.Gst;
using KTransport.API.Services.Gst;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers;

/// <summary>
/// GST profile for a specific party (consignor/consignee/vendor). One
/// versioned chain per party_id, tenant-scoped via the entity's query
/// filter. Reads open to any authenticated tenant user (Bilty UI needs
/// them to classify POS + recipient category); writes gated to tenant admin.
/// </summary>
[ApiController]
[Route("api/parties/{partyId:int}/gst-profile")]
[Authorize]
public class PartyGstProfileController : ControllerBase
{
    private readonly IGstProfileService _profiles;

    public PartyGstProfileController(IGstProfileService profiles)
    {
        _profiles = profiles;
    }

    [HttpGet]
    public async Task<IActionResult> GetActive(int partyId, [FromQuery] DateOnly? onDate)
    {
        var dto = await _profiles.GetActivePartyAsync(partyId, onDate);
        return dto == null ? NotFound() : Ok(dto);
    }

    [HttpGet("history")]
    public async Task<IActionResult> History(int partyId)
    {
        var rows = await _profiles.ListPartyHistoryAsync(partyId);
        return Ok(rows);
    }

    [HttpPost]
    [RequireSuperUser]
    public async Task<IActionResult> Upsert(int partyId, [FromBody] PartyGstProfileWriteDto body)
    {
        try
        {
            var dto = await _profiles.UpsertPartyAsync(partyId, body);
            return CreatedAtAction(nameof(GetActive), new { partyId }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, code = "GST_PROFILE_INVALID", message = ex.Message });
        }
    }
}
