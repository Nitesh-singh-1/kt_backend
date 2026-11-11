using System.Threading;
using System.Threading.Tasks;
using KTransport.API.DTOs.Gst;

namespace KTransport.API.Services.Gst;

/// <summary>
/// Thin wrapper over the NIC E-Way Bill HTTP API, pluggable per GSP.
/// Methods return the raw NIC-shaped JSON payload (as string) + the
/// extracted ewbNumber/validUpto for convenience. The caller
/// (EwayBillService) persists what we got to eway_bill_cache.
///
/// Implementations:
///   - MockEwayBillGspAdapter: deterministic dev/test impl, no network.
///   - NicEwayBillGspAdapter:  real GSP integration (requires creds).
/// </summary>
public interface IEwayBillGspAdapter
{
    string AdapterName { get; }

    Task<EwbAdapterResult> FetchAsync(string ewbNumber, CancellationToken ct = default);
    Task<EwbAdapterResult> GenerateAsync(GenerateEwbRequestDto request, CancellationToken ct = default);
    Task<EwbAdapterResult> UpdateVehicleAsync(UpdateVehicleRequestDto request, CancellationToken ct = default);
    Task<EwbAdapterResult> CancelAsync(CancelEwbRequestDto request, CancellationToken ct = default);
    Task<EwbAdapterResult> ExtendValidityAsync(ExtendValidityRequestDto request, CancellationToken ct = default);
}

public class EwbAdapterResult
{
    public bool Success { get; set; }
    public string? EwbNumber { get; set; }
    public System.DateTime? EwbDate { get; set; }
    public System.DateTime? ValidUpto { get; set; }
    public string? Status { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
