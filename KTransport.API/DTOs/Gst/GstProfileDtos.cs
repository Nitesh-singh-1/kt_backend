using System;
using KTransport.API.Models.Gst;

namespace KTransport.API.DTOs.Gst;

// ---------------------------------------------------------------------
// TASK-033 — New registration + per-registration profile DTOs
// ---------------------------------------------------------------------

public class GstRegistrationDto
{
    public long Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string StateCode { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Pincode { get; set; }
    public bool IsPrincipal { get; set; }
    public bool IsActive { get; set; }
    public GstRegistrationProfileDto? ActiveProfile { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateGstRegistrationRequest
{
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string StateCode { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Pincode { get; set; }
    public bool IsPrincipal { get; set; }
    public UpsertGstRegistrationProfileRequest? InitialProfile { get; set; }
}

public class UpdateGstRegistrationRequest
{
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? Pincode { get; set; }
    public bool? IsActive { get; set; }
}

public class GstRegistrationProfileDto
{
    public Guid Id { get; set; }
    public long GstRegistrationId { get; set; }
    public string? Gstin { get; set; }
    public bool GstRegistered { get; set; }
    public bool IsGta { get; set; }
    public bool IssuesConsignmentNote { get; set; }
    public GtaTaxScheme GtaTaxScheme { get; set; } = GtaTaxScheme.NotApplicable;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

public class UpsertGstRegistrationProfileRequest
{
    public string? Gstin { get; set; }
    public bool GstRegistered { get; set; }
    public bool IsGta { get; set; }
    public bool IssuesConsignmentNote { get; set; }
    public GtaTaxScheme GtaTaxScheme { get; set; } = GtaTaxScheme.NotApplicable;
    public DateOnly? EffectiveFrom { get; set; }
}

// ---------------------------------------------------------------------
// Legacy shim DTOs — bit-for-bit copies of today's shapes for one-release
// backward compatibility against /api/company-gst-profile.
// ---------------------------------------------------------------------

public class CompanyGstProfileDto
{
    public Guid Id { get; set; }
    public string? Gstin { get; set; }
    public bool GstRegistered { get; set; }
    public string? State { get; set; }
    public string? StateCode { get; set; }
    public bool IsGta { get; set; }
    public bool IssuesConsignmentNote { get; set; }
    public GtaTaxScheme GtaTaxScheme { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

public class CompanyGstProfileWriteDto
{
    public string? Gstin { get; set; }
    public bool GstRegistered { get; set; }
    public string? State { get; set; }
    public string? StateCode { get; set; }
    public bool IsGta { get; set; }
    public bool IssuesConsignmentNote { get; set; }
    public GtaTaxScheme GtaTaxScheme { get; set; } = GtaTaxScheme.NotApplicable;
    public DateOnly? EffectiveFrom { get; set; }
}

// ---------------------------------------------------------------------
// Party profile DTOs (unchanged by this task)
// ---------------------------------------------------------------------

public class PartyGstProfileDto
{
    public Guid Id { get; set; }
    public int PartyId { get; set; }
    public string? Gstin { get; set; }
    public bool GstRegistered { get; set; }
    public string? State { get; set; }
    public string? StateCode { get; set; }
    public RecipientCategory RecipientCategory { get; set; }
    public string? PlaceOfSupplyDefault { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

public class PartyGstProfileWriteDto
{
    public string? Gstin { get; set; }
    public bool GstRegistered { get; set; }
    public string? State { get; set; }
    public string? StateCode { get; set; }
    public RecipientCategory RecipientCategory { get; set; } = RecipientCategory.UnregisteredBusiness;
    public string? PlaceOfSupplyDefault { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
}
