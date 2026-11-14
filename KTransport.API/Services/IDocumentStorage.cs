using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KTransport.API.Services
{
    /// <summary>The persisted reference to a stored document: its canonical object key.</summary>
    public sealed record StoredDocumentRef(string ObjectKey);

    /// <summary>
    /// Tenant-aware document storage over <see cref="IFileStorage"/> (MinIO/S3).
    ///
    /// Money-critical documents (bilty tax-invoice scans, PODs, tenant logos) are
    /// stored as real objects — never base64 in the database. Objects are laid out
    /// per tenant so the bucket is auditable tenant-by-tenant in the MinIO console:
    ///
    ///     {tenantCode}/{documentType}/{...}        e.g.  ACME/TaxInvoice/GR-2026-0006/invoice-ab12.pdf
    ///
    /// The canonical object key returned here is what the owning row persists in its
    /// <c>document_path</c> column — the single source of truth for retrieval. A PUT to
    /// an existing key overwrites in place (replace-on-same-path), so callers that key a
    /// document slot deterministically get free replacement with no orphans.
    /// </summary>
    public interface IDocumentStorage
    {
        /// <summary>
        /// Store <paramref name="content"/> for <paramref name="tenantId"/> at the tenant-relative
        /// <paramref name="path"/> (e.g. "TaxInvoice/GR-2026-0006/invoice.pdf"). Returns the full
        /// object key, which the caller persists. Overwrites any existing object at the same key.
        /// </summary>
        Task<StoredDocumentRef> StoreAsync(Guid tenantId, string path, Stream content, string contentType, CancellationToken ct = default);

        /// <summary>Fetch a previously stored object by its full key. Null if absent.</summary>
        Task<StoredFile?> GetAsync(string objectKey, CancellationToken ct = default);

        /// <summary>Delete an object by its full key. Idempotent.</summary>
        Task DeleteAsync(string objectKey, CancellationToken ct = default);

        /// <summary>The tenant folder (sanitised tenant Code) a key must sit under to belong to that tenant.</summary>
        Task<string> GetTenantFolderAsync(Guid tenantId, CancellationToken ct = default);
    }
}
