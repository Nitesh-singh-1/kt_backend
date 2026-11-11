using System;
using System.Threading.Tasks;
using KTransport.API.DTOs.Gst;
using KTransport.API.Authorization;
using KTransport.API.Services.Gst;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers;

/// <summary>
/// E-Way Bill operations fronted by the configured GSP adapter.
/// Isolation rule: this controller does NOT touch Shipment/Bilty state
/// (only writes to eway_bill_cache). The Bilty UI reads from the cache
/// via GET /api/ewaybill/{ewbNumber}.
/// </summary>
[ApiController]
[Route("api/ewaybill")]
[Authorize]
[RequirePermission("consignments.create", "consignments.view")]
public class EWayBillController : ControllerBase
{
    private readonly IEwayBillService _service;

    public EWayBillController(IEwayBillService service)
    {
        _service = service;
    }

    private int? CurrentUserId =>
        int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    /// <summary>Lookup by EWB number — reads cache if present, else fetches via GSP.</summary>
    [HttpGet("{ewbNumber}")]
    public async Task<IActionResult> Get(string ewbNumber, [FromQuery] bool refresh = false)
    {
        if (!refresh)
        {
            var cached = await _service.GetFromCacheAsync(ewbNumber);
            if (cached != null) return Ok(cached);
        }
        try
        {
            var dto = await _service.FetchAsync(ewbNumber);
            return Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    [HttpPost("fetch")]
    public async Task<IActionResult> Fetch([FromBody] FetchEwbBody body)
    {
        if (string.IsNullOrWhiteSpace(body.EwbNumber))
            return BadRequest(new { success = false, message = "ewbNumber required" });
        try
        {
            var dto = await _service.FetchAsync(body.EwbNumber);
            return Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateEwbRequestDto body)
    {
        try
        {
            var dto = await _service.GenerateAsync(body, CurrentUserId);
            return CreatedAtAction(nameof(Get), new { ewbNumber = dto.EwbNumber }, dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    [HttpPost("update-vehicle")]
    public async Task<IActionResult> UpdateVehicle([FromBody] UpdateVehicleRequestDto body)
    {
        try
        {
            var dto = await _service.UpdateVehicleAsync(body);
            return Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel([FromBody] CancelEwbRequestDto body)
    {
        try
        {
            var dto = await _service.CancelAsync(body);
            return Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    [HttpPost("extend-validity")]
    public async Task<IActionResult> ExtendValidity([FromBody] ExtendValidityRequestDto body)
    {
        try
        {
            var dto = await _service.ExtendValidityAsync(body);
            return Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    public class FetchEwbBody { public string EwbNumber { get; set; } = string.Empty; }
}
