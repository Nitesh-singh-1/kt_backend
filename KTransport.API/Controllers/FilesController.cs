using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KTransport.API.Controllers
{
    /// <summary>
    /// Reusable document upload/serve over tenant-aware object storage (MinIO/S3).
    /// Replaces the old base64-in-DB pattern for money-critical documents (bilty
    /// tax-invoice scans, PODs). The upload returns the canonical object key; the
    /// owning row persists it in its <c>document_path</c> column and renders the file
    /// via the serve URL. Every object lives under the caller's tenant folder, and the
    /// serve endpoint refuses any key outside it, so one tenant can never read another's
    /// documents.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FilesController : ControllerBase
    {
        private readonly IDocumentStorage _docs;
        private readonly ITenantContext _tenant;
        private readonly FileStorageOptions _storageOptions;
        private readonly ILogger<FilesController> _logger;

        private const long MaxBytes = 10 * 1024 * 1024; // 10 MB

        private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/png"] = ".png",
            ["image/jpeg"] = ".jpg",
            ["image/webp"] = ".webp",
            ["application/pdf"] = ".pdf",
        };

        public FilesController(
            IDocumentStorage docs,
            ITenantContext tenant,
            IOptions<FileStorageOptions> storageOptions,
            ILogger<FilesController> logger)
        {
            _docs = docs;
            _tenant = tenant;
            _storageOptions = storageOptions.Value;
            _logger = logger;
        }

        /// <summary>
        /// Upload a document. Multipart form: <c>file</c> (required), <c>documentType</c>
        /// (required, e.g. TaxInvoice / POD / Logo), <c>reference</c> (optional, e.g. the GR
        /// number — groups files under a readable sub-folder). Returns the object key to
        /// persist and the serve URL to display.
        /// </summary>
        [HttpPost]
        [RequestSizeLimit(12 * 1024 * 1024)]
        public async Task<IActionResult> Upload(
            IFormFile file,
            [FromForm] string documentType,
            [FromForm] string? reference = null)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file was uploaded." });

            if (file.Length > MaxBytes)
                return BadRequest(new { message = "File must be 10 MB or smaller." });

            if (string.IsNullOrWhiteSpace(documentType))
                return BadRequest(new { message = "documentType is required." });

            var contentType = (file.ContentType ?? string.Empty).ToLowerInvariant();
            if (!AllowedTypes.TryGetValue(contentType, out var ext))
                return BadRequest(new { message = "Unsupported file type. Upload a PNG, JPG, WEBP, or PDF." });

            var tenantId = _tenant.CurrentTenantId;

            // Build a readable, tenant-relative path. A per-upload GUID makes the key unique
            // so a browser/CDN re-fetches after a replace; `reference` (e.g. GR number) groups
            // files for easy auditing in the MinIO console.
            var parts = new List<string> { documentType };
            if (!string.IsNullOrWhiteSpace(reference)) parts.Add(reference!);
            parts.Add($"{Guid.NewGuid():N}{ext}");
            var relativePath = string.Join('/', parts);

            StoredDocumentRef stored;
            await using (var upload = file.OpenReadStream())
            {
                stored = await _docs.StoreAsync(tenantId, relativePath, upload, contentType);
            }

            return Ok(new
            {
                objectKey = stored.ObjectKey,
                url = BuildServeUrl(stored.ObjectKey),
                contentType,
            });
        }

        /// <summary>
        /// Serve a stored document by its object key. Tenant-scoped: the key must sit under the
        /// caller's tenant folder, otherwise 404 (indistinguishable from "missing", so it leaks
        /// nothing about other tenants' keys).
        /// </summary>
        [HttpGet("{**objectKey}")]
        public async Task<IActionResult> Get(string objectKey)
        {
            if (string.IsNullOrWhiteSpace(objectKey))
                return NotFound();

            var tenantFolder = await _docs.GetTenantFolderAsync(_tenant.CurrentTenantId);
            if (!objectKey.StartsWith(tenantFolder + "/", StringComparison.Ordinal))
            {
                _logger.LogWarning("Blocked cross-tenant document access to key {Key} by tenant folder {Folder}.", objectKey, tenantFolder);
                return NotFound();
            }

            var stored = await _docs.GetAsync(objectKey);
            if (stored == null) return NotFound();

            Response.Headers["Cache-Control"] = "private, max-age=3600";
            return File(stored.Content, stored.ContentType);
        }

        private string BuildServeUrl(string objectKey)
        {
            var baseUrl = !string.IsNullOrWhiteSpace(_storageOptions.PublicApiBaseUrl)
                ? _storageOptions.PublicApiBaseUrl!.TrimEnd('/')
                : $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            return $"{baseUrl}/api/files/{objectKey}";
        }
    }
}
