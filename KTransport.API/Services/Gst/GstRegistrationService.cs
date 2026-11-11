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
public class GstRegistrationService : IGstRegistrationService
{
    private readonly KTransportDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IGstRegistrationProfileService _profiles;

    public GstRegistrationService(
        KTransportDbContext db,
        ITenantContext tenant,
        IGstRegistrationProfileService profiles)
    {
        _db = db;
        _tenant = tenant;
        _profiles = profiles;
    }

    public async Task<IReadOnlyList<GstRegistrationDto>> ListAsync()
    {
        var rows = await _db.GstRegistrations
            .OrderByDescending(r => r.IsPrincipal)
            .ThenBy(r => r.Name)
            .ToListAsync();

        var result = new List<GstRegistrationDto>(rows.Count);
        foreach (var r in rows)
        {
            var active = await _profiles.GetActiveAsync(r.Id, null);
            result.Add(ToDto(r, active));
        }
        return result;
    }

    public async Task<GstRegistrationDto?> GetAsync(long id)
    {
        var row = await _db.GstRegistrations.FirstOrDefaultAsync(r => r.Id == id);
        if (row == null) return null;
        var active = await _profiles.GetActiveAsync(id, null);
        return ToDto(row, active);
    }

    public async Task<GstRegistrationDto> CreateAsync(CreateGstRegistrationRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new GstRegistrationException("VALIDATION_ERROR", "Name is required.");
        if (string.IsNullOrWhiteSpace(req.State))
            throw new GstRegistrationException("VALIDATION_ERROR", "State is required.");
        if (string.IsNullOrWhiteSpace(req.StateCode) || req.StateCode.Length != 2)
            throw new GstRegistrationException("VALIDATION_ERROR", "StateCode must be 2 characters.");

        var tenantId = _tenant.CurrentTenantId;

        // Enforce "exactly one principal per tenant" at the application layer in addition
        // to the partial unique index — gives a clean 409 instead of a DB constraint violation.
        var anyExisting = await _db.GstRegistrations.AnyAsync();
        var hasPrincipal = await _db.GstRegistrations.AnyAsync(r => r.IsPrincipal);
        var wantsPrincipal = req.IsPrincipal || !anyExisting;

        if (wantsPrincipal && hasPrincipal)
            throw new GstRegistrationException(
                "GST_REGISTRATION_PRINCIPAL_CONFLICT",
                "Another principal registration already exists. Use /make-principal to swap.",
                409);

        // Optional duplicate GSTIN guard — scoped to the initial profile only (deeper
        // enforcement across history is out of scope for the directory row).
        if (req.InitialProfile?.Gstin is { Length: > 0 } gstin)
        {
            var dupe = await _db.GstRegistrationGstProfiles
                .AnyAsync(p => p.Gstin == gstin);
            if (dupe)
                throw new GstRegistrationException(
                    "GST_REGISTRATION_DUPLICATE_GSTIN",
                    $"A registration with GSTIN {gstin} already exists for this tenant.",
                    409);
        }

        using var tx = await _db.Database.BeginTransactionAsync();

        var now = DateTime.UtcNow;
        var row = new GstRegistration
        {
            TenantId = tenantId,
            Name = req.Name.Trim(),
            State = req.State.Trim(),
            StateCode = req.StateCode.Trim(),
            Address = req.Address,
            Pincode = req.Pincode,
            IsPrincipal = wantsPrincipal,
            IsActive = true,
            CreatedAt = now
        };
        _db.GstRegistrations.Add(row);
        await _db.SaveChangesAsync();

        GstRegistrationProfileDto? active = null;
        if (req.InitialProfile != null)
            active = await _profiles.UpsertAsync(row.Id, req.InitialProfile);

        await tx.CommitAsync();
        return ToDto(row, active);
    }

    public async Task<GstRegistrationDto?> UpdateAsync(long id, UpdateGstRegistrationRequest req)
    {
        var row = await _db.GstRegistrations.FirstOrDefaultAsync(r => r.Id == id);
        if (row == null) return null;

        if (req.Name is { Length: > 0 }) row.Name = req.Name.Trim();
        if (req.Address != null) row.Address = req.Address;
        if (req.Pincode != null) row.Pincode = req.Pincode;
        if (req.IsActive.HasValue) row.IsActive = req.IsActive.Value;
        row.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        var active = await _profiles.GetActiveAsync(id, null);
        return ToDto(row, active);
    }

    public async Task<bool> DeactivateAsync(long id)
    {
        var row = await _db.GstRegistrations.FirstOrDefaultAsync(r => r.Id == id);
        if (row == null) return false;

        if (row.IsPrincipal)
            throw new GstRegistrationException(
                "GST_REGISTRATION_IS_PRINCIPAL",
                "Promote another registration via /make-principal before deactivating the principal.",
                409);

        var inUse = await _db.Locations.AnyAsync(l => l.GstRegistrationId == id);
        if (inUse)
            throw new GstRegistrationException(
                "GST_REGISTRATION_IN_USE",
                "Locations still reference this GST registration.",
                409);

        row.IsActive = false;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<GstRegistrationDto?> MakePrincipalAsync(long id)
    {
        var target = await _db.GstRegistrations.FirstOrDefaultAsync(r => r.Id == id);
        if (target == null) return null;

        if (target.IsPrincipal)
            return ToDto(target, await _profiles.GetActiveAsync(id, null));

        using var tx = await _db.Database.BeginTransactionAsync();

        // Partial unique index enforces exactly one principal — demote first.
        var current = await _db.GstRegistrations.FirstOrDefaultAsync(r => r.IsPrincipal);
        if (current != null)
        {
            current.IsPrincipal = false;
            current.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        target.IsPrincipal = true;
        target.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        var active = await _profiles.GetActiveAsync(id, null);
        return ToDto(target, active);
    }

    public async Task<long?> GetPrincipalRegistrationIdAsync()
    {
        var row = await _db.GstRegistrations
            .Where(r => r.IsPrincipal)
            .Select(r => (long?)r.Id)
            .FirstOrDefaultAsync();
        return row;
    }

    private static GstRegistrationDto ToDto(GstRegistration r, GstRegistrationProfileDto? active) => new()
    {
        Id = r.Id,
        TenantId = r.TenantId,
        Name = r.Name,
        State = r.State,
        StateCode = r.StateCode,
        Address = r.Address,
        Pincode = r.Pincode,
        IsPrincipal = r.IsPrincipal,
        IsActive = r.IsActive,
        ActiveProfile = active,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt
    };
}
