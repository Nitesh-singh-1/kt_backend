using System;
using KTransport.API.Common;

namespace KTransport.API.Models.Gst;

/// <summary>
/// Per-tenant local cache of E-Way Bill payloads as returned by the NIC
/// adapter. We cache so the Bilty UI can prefill from a fetched EWB
/// without re-hitting NIC on every page load, and so we have a durable
/// record of what we SAW when we generated/updated the EWB (useful on
/// §129 disputes). The actual payload lives in <see cref="PayloadJson"/>
/// as the raw NIC JSON (not transformed) + Status/ValidUpto denormalized
/// for quick list queries.
/// </summary>
public class EWayBillCache : ITenantScopedEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string EwbNumber { get; set; } = string.Empty;
    public DateTime EwbDate { get; set; }
    public DateTime? ValidUpto { get; set; }
    public string? Status { get; set; }                 // EwbStatus.*
    public EwayBillSyncStatus SyncStatus { get; set; } = EwayBillSyncStatus.LocalOnly;

    public string? SupplyType { get; set; }
    public string? SubSupplyType { get; set; }
    public string? DocType { get; set; }
    public string? DocNo { get; set; }
    public DateOnly? DocDate { get; set; }

    public string? FromGstin { get; set; }
    public string? FromStateCode { get; set; }
    public string? ToGstin { get; set; }
    public string? ToStateCode { get; set; }
    public string? ShipToGstin { get; set; }            // mandatory on Bill-To/Ship-To (NIC 2026-06-15)
    public string? ShipToStateCode { get; set; }

    public int? TransactionType { get; set; }           // EwbTransactionType.*
    public decimal? TotalInvoiceValue { get; set; }
    public decimal? TransDistance { get; set; }

    public string? VehicleNo { get; set; }
    public string? TransporterId { get; set; }
    public string? TransporterName { get; set; }

    public string PayloadJson { get; set; } = "{}";
    public string? LastError { get; set; }

    public long? ShipmentId { get; set; }               // optional back-ref

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
