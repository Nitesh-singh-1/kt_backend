using KTransport.API.Authorization;
using KTransport.API.DTOs;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequireSuperUser]
    public class ConfigurationController : ControllerBase
    {
        private readonly ITenantConfigurationService _configService;
        private readonly ITenantContext _tenantContext;
        private readonly IFileStorage _storage;
        private readonly FileStorageOptions _storageOptions;
        private readonly ILogger<ConfigurationController> _logger;

        public ConfigurationController(
            ITenantConfigurationService configService,
            ITenantContext tenantContext,
            IFileStorage storage,
            Microsoft.Extensions.Options.IOptions<FileStorageOptions> storageOptions,
            ILogger<ConfigurationController> logger)
        {
            _configService = configService;
            _tenantContext = tenantContext;
            _storage = storage;
            _storageOptions = storageOptions.Value;
            _logger = logger;
        }

        /// <summary>
        /// Public endpoint to get tenant branding and theme by tenant code/domain (for login or bootstrap).
        /// </summary>
        [HttpGet("public/{code}")]
        [AllowAnonymous]
        public async Task<ActionResult<PublicTenantConfigDto>> GetPublicConfig(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return BadRequest(new { message = "Tenant code or domain is required." });
            }

            var config = await _configService.GetPublicTenantConfigAsync(code);
            if (config == null)
            {
                return NotFound(new { message = "Tenant configuration not found." });
            }

            return Ok(config);
        }

        /// <summary>
        /// Get full configuration for the currently authenticated tenant.
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<TenantConfigurationDto>> GetCurrentTenantConfiguration()
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var config = await _configService.GetTenantConfigurationAsync(tenantId);
            return Ok(config);
        }

        /// <summary>
        /// Update configuration for the currently authenticated tenant.
        /// </summary>
        [HttpPut]
        [Authorize]
        public async Task<ActionResult<TenantConfigurationDto>> UpdateTenantConfiguration([FromBody] UpdateTenantConfigurationDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tenantId = _tenantContext.CurrentTenantId;
            var updatedConfig = await _configService.UpdateTenantConfigurationAsync(tenantId, dto);
            return Ok(updatedConfig);
        }

        /// <summary>
        /// Get active feature flags and module access matrix for the currently authenticated tenant.
        /// </summary>
        [HttpGet("features")]
        [Authorize]
        public async Task<ActionResult<TenantFeatureFlagsDto>> GetFeatureFlags()
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var flags = await _configService.GetFeatureFlagsAsync(tenantId);
            return Ok(flags);
        }

        /// <summary>
        /// Get subscription plan, resource limits, and current usage for the tenant.
        /// </summary>
        [HttpGet("subscription")]
        [Authorize]
        public async Task<ActionResult<TenantSubscriptionDto>> GetSubscription()
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var subscription = await _configService.GetSubscriptionDetailsAsync(tenantId);
            return Ok(subscription);
        }

        /// <summary>
        /// Reset tenant configuration to system default settings.
        /// </summary>
        [HttpPost("reset")]
        [Authorize]
        public async Task<ActionResult<TenantConfigurationDto>> ResetToDefaults()
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var config = await _configService.ResetToDefaultsAsync(tenantId);
            return Ok(config);
        }

        // ============================================================
        //  Logo asset storage (MinIO / S3) — replaces base64-in-DB
        // ============================================================

        private static readonly Dictionary<string, string> AllowedLogoTypes = new()
        {
            ["image/png"] = ".png",
            ["image/jpeg"] = ".jpg",
            ["image/svg+xml"] = ".svg",
            ["image/webp"] = ".webp",
        };

        private const long MaxLogoBytes = 2 * 1024 * 1024; // 2 MB

        /// <summary>
        /// Upload the tenant's logo to object storage and record the reference.
        /// Replaces any previous logo. Returns the absolute serve URL.
        /// </summary>
        [HttpPost("logo")]
        [Authorize]
        [RequestSizeLimit(5 * 1024 * 1024)]
        public async Task<IActionResult> UploadLogo(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file was uploaded." });

            if (file.Length > MaxLogoBytes)
                return BadRequest(new { message = "Logo must be 2 MB or smaller." });

            var contentType = (file.ContentType ?? string.Empty).ToLowerInvariant();
            if (!AllowedLogoTypes.TryGetValue(contentType, out var ext))
                return BadRequest(new { message = "Unsupported file type. Upload a PNG, JPG, SVG, or WEBP image." });

            // Light magic-byte sniff so a mislabeled/renamed file is rejected.
            var head = new byte[12];
            await using (var peek = file.OpenReadStream())
            {
                var read = await peek.ReadAsync(head, 0, head.Length);
                if (!SignatureMatches(head, read, contentType))
                    return BadRequest(new { message = "File contents do not match the declared image type." });
            }

            var tenantId = _tenantContext.CurrentTenantId;
            var objectKey = $"tenants/{tenantId}/logo-{Guid.NewGuid():N}{ext}";

            await using (var upload = file.OpenReadStream())
            {
                await _storage.PutAsync(objectKey, upload, contentType);
            }

            var logoUrl = BuildLogoUrl(tenantId, objectKey);

            var previous = await _configService.GetLogoReferenceAsync(tenantId);
            await _configService.SetLogoAsync(tenantId, objectKey, contentType, logoUrl);

            if (previous.HasValue && !string.Equals(previous.Value.ObjectKey, objectKey, StringComparison.Ordinal))
            {
                try { await _storage.DeleteAsync(previous.Value.ObjectKey); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete replaced logo {Key}", previous.Value.ObjectKey); }
            }

            return Ok(new { logoUrl });
        }

        /// <summary>
        /// Serve a tenant's logo from object storage. Public so it renders on the
        /// pre-auth login screen and on printed Bilties.
        /// </summary>
        [HttpGet("tenants/{tenantId:guid}/logo")]
        [AllowAnonymous]
        public async Task<IActionResult> GetLogo(Guid tenantId)
        {
            var reference = await _configService.GetLogoReferenceAsync(tenantId);
            if (reference == null) return NotFound();

            var stored = await _storage.GetAsync(reference.Value.ObjectKey);
            if (stored == null) return NotFound();

            Response.Headers["Cache-Control"] = "public, max-age=86400";
            return File(stored.Content, reference.Value.ContentType);
        }

        /// <summary>Remove the tenant's logo from object storage and clear the reference.</summary>
        [HttpDelete("logo")]
        [Authorize]
        public async Task<IActionResult> DeleteLogo()
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var removedKey = await _configService.ClearLogoAsync(tenantId);
            if (!string.IsNullOrWhiteSpace(removedKey))
            {
                try { await _storage.DeleteAsync(removedKey!); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete logo object {Key}", removedKey); }
            }
            return Ok(new { success = true });
        }

        /// <summary>
        /// One-time migration: move every tenant's base64 (data-URI) logo out of the DB
        /// and into object storage, replacing GeneralJson.logoUrl with a serve URL.
        /// Platform operator only; safe to re-run (only data: URIs are touched).
        /// </summary>
        [HttpPost("logo/backfill-base64")]
        [Authorize]
        [RequirePlatformAdmin]
        public async Task<IActionResult> BackfillBase64Logos()
        {
            var pending = await _configService.GetTenantsWithBase64LogosAsync();
            var migrated = 0;
            var errors = new List<string>();

            foreach (var (tenantId, dataUri) in pending)
            {
                try
                {
                    if (!TryDecodeDataUri(dataUri, out var bytes, out var contentType, out var ext))
                    {
                        errors.Add($"{tenantId}: unrecognized data URI");
                        continue;
                    }

                    var objectKey = $"tenants/{tenantId}/logo-{Guid.NewGuid():N}{ext}";
                    await using (var ms = new MemoryStream(bytes))
                    {
                        await _storage.PutAsync(objectKey, ms, contentType);
                    }

                    var logoUrl = BuildLogoUrl(tenantId, objectKey);
                    await _configService.SetLogoAsync(tenantId, objectKey, contentType, logoUrl);
                    migrated++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{tenantId}: {ex.Message}");
                }
            }

            return Ok(new { total = pending.Count, migrated, errors });
        }

        // ---- helpers ----

        private string BuildLogoUrl(Guid tenantId, string objectKey)
        {
            // Unique per upload (the key carries a GUID) so browsers/CDNs re-fetch after a
            // change. Absolute so it works as an <img src> from the frontend, print
            // templates, and the pre-auth login screen.
            var version = objectKey.Length >= 12 ? objectKey.Substring(objectKey.Length - 12) : objectKey;
            var baseUrl = !string.IsNullOrWhiteSpace(_storageOptions.PublicApiBaseUrl)
                ? _storageOptions.PublicApiBaseUrl!.TrimEnd('/')
                : $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            return $"{baseUrl}/api/configuration/tenants/{tenantId}/logo?v={Uri.EscapeDataString(version)}";
        }

        private static bool SignatureMatches(byte[] head, int read, string contentType)
        {
            bool StartsWith(params byte[] sig)
            {
                if (read < sig.Length) return false;
                for (var i = 0; i < sig.Length; i++) if (head[i] != sig[i]) return false;
                return true;
            }

            return contentType switch
            {
                "image/png" => StartsWith(0x89, 0x50, 0x4E, 0x47),
                "image/jpeg" => StartsWith(0xFF, 0xD8, 0xFF),
                "image/webp" => read >= 12 && head[0] == (byte)'R' && head[1] == (byte)'I' && head[2] == (byte)'F' && head[3] == (byte)'F'
                                           && head[8] == (byte)'W' && head[9] == (byte)'E' && head[10] == (byte)'B' && head[11] == (byte)'P',
                "image/svg+xml" => true, // text; validated by declared type + extension allowlist
                _ => false,
            };
        }

        private static bool TryDecodeDataUri(string dataUri, out byte[] bytes, out string contentType, out string ext)
        {
            bytes = Array.Empty<byte>();
            contentType = "application/octet-stream";
            ext = ".bin";

            // Expected shape: data:<contentType>;base64,<payload>
            var comma = dataUri.IndexOf(',');
            if (!dataUri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || comma < 0) return false;

            var meta = dataUri.Substring(5, comma - 5); // e.g. "image/png;base64"
            var payload = dataUri.Substring(comma + 1);
            if (!meta.Contains("base64", StringComparison.OrdinalIgnoreCase)) return false;

            contentType = meta.Split(';')[0].Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(contentType)) contentType = "image/png";
            ext = AllowedLogoTypes.TryGetValue(contentType, out var matchedExt) ? matchedExt : ".png";

            try { bytes = Convert.FromBase64String(payload); }
            catch { return false; }

            return bytes.Length > 0;
        }
    }
}
