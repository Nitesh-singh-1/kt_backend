using System;
using System.Threading.Tasks;
using KTransport.API.DTOs.Gst;
using KTransport.API.Authorization;
using KTransport.API.Services.Gst;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers;

/// <summary>
/// Freight-GST endpoints. All require authentication; tenant scope comes
/// from the JWT (ITenantContext).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission("consignments.create", "consignments.view")]
public class TaxController : ControllerBase
{
    private readonly ITaxCalculationService _tax;

    public TaxController(ITaxCalculationService tax)
    {
        _tax = tax;
    }

    private int? CurrentUserId =>
        int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    /// <summary>Preview — does NOT persist. Used by the Bilty UI live.</summary>
    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate([FromBody] TaxCalculationInputDto input)
    {
        try
        {
            var result = await _tax.CalculateAsync(input);
            return Ok(result);
        }
        catch (GstRuleUnresolvedException ex)
        {
            return BadRequest(new { success = false, code = "GST_RULE_UNRESOLVED", message = ex.Message });
        }
    }

    /// <summary>Compute AND persist an immutable audit row for this tenant.</summary>
    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] TaxCalculationApplyRequestDto request)
    {
        try
        {
            var dto = await _tax.ApplyAsync(request, CurrentUserId);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }
        catch (GstRuleUnresolvedException ex)
        {
            return BadRequest(new { success = false, code = "GST_RULE_UNRESOLVED", message = ex.Message });
        }
    }

    /// <summary>
    /// Re-run calc for an existing audit row. Historical fidelity: the
    /// stored input (including its TransactionDate) is replayed, so the
    /// rule effective on the original date is still used. The old row's
    /// SupersededBy is updated to point at the new one.
    /// </summary>
    [HttpPost("recalculate/{taxCalculationId:guid}")]
    public async Task<IActionResult> Recalculate(Guid taxCalculationId)
    {
        try
        {
            var dto = await _tax.RecalculateAsync(taxCalculationId, CurrentUserId);
            return Ok(dto);
        }
        catch (GstRuleUnresolvedException ex)
        {
            return BadRequest(new { success = false, code = "GST_RULE_UNRESOLVED", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, code = "RECALC_INVALID", message = ex.Message });
        }
    }

    [HttpGet("calculation/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var dto = await _tax.GetAsync(id);
        return dto == null ? NotFound() : Ok(dto);
    }
}
