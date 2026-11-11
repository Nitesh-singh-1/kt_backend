// Namespace deliberately NOT "KTransport.API.Options": that shadows the BCL
// Microsoft.Extensions.Options namespace for any KTransport.API.* code using
// unqualified Options.Create(...). Renamed during the develop integration.
namespace KTransport.API.Configuration;

/// <summary>
/// Config section: "EWayBill" in appsettings / env vars.
/// Determines which adapter is used + holds all GSP credentials.
/// All production secrets MUST come from env vars or user-secrets,
/// NEVER checked-in appsettings.json.
/// </summary>
public class EWayBillOptions
{
    /// <summary>"Mock" (default, dev) or "Nic" (real NIC via GSP).</summary>
    public string Adapter { get; set; } = "Mock";

    /// <summary>GSP base URL — e.g. https://gsp.sandbox.vayana.com/.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>GSP vendor identifier used in the URL path, e.g. "vayana" / "masters-india" / "taxilla".</summary>
    public string? EwbProvider { get; set; }

    /// <summary>Sandbox or Production env flag — affects URL template.</summary>
    public bool Sandbox { get; set; } = true;

    /// <summary>GSP-issued client credential — header "client-id".</summary>
    public string? ClientId { get; set; }

    /// <summary>GSP-issued client credential — header "client-secret".</summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Alternative GSP (e.g. Vayana) uses signed tokens instead of raw
    /// client-id/secret: X-Asp-Auth-Token + X-Asp-Auth-Signature headers.
    /// Leave both empty if using client-id/client-secret style.
    /// </summary>
    public string? AspAuthToken { get; set; }
    public string? AspAuthSignatureSecret { get; set; }

    /// <summary>Taxpayer-side user issued by the EWB portal for API usage.</summary>
    public string? Username { get; set; }
    public string? Password { get; set; }

    /// <summary>Taxpayer GSTIN against which the auth token is issued.</summary>
    public string? Gstin { get; set; }

    /// <summary>
    /// Path to NIC's RSA public key (PEM) used to encrypt app_key during
    /// authentication. Different file for sandbox vs production.
    /// </summary>
    public string? NicPublicKeyPemPath { get; set; }

    /// <summary>Token renew window — re-auth after this many minutes.</summary>
    public int TokenRenewMinutes { get; set; } = 345;    // 5h 45m, token lives ~6h

    public int RequestTimeoutSeconds { get; set; } = 30;
}
