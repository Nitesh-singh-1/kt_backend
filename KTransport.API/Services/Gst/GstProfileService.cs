using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.DTOs.Gst;
using KTransport.API.Models.Gst;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services.Gst;

/// <summary>
/// TASK-033 BACK-COMPAT SHIM.
///
/// Company side: resolves the tenant's principal GstRegistration and
/// delegates to <see cref="GstRegistrationProfileService"/> so legacy
/// callers of the old interface keep compiling. New code should use
/// <see cref="IGstRegistrationService"/> + <see cref="IGstRegistrationProfileService"/>.
///
/// Party side: unchanged — party profiles are not part of TASK-033.
/// </summary>
[Obsolete("Use IGstRegistrationService + IGstRegistrationProfileService. One-release deprecation window.")]
public class GstProfileService : IGstProfileService
{
    private readonly KTransportDbContext _db;
    private readonly IGstRegistrationService _registrations;
    private readonly IGstRegistrationProfileService _registrationProfiles;

    public GstProfileService(
        KTransportDbContext db,
        IGstRegistrationService registrations,
        IGstRegistrationProfileService registrationProfiles)
    {
        _db = db;
        _registrations = registrations;
        _registrationProfiles = registrationProfiles;
    }

    // ---------------- Company (via principal registration) ----------------

    public async Task<CompanyGstProfileDto?> GetActiveCompanyAsync(DateOnly? onDate)
    {
        var principalId = await _registrations.GetPrincipalRegistrationIdAsync();
        if (principalId == null) return null;
        var reg = await _db.GstRegistrations.FirstOrDefaultAsync(r => r.Id == principalId.Value);
        if (reg == null) return null;

        var active = await _registrationProfiles.GetActiveAsync(principalId.Value, onDate);
        if (active == null) return null;

        return new CompanyGstProfileDto
        {
            Id = active.Id,
            Gstin = active.Gstin,
            GstRegistered = active.GstRegistered,
            State = reg.State,
            StateCode = reg.StateCode,
            IsGta = active.IsGta,
            IssuesConsignmentNote = active.IssuesConsignmentNote,
            GtaTaxScheme = active.GtaTaxScheme,
            EffectiveFrom = active.EffectiveFrom,
            EffectiveTo = active.EffectiveTo
        };
    }

    public async Task<IReadOnlyList<CompanyGstProfileDto>> ListCompanyHistoryAsync()
    {
        var principalId = await _registrations.GetPrincipalRegistrationIdAsync();
        if (principalId == null) return Array.Empty<CompanyGstProfileDto>();

        var reg = await _db.GstRegistrations.FirstOrDefaultAsync(r => r.Id == principalId.Value);
        if (reg == null) return Array.Empty<CompanyGstProfileDto>();

        var rows = await _registrationProfiles.ListHistoryAsync(principalId.Value);
        return rows.Select(r => new CompanyGstProfileDto
        {
            Id = r.Id,
            Gstin = r.Gstin,
            GstRegistered = r.GstRegistered,
            State = reg.State,
            StateCode = reg.StateCode,
            IsGta = r.IsGta,
            IssuesConsignmentNote = r.IssuesConsignmentNote,
            GtaTaxScheme = r.GtaTaxScheme,
            EffectiveFrom = r.EffectiveFrom,
            EffectiveTo = r.EffectiveTo
        }).ToList();
    }

