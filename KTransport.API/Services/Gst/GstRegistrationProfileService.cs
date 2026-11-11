using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.DTOs.Gst;
using KTransport.API.Models.Gst;
using KTransport.API.Services;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services.Gst;

/// <inheritdoc />
public class GstRegistrationProfileService : IGstRegistrationProfileService
{
    private readonly KTransportDbContext _db;
    private readonly ITenantContext _tenant;

    public GstRegistrationProfileService(KTransportDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<GstRegistrationProfileDto?> GetActiveAsync(long gstRegistrationId, DateOnly? onDate)
    {
        var d = onDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var row = await _db.GstRegistrationGstProfiles
            .Where(p => p.GstRegistrationId == gstRegistrationId)
            .Where(p => p.EffectiveFrom <= d && (p.EffectiveTo == null || p.EffectiveTo >= d))
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync();
        return row == null ? null : ToDto(row);
    }

    public async Task<IReadOnlyList<GstRegistrationProfileDto>> ListHistoryAsync(long gstRegistrationId)
    {
        var rows = await _db.GstRegistrationGstProfiles
            .Where(p => p.GstRegistrationId == gstRegistrationId)
            .OrderByDescending(p => p.EffectiveFrom)
            .ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<GstRegistrationProfileDto> UpsertAsync(long gstRegistrationId, UpsertGstRegistrationProfileRequest req)
    {
        var now = DateTime.UtcNow;
        var effFrom = req.EffectiveFrom ?? DateOnly.FromDateTime(now);

        // Verify the registration exists and belongs to the current tenant (query filter does that).
        var registration = await _db.GstRegistrations.FirstOrDefaultAsync(r => r.Id == gstRegistrationId)
            ?? throw new GstRegistrationException("GST_REGISTRATION_NOT_FOUND", "GST registration not found.", 404);

        var active = await _db.GstRegistrationGstProfiles
            .Where(p => p.GstRegistrationId == gstRegistrationId)
            .Where(p => p.EffectiveTo == null || p.EffectiveTo >= effFrom)
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync();

        if (active != null)
        {
            var closeAt = effFrom.AddDays(-1);
            if (active.EffectiveFrom > closeAt)
                throw new GstRegistrationException(
                    "GST_PROFILE_EFFECTIVE_FROM_INVALID",
                    "EffectiveFrom must be strictly after the current active profile's EffectiveFrom.");
            active.EffectiveTo = closeAt;
            active.UpdatedAt = now;
        }

        var neu = new GstRegistrationGstProfile
        {
            Id = Guid.NewGuid(),
            TenantId = registration.TenantId,
            GstRegistrationId = gstRegistrationId,
            Gstin = req.Gstin,
            GstRegistered = req.GstRegistered,
            IsGta = req.IsGta,
            IssuesConsignmentNote = req.IssuesConsignmentNote,
            GtaTaxScheme = req.GtaTaxScheme,
            EffectiveFrom = effFrom,
            EffectiveTo = null,
            CreatedAt = now
        };
        _db.GstRegistrationGstProfiles.Add(neu);
        await _db.SaveChangesAsync();
        return ToDto(neu);
    }

    internal static GstRegistrationProfileDto ToDto(GstRegistrationGstProfile p) => new()
    {
        Id = p.Id,
        GstRegistrationId = p.GstRegistrationId,
        Gstin = p.Gstin,
        GstRegistered = p.GstRegistered,
        IsGta = p.IsGta,
        IssuesConsignmentNote = p.IssuesConsignmentNote,
        GtaTaxScheme = p.GtaTaxScheme,
        EffectiveFrom = p.EffectiveFrom,
        EffectiveTo = p.EffectiveTo
    };
}
