using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KTransport.API.Data;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    /// <summary>
    /// Default <see cref="IDocumentStorage"/>. Resolves the tenant's folder from its
    /// <c>Code</c> (stable, unique, filesystem-safe) and delegates the actual bytes to
    /// <see cref="IFileStorage"/>. See the interface for the layout contract.
    /// </summary>
    public class DocumentStorage : IDocumentStorage
    {
        private readonly IFileStorage _files;
        private readonly KTransportDbContext _db;

        public DocumentStorage(IFileStorage files, KTransportDbContext db)
        {
            _files = files;
            _db = db;
        }

        public async Task<StoredDocumentRef> StoreAsync(Guid tenantId, string path, Stream content, string contentType, CancellationToken ct = default)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            var folder = await GetTenantFolderAsync(tenantId, ct);
            var key = $"{folder}/{SanitizePath(path)}";
            await _files.PutAsync(key, content, contentType, ct); // PUT overwrites an existing key in place.
            return new StoredDocumentRef(key);
        }

        public Task<StoredFile?> GetAsync(string objectKey, CancellationToken ct = default)
            => _files.GetAsync(objectKey, ct);

        public Task DeleteAsync(string objectKey, CancellationToken ct = default)
            => _files.DeleteAsync(objectKey, ct);

        public async Task<string> GetTenantFolderAsync(Guid tenantId, CancellationToken ct = default)
        {
            var code = await _db.Tenants.IgnoreQueryFilters()
                .Where(t => t.Id == tenantId)
                .Select(t => t.Code)
                .FirstOrDefaultAsync(ct);

            // Fall back to the id so a document can never be stored folder-less, even if
            // Code is somehow blank. The id form is still unique and safe.
            var folder = SanitizeSegment(string.IsNullOrWhiteSpace(code) ? tenantId.ToString("N") : code!);
            return string.IsNullOrEmpty(folder) ? tenantId.ToString("N") : folder;
        }

        /// <summary>
        /// Sanitise a multi-segment tenant-relative path: each segment is cleaned, empties and
        /// traversal (".", "..") are dropped. Keeps A–Z a–z 0–9 . _ - and the segment
        /// separators, so a key can never escape the tenant folder.
        /// </summary>
        private static string SanitizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return Guid.NewGuid().ToString("N");
            var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var clean = segments
                .Select(SanitizeSegment)
                .Where(s => s.Length > 0 && s != "." && s != "..")
                .ToArray();
            return clean.Length == 0 ? Guid.NewGuid().ToString("N") : string.Join('/', clean);
        }

        private static string SanitizeSegment(string? segment)
        {
            if (string.IsNullOrWhiteSpace(segment)) return string.Empty;
            var sb = new StringBuilder(segment.Length);
            foreach (var ch in segment.Trim())
            {
                if (char.IsLetterOrDigit(ch) || ch == '.' || ch == '_' || ch == '-')
                    sb.Append(ch);
                else if (ch == ' ')
                    sb.Append('-');
                // everything else is dropped
            }
            // collapse to avoid a segment that is only dots (e.g. "..")
            var result = sb.ToString().Trim('-');
            return result.All(c => c == '.') ? string.Empty : result;
        }
    }
}
