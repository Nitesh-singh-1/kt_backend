using System;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.DTOs.Gst;
using KTransport.API.Services.Gst;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers;

/// <summary>
/// TASK-033 — Tenant GST registrations (one per state the tenant operates in).
/// Reads open to any authenticated tenant user; writes gated to RequireSuperUser.
/// </summary>
[ApiController]
[Route("api/gst-registrations")]
[Authorize]
public class GstRegistrationController : ControllerBase
{
    private readonly IGstRegistrationService _registrations;
    private readonly IGstRegistrationProfileService _profiles;

    public GstRegistrationController(
        IGstRegistrationService registrations,
        IGstRegistrationProfileService profiles)
    {
        _registrations = registrations;
        _profiles = profiles;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var rows = await _registrations.ListAsync();
        return Ok(rows);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id)
    {
        var row = await _registrations.GetAsync(id);
        return row == null ? NotFoundEnvelope("Not found.") : Ok(row);
    }

    [HttpPost]
    [RequireSuperUser]
    public async Task<IActionResult> Create([FromBody] CreateGstRegistrationRequest body)
    {
        try
        {
            var created = await _registrations.CreateAsync(body);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (GstRegistrationException ex)
        {
            return StatusCode(ex.StatusCode, new { success = false, code = ex.Code, message = ex.Message });
        }
    }

    [HttpPatch("{id:long}")]
    [RequireSuperUser]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateGstRegistrationRequest body)
    {
        try
        {
            var updated = await _registrations.UpdateAsync(id, body);
            return updated == null ? NotFoundEnvelope("Not found.") : Ok(updated);
        }
        catch (GstRegistrationException ex)
        {
            return StatusCode(ex.StatusCode, new { success = false, code = ex.Code, message = ex.Message });
        }
    }

    [HttpDelete("{id:long}")]
    [RequireSuperUser]
    public async Task<IActionResult> Deactivate(long id)
    {
        try
        {
            var ok = await _registrations.DeactivateAsync(id);
            return ok ? NoContent() : NotFoundEnvelope("Not found.");
        }
        catch (GstRegistrationException ex)
        {
            return StatusCode(ex.StatusCode, new { success = false, code = ex.Code, message = ex.Message });
        }
    }

    [HttpPatch("{id:long}/make-principal")]
    [RequireSuperUser]
    public async Task<IActionResult> MakePrincipal(long id)
    {
        try
        {
            var updated = await _registrations.MakePrincipalAsync(id);
            return updated == null ? NotFoundEnvelope("Not found.") : Ok(updated);
        }
        catch (GstRegistrationException ex)
        {
            return StatusCode(ex.StatusCode, new { success = false, code = ex.Code, message = ex.Message });
        }
    }

    [HttpGet("{id:long}/profile")]
    public async Task<IActionResult> GetProfile(long id, [FromQuery] DateOnly? onDate)
    {
        // 404 when the parent registration doesn't exist in-tenant, else return the active row (or 404).
        var reg = await _registrations.GetAsync(id);
        if (reg == null) return NotFoundEnvelope("Not found.");
        var active = await _profiles.GetActiveAsync(id, onDate);
        return active == null ? NotFoundEnvelope("No active profile.") : Ok(active);
    }

    [HttpPost("{id:long}/profile")]
    [RequireSuperUser]
    public async Task<IActionResult> UpsertProfile(long id, [FromBody] UpsertGstRegistrationProfileRequest body)
    {
        try
        {
            // Trip 404 cleanly if the registration isn't visible in-tenant.
            var reg = await _registrations.GetAsync(id);
            if (reg == null) return NotFoundEnvelope("Not found.");
            var dto = await _profiles.UpsertAsync(id, body);
            return CreatedAtAction(nameof(GetProfile), new { id }, dto);
        }
        catch (GstRegistrationException ex)
        {
            return StatusCode(ex.StatusCode, new { success = false, code = ex.Code, message = ex.Message });
        }
    }

    [HttpGet("{id:long}/profile/history")]
    public async Task<IActionResult> History(long id)
    {
        var reg = await _registrations.GetAsync(id);
        if (reg == null) return NotFoundEnvelope("Not found.");
        var rows = await _profiles.ListHistoryAsync(id);
        return Ok(rows);
    }

    private IActionResult NotFoundEnvelope(string message) =>
        NotFound(new { success = false, code = "NOT_FOUND", message });
}
