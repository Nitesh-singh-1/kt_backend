using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KTransport.API.DTOs.Gst;

namespace KTransport.API.Services.Gst;

/// <summary>
/// Deterministic in-process adapter — no network, no credentials.
/// Used for local dev + CI. Produces EWB numbers of the form
/// "MOCK-{yyMMdd}-{6-digit-counter}" and a 24h validity window.
/// Flipped on by setting EWayBill:Adapter=Mock (default).
/// </summary>
public class MockEwayBillGspAdapter : IEwayBillGspAdapter
{
    public string AdapterName => "Mock";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static int _counter;

    private static string NewEwbNumber()
    {
        var seq = System.Threading.Interlocked.Increment(ref _counter);
        return $"MOCK{DateTime.UtcNow:yyMMdd}{seq:D6}";
    }

    public Task<EwbAdapterResult> FetchAsync(string ewbNumber, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ewbNumber))
            return Task.FromResult(new EwbAdapterResult { Success = false, ErrorCode = "INVALID", ErrorMessage = "Empty EWB number" });

        // Mock fetch returns a stub payload echoing the number.
        var payload = JsonSerializer.Serialize(new
        {
            ewbNo = ewbNumber,
            ewbDate = DateTime.UtcNow.AddDays(-1).ToString("dd/MM/yyyy HH:mm:ss"),
            validUpto = DateTime.UtcNow.AddDays(1).ToString("dd/MM/yyyy HH:mm:ss"),
            status = "ACT",
            __mock__ = true
        }, JsonOpts);

        return Task.FromResult(new EwbAdapterResult
        {
            Success = true,
            EwbNumber = ewbNumber,
            EwbDate = DateTime.UtcNow.AddDays(-1),
            ValidUpto = DateTime.UtcNow.AddDays(1),
            Status = "ACT",
            PayloadJson = payload
        });
    }

    public Task<EwbAdapterResult> GenerateAsync(GenerateEwbRequestDto request, CancellationToken ct = default)
    {
        var validity = request.TransDistance > 100 ? DateTime.UtcNow.AddDays(2) : DateTime.UtcNow.AddDays(1);
        var number = NewEwbNumber();
        var payload = JsonSerializer.Serialize(new
        {
            ewbNo = number,
            ewbDate = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss"),
            validUpto = validity.ToString("dd/MM/yyyy HH:mm:ss"),
            status = "ACT",
            __mock__ = true,
            request
        }, JsonOpts);

        return Task.FromResult(new EwbAdapterResult
        {
            Success = true,
            EwbNumber = number,
            EwbDate = DateTime.UtcNow,
            ValidUpto = validity,
            Status = "ACT",
            PayloadJson = payload
        });
    }

    public Task<EwbAdapterResult> UpdateVehicleAsync(UpdateVehicleRequestDto request, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            ewbNo = request.EwbNumber,
            vehicleNo = request.VehicleNo,
            validUpto = DateTime.UtcNow.AddDays(1).ToString("dd/MM/yyyy HH:mm:ss"),
            __mock__ = true
        }, JsonOpts);
        return Task.FromResult(new EwbAdapterResult
        {
            Success = true,
            EwbNumber = request.EwbNumber,
            ValidUpto = DateTime.UtcNow.AddDays(1),
            Status = "ACT",
            PayloadJson = payload
        });
    }

    public Task<EwbAdapterResult> CancelAsync(CancelEwbRequestDto request, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            ewbNo = request.EwbNumber,
            cancelDate = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss"),
            __mock__ = true
        }, JsonOpts);
        return Task.FromResult(new EwbAdapterResult
        {
            Success = true,
            EwbNumber = request.EwbNumber,
            Status = "CNL",
            PayloadJson = payload
        });
    }

    public Task<EwbAdapterResult> ExtendValidityAsync(ExtendValidityRequestDto request, CancellationToken ct = default)
    {
        var newValidity = DateTime.UtcNow.AddDays(1);
        var payload = JsonSerializer.Serialize(new
        {
            ewbNo = request.EwbNumber,
            validUpto = newValidity.ToString("dd/MM/yyyy HH:mm:ss"),
            __mock__ = true
        }, JsonOpts);
        return Task.FromResult(new EwbAdapterResult
        {
            Success = true,
            EwbNumber = request.EwbNumber,
            ValidUpto = newValidity,
            Status = "ACT",
            PayloadJson = payload
        });
    }
}
