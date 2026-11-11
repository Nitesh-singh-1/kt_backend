using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.DTOs.Gst;
using KTransport.API.Models.Gst;
using KTransport.API.Services.Gst;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers;

/// <summary>
/// Platform-admin console for the GST rule matrix. Rate/treatment values
/// live here, not in app code. A rule referenced by any tax_calculations
/// row is immutable: only EffectiveTo and Active can change.
/// </summary>
[ApiController]
[Route("api/gst/rules")]
[Authorize]
[RequirePlatformAdmin]
public class GstRuleController : ControllerBase
{
    private readonly IGstRuleRepository _repo;

    public GstRuleController(IGstRuleRepository repo)
    {
        _repo = repo;
    }

    private int? CurrentUserId =>
        int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateOnly? onDate, [FromQuery] bool? active)
    {
        var rows = await _repo.ListAsync(onDate, active);
        return Ok(rows.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var rule = await _repo.GetByIdAsync(id);
        return rule == null ? NotFound() : Ok(ToDto(rule));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] GstRuleCreateDto dto)
    {
        var invariantError = ValidateInvariants(dto);
        if (invariantError != null)
            return BadRequest(new { success = false, message = invariantError });

        var rule = new GstRule
        {
            ServiceType = dto.ServiceType,
            ProviderIsGta = dto.ProviderIsGta,
            IssuesConsignmentNote = dto.IssuesConsignmentNote,
            ProviderGstRegistered = dto.ProviderGstRegistered,
            GtaTaxScheme = dto.GtaTaxScheme,
            RecipientCategory = dto.RecipientCategory,
            PosRelation = dto.PosRelation,
            TaxTreatment = dto.TaxTreatment,
            GstRate = dto.GstRate,
            CgstRate = dto.CgstRate,
            SgstRate = dto.SgstRate,
            IgstRate = dto.IgstRate,
            UtgstRate = dto.UtgstRate,
            ItcEligibleForProvider = dto.ItcEligibleForProvider,
            RcmPayableBy = dto.RcmPayableBy,
            NotificationReference = dto.NotificationReference,
            ExceptionsNote = dto.ExceptionsNote,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo
        };

        try
        {
            var saved = await _repo.AddAsync(rule, CurrentUserId);
            return CreatedAtAction(nameof(GetById), new { id = saved.Id }, ToDto(saved));
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            return BadRequest(new
            {
                success = false,
                code = "GST_RULE_TUPLE_CONFLICT",
                message = "A rule with the same input tuple and effective_from already exists. Close the existing row first.",
                detail = ex.InnerException?.Message
            });
        }
    }

    /// <summary>
    /// Transactional bulk upload — validates every row first, inserts
    /// all-or-none. Used to seed a fresh environment from a JSON file
    /// of CBIC-sourced rules without touching app code.
    /// </summary>
    [HttpPost("bulk")]
    public async Task<IActionResult> BulkCreate([FromBody] List<GstRuleCreateDto> rules)
    {
        if (rules == null || rules.Count == 0)
            return BadRequest(new { success = false, message = "Body must be a non-empty array." });

        var errors = new List<object>();
        for (var i = 0; i < rules.Count; i++)
        {
            var err = ValidateInvariants(rules[i]);
            if (err != null) errors.Add(new { index = i, message = err });
        }
        if (errors.Count > 0)
            return BadRequest(new { success = false, code = "GST_RULE_BULK_INVALID", errors });

        var entities = rules.Select(dto => new GstRule
        {
            ServiceType = dto.ServiceType,
            ProviderIsGta = dto.ProviderIsGta,
            IssuesConsignmentNote = dto.IssuesConsignmentNote,
            ProviderGstRegistered = dto.ProviderGstRegistered,
            GtaTaxScheme = dto.GtaTaxScheme,
            RecipientCategory = dto.RecipientCategory,
            PosRelation = dto.PosRelation,
            TaxTreatment = dto.TaxTreatment,
            GstRate = dto.GstRate,
            CgstRate = dto.CgstRate,
            SgstRate = dto.SgstRate,
            IgstRate = dto.IgstRate,
            UtgstRate = dto.UtgstRate,
            ItcEligibleForProvider = dto.ItcEligibleForProvider,
            RcmPayableBy = dto.RcmPayableBy,
            NotificationReference = dto.NotificationReference,
            ExceptionsNote = dto.ExceptionsNote,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo
        }).ToList();

        try
        {
            var saved = await _repo.AddManyAsync(entities, CurrentUserId);
            return Ok(new { success = true, inserted = saved.Count, ids = saved.Select(r => r.Id) });
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            return BadRequest(new
            {
                success = false,
                code = "GST_RULE_TUPLE_CONFLICT",
                message = "At least one input tuple conflicts with an existing active rule's effective_from. Nothing was inserted.",
                detail = ex.InnerException?.Message
            });
        }
    }

    /// <summary>Only EffectiveTo and Active may change on an existing rule.</summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateWindow(Guid id, [FromBody] GstRuleUpdateDto dto)
    {
        var existing = await _repo.GetByIdAsync(id);
        if (existing == null) return NotFound();

        if (dto.EffectiveTo.HasValue && existing.EffectiveTo.HasValue && existing.EffectiveTo < dto.EffectiveTo)
        {
            // extending past a previous close is allowed; shortening into the past is not when audit exists
            if (await _repo.IsReferencedByAuditAsync(id))
                return BadRequest(new { success = false, message = "Cannot extend window past original close; rule is referenced by audit rows." });
        }

        var saved = await _repo.UpdateWindowAsync(id, dto.EffectiveTo, dto.Active, CurrentUserId);
        return Ok(ToDto(saved));
    }

    private static string? ValidateInvariants(GstRuleCreateDto dto)
    {
        if (dto.EffectiveTo.HasValue && dto.EffectiveTo < dto.EffectiveFrom)
            return "EffectiveTo must be on or after EffectiveFrom.";

        var hasIntraPair = (dto.CgstRate ?? 0) > 0 || (dto.SgstRate ?? 0) > 0;
        var hasUtPair = (dto.CgstRate ?? 0) > 0 && (dto.UtgstRate ?? 0) > 0;
        var hasIgst = (dto.IgstRate ?? 0) > 0;

        if (dto.TaxTreatment == GstTaxTreatment.ForwardCharge)
        {
            if (!hasIntraPair && !hasUtPair && !hasIgst)
                return "Forward-charge rule must specify at least one of (CGST+SGST), (CGST+UTGST), or IGST.";

            switch (dto.PosRelation)
            {
                case PosRelation.IntraState:
                    if (hasIgst || hasUtPair) return "Intra-state rule cannot carry IGST or UTGST.";
                    if ((dto.CgstRate ?? 0) == 0 || (dto.SgstRate ?? 0) == 0)
                        return "Intra-state rule must set both CGST and SGST.";
                    break;
                case PosRelation.UnionTerritory:
                    if (hasIgst) return "UT-intra rule cannot carry IGST.";
                    if ((dto.CgstRate ?? 0) == 0 || (dto.UtgstRate ?? 0) == 0)
                        return "UT-intra rule must set both CGST and UTGST.";
                    break;
                case PosRelation.InterState:
                    if (hasIntraPair || hasUtPair) return "Inter-state rule cannot carry CGST/SGST/UTGST.";
                    if (!hasIgst) return "Inter-state rule must set IGST.";
                    break;
            }
        }
        else if (dto.TaxTreatment == GstTaxTreatment.ReverseCharge
                 || dto.TaxTreatment == GstTaxTreatment.Exempt
                 || dto.TaxTreatment == GstTaxTreatment.OutOfScope)
        {
            if (hasIntraPair || hasIgst || hasUtPair)
                return $"{dto.TaxTreatment} rule must leave all component rates null/zero.";
        }

        return null;
    }

    private static GstRuleDto ToDto(GstRule r) => new()
    {
        Id = r.Id,
        ServiceType = r.ServiceType,
        ProviderIsGta = r.ProviderIsGta,
        IssuesConsignmentNote = r.IssuesConsignmentNote,
        ProviderGstRegistered = r.ProviderGstRegistered,
        GtaTaxScheme = r.GtaTaxScheme,
        RecipientCategory = r.RecipientCategory,
        PosRelation = r.PosRelation,
        TaxTreatment = r.TaxTreatment,
        GstRate = r.GstRate,
        CgstRate = r.CgstRate,
        SgstRate = r.SgstRate,
        IgstRate = r.IgstRate,
        UtgstRate = r.UtgstRate,
        ItcEligibleForProvider = r.ItcEligibleForProvider,
        RcmPayableBy = r.RcmPayableBy,
        NotificationReference = r.NotificationReference,
        ExceptionsNote = r.ExceptionsNote,
        EffectiveFrom = r.EffectiveFrom,
        EffectiveTo = r.EffectiveTo,
        Active = r.Active
    };
}
