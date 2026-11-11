using System;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.DTOs.Gst;
using KTransport.API.Models.Gst;
using KTransport.API.Services;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services.Gst;

/// <summary>
/// Fronts the EWB adapter and persists to <c>eway_bill_cache</c>. Every
/// call upserts the local row keyed by (tenant, ewb_number). Payload JSON
/// is retained verbatim for §129 dispute evidence; denormalized columns
/// let the Bilty UI list EWBs without reparsing.
/// </summary>
public class EwayBillService : IEwayBillService
{
    private readonly IEwayBillGspAdapter _adapter;
    private readonly KTransportDbContext _db;
    private readonly ITenantContext _tenantContext;

    public EwayBillService(IEwayBillGspAdapter adapter, KTransportDbContext db, ITenantContext tenantContext)
    {
        _adapter = adapter;
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<EwayBillDto?> GetFromCacheAsync(string ewbNumber)
    {
        var row = await _db.EWayBillCache.FirstOrDefaultAsync(e => e.EwbNumber == ewbNumber);
        return row == null ? null : ToDto(row);
    }

    public async Task<EwayBillDto> FetchAsync(string ewbNumber)
    {
        var result = await _adapter.FetchAsync(ewbNumber);
        var row = await UpsertFromAdapterAsync(ewbNumber, result, inputPayload: null,
            syncOnSuccess: EwayBillSyncStatus.FetchedFromNic, shipmentId: null);
        return ToDto(row);
    }

    public async Task<EwayBillDto> GenerateAsync(GenerateEwbRequestDto request, int? userId)
    {
        ValidateShipToIfBillToShipTo(request);

        var result = await _adapter.GenerateAsync(request);
        if (!result.Success || string.IsNullOrWhiteSpace(result.EwbNumber))
            throw new InvalidOperationException($"EWB generation failed: {result.ErrorCode} {result.ErrorMessage}");

        var row = await UpsertFromAdapterAsync(result.EwbNumber!, result, inputPayload: request,
            syncOnSuccess: EwayBillSyncStatus.GeneratedViaApi, shipmentId: request.ShipmentId);
        return ToDto(row);
    }

    public async Task<EwayBillDto> UpdateVehicleAsync(UpdateVehicleRequestDto request)
    {
        var result = await _adapter.UpdateVehicleAsync(request);
        var row = await UpsertFromAdapterAsync(request.EwbNumber, result, inputPayload: request,
            syncOnSuccess: EwayBillSyncStatus.PartBUpdated, shipmentId: null);
        return ToDto(row);
    }

    public async Task<EwayBillDto> CancelAsync(CancelEwbRequestDto request)
    {
        var result = await _adapter.CancelAsync(request);
        var row = await UpsertFromAdapterAsync(request.EwbNumber, result, inputPayload: request,
            syncOnSuccess: EwayBillSyncStatus.Cancelled, shipmentId: null);
        return ToDto(row);
    }

    public async Task<EwayBillDto> ExtendValidityAsync(ExtendValidityRequestDto request)
    {
        var result = await _adapter.ExtendValidityAsync(request);
        var row = await UpsertFromAdapterAsync(request.EwbNumber, result, inputPayload: request,
            syncOnSuccess: EwayBillSyncStatus.ValidityExtended, shipmentId: null);
        return ToDto(row);
    }

    // ---------------- impl ----------------

    private static void ValidateShipToIfBillToShipTo(GenerateEwbRequestDto req)
    {
        if (req.TransactionType >= 2 && string.IsNullOrWhiteSpace(req.ShipToGstin))
        {
            throw new InvalidOperationException(
                "ShipToGstin is mandatory when TransactionType is Bill-To/Ship-To (NIC rule effective 2026-06-15).");
        }
    }

    private async Task<EWayBillCache> UpsertFromAdapterAsync(
        string ewbNumber, EwbAdapterResult result, object? inputPayload,
        EwayBillSyncStatus syncOnSuccess, long? shipmentId)
    {
        if (!_tenantContext.HasTenant)
            throw new InvalidOperationException("Tenant context required to persist EWB.");

        var tenantId = _tenantContext.CurrentTenantId;
        var row = await _db.EWayBillCache.FirstOrDefaultAsync(e => e.EwbNumber == ewbNumber);
        var isNew = row == null;
        row ??= new EWayBillCache
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EwbNumber = ewbNumber,
            CreatedAt = DateTime.UtcNow
        };

        if (result.Success)
        {
            if (result.EwbDate.HasValue) row.EwbDate = result.EwbDate.Value;
            else if (row.EwbDate == default) row.EwbDate = DateTime.UtcNow;
            row.ValidUpto = result.ValidUpto ?? row.ValidUpto;
            row.Status = result.Status ?? row.Status;
            row.SyncStatus = syncOnSuccess;
            row.PayloadJson = result.PayloadJson;
            row.LastError = null;
        }
        else
        {
            row.SyncStatus = EwayBillSyncStatus.Error;
            row.LastError = $"{result.ErrorCode}: {result.ErrorMessage}";
        }

        if (inputPayload is GenerateEwbRequestDto gen)
        {
            row.SupplyType = gen.SupplyType;
            row.SubSupplyType = gen.SubSupplyType;
            row.DocType = gen.DocType;
            row.DocNo = gen.DocNo;
            row.DocDate = gen.DocDate;
            row.FromGstin = gen.FromGstin;
            row.FromStateCode = gen.FromStateCode.ToString("D2");
            row.ToGstin = gen.ToGstin;
            row.ToStateCode = gen.ToStateCode.ToString("D2");
            row.ShipToGstin = gen.ShipToGstin;
            row.TransactionType = gen.TransactionType;
            row.TotalInvoiceValue = gen.TotInvValue;
            row.TransDistance = gen.TransDistance;
            row.VehicleNo = gen.VehicleNo;
            row.TransporterId = gen.TransporterId;
            row.TransporterName = gen.TransporterName;
        }
        else if (inputPayload is UpdateVehicleRequestDto upd)
        {
            row.VehicleNo = upd.VehicleNo;
        }

        if (shipmentId.HasValue) row.ShipmentId = shipmentId;
        row.UpdatedAt = DateTime.UtcNow;

        if (isNew) _db.EWayBillCache.Add(row);
        await _db.SaveChangesAsync();
        return row;
    }

    private static EwayBillDto ToDto(EWayBillCache r) => new()
    {
        Id = r.Id,
        EwbNumber = r.EwbNumber,
        EwbDate = r.EwbDate,
        ValidUpto = r.ValidUpto,
        Status = r.Status,
        SyncStatus = r.SyncStatus.ToString(),
        SupplyType = r.SupplyType,
        SubSupplyType = r.SubSupplyType,
        DocType = r.DocType,
        DocNo = r.DocNo,
        DocDate = r.DocDate,
        FromGstin = r.FromGstin,
        ToGstin = r.ToGstin,
        ShipToGstin = r.ShipToGstin,
        TransactionType = r.TransactionType,
        TotalInvoiceValue = r.TotalInvoiceValue,
        TransDistance = r.TransDistance,
        VehicleNo = r.VehicleNo,
        TransporterId = r.TransporterId,
        TransporterName = r.TransporterName,
        ShipmentId = r.ShipmentId,
        PayloadJson = r.PayloadJson,
        LastError = r.LastError,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt
    };
}
