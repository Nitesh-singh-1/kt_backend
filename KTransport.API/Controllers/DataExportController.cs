using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.Data;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Controllers
{
    /// <summary>
    /// Per-tenant data export (portability / GDPR-style). Returns the CALLING tenant's core business
    /// data as a downloadable JSON bundle. Tenant-scoped: the EF global query filters restrict every
    /// query to the caller's own tenant, so an admin can only export their own organization's data.
    /// Excludes auth/internal tables (users, tokens, audit, subscription).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequireSuperUser]
    public class DataExportController : ControllerBase
    {
        private readonly KTransportDbContext _context;
        private readonly ITenantContext _tenantContext;
        private readonly IAuditLogService _auditLogService;

        public DataExportController(KTransportDbContext context, ITenantContext tenantContext, IAuditLogService auditLogService)
        {
            _context = context;
            _tenantContext = tenantContext;
            _auditLogService = auditLogService;
        }

        [HttpGet]
        public async Task<IActionResult> Export()
        {
            var tenantId = _tenantContext.CurrentTenantId;

            // Query filters scope each set to the caller's tenant automatically.
            var bundle = new
            {
                exportedAt = DateTime.UtcNow,
                tenantId,
                format = "ktransport-export-v1",
                data = new
                {
                    parties = await _context.Parties.AsNoTracking().ToListAsync(),
                    vendors = await _context.Vendors.AsNoTracking().ToListAsync(),
                    vehicles = await _context.Vehicles.AsNoTracking().ToListAsync(),
                    drivers = await _context.Drivers.AsNoTracking().ToListAsync(),
                    locations = await _context.Locations.AsNoTracking().ToListAsync(),
                    shipments = await _context.Shipments.AsNoTracking().ToListAsync(),
                    shipmentItems = await _context.ShipmentItems.AsNoTracking().ToListAsync(),
                    invoices = await _context.Invoices.AsNoTracking().ToListAsync(),
                    invoiceItems = await _context.InvoiceItems.AsNoTracking().ToListAsync(),
                    trips = await _context.Trips.AsNoTracking().ToListAsync(),
                    challans = await _context.Challans.AsNoTracking().ToListAsync(),
                    gstBills = await _context.GstBills.AsNoTracking().ToListAsync(),
                    podRecords = await _context.PodRecords.AsNoTracking().ToListAsync(),
                    cargoClaims = await _context.CargoClaims.AsNoTracking().ToListAsync(),
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };

            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(bundle, options));

            await _auditLogService.LogAsync("DataExported", tenantId: tenantId, details: $"{bytes.Length} bytes");

            var fileName = $"ktransport-export-{tenantId}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
            return File(bytes, "application/json", fileName);
        }
    }
}