    public async Task<CompanyGstProfileDto> UpsertCompanyAsync(CompanyGstProfileWriteDto write)
    {
        var principalId = await _registrations.GetPrincipalRegistrationIdAsync();
        if (principalId == null)
        {
            // Legacy write with no principal yet — synthesize one from the legacy state fields.
            if (string.IsNullOrWhiteSpace(write.State) || string.IsNullOrWhiteSpace(write.StateCode))
                throw new GstRegistrationException(
                    "VALIDATION_ERROR",
                    "state and stateCode are required to create the principal registration via the legacy endpoint.");

            var created = await _registrations.CreateAsync(new CreateGstRegistrationRequest
            {
                Name = $"{write.State} Office",
                State = write.State!,
                StateCode = write.StateCode!,
                IsPrincipal = true,
                InitialProfile = new UpsertGstRegistrationProfileRequest
                {
                    Gstin = write.Gstin,
                    GstRegistered = write.GstRegistered,
                    IsGta = write.IsGta,
                    IssuesConsignmentNote = write.IssuesConsignmentNote,
                    GtaTaxScheme = write.GtaTaxScheme,
                    EffectiveFrom = write.EffectiveFrom
                }
            });
            principalId = created.Id;
        }
        else
        {
            await _registrationProfiles.UpsertAsync(principalId.Value, new UpsertGstRegistrationProfileRequest
            {
                Gstin = write.Gstin,
                GstRegistered = write.GstRegistered,
                IsGta = write.IsGta,
                IssuesConsignmentNote = write.IssuesConsignmentNote,
                GtaTaxScheme = write.GtaTaxScheme,
                EffectiveFrom = write.EffectiveFrom
            });
        }

        var active = await GetActiveCompanyAsync(write.EffectiveFrom)
            ?? throw new InvalidOperationException("Legacy upsert produced no active profile row.");
        return active;
    }

    // ---------------- Party (unchanged) ----------------

    public async Task<PartyGstProfileDto?> GetActivePartyAsync(int partyId, DateOnly? onDate)
    {
        var d = onDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var row = await _db.PartyGstProfiles
            .Where(p => p.PartyId == partyId)
            .Where(p => p.EffectiveFrom <= d && (p.EffectiveTo == null || p.EffectiveTo >= d))
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync();
        return row == null ? null : ToDto(row);
    }

    public async Task<IReadOnlyList<PartyGstProfileDto>> ListPartyHistoryAsync(int partyId)
    {
        var rows = await _db.PartyGstProfiles
            .Where(p => p.PartyId == partyId)
            .OrderByDescending(p => p.EffectiveFrom)
            .ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<PartyGstProfileDto> UpsertPartyAsync(int partyId, PartyGstProfileWriteDto write)
    {
        var now = DateTime.UtcNow;
        var effFrom = write.EffectiveFrom ?? DateOnly.FromDateTime(now);

        var active = await _db.PartyGstProfiles
            .Where(p => p.PartyId == partyId)
            .Where(p => p.EffectiveTo == null || p.EffectiveTo >= effFrom)
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync();

        if (active != null)
        {
            var closeAt = effFrom.AddDays(-1);
            if (active.EffectiveFrom > closeAt)
                throw new InvalidOperationException("EffectiveFrom must be AFTER the current active profile's EffectiveFrom.");
            active.EffectiveTo = closeAt;
            active.UpdatedAt = now;
        }

        var neu = new PartyGstProfile
        {
            Id = Guid.NewGuid(),
            PartyId = partyId,
            Gstin = write.Gstin,
            GstRegistered = write.GstRegistered,
            State = write.State,
            StateCode = write.StateCode,
            RecipientCategory = write.RecipientCategory,
            PlaceOfSupplyDefault = write.PlaceOfSupplyDefault,
            EffectiveFrom = effFrom,
            EffectiveTo = null,
            CreatedAt = now
        };
        _db.PartyGstProfiles.Add(neu);
        await _db.SaveChangesAsync();
        return ToDto(neu);
    }

    private static PartyGstProfileDto ToDto(PartyGstProfile p) => new()
    {
        Id = p.Id,
        PartyId = p.PartyId,
        Gstin = p.Gstin,
        GstRegistered = p.GstRegistered,
        State = p.State,
        StateCode = p.StateCode,
        RecipientCategory = p.RecipientCategory,
        PlaceOfSupplyDefault = p.PlaceOfSupplyDefault,
        EffectiveFrom = p.EffectiveFrom,
        EffectiveTo = p.EffectiveTo
    };
}
