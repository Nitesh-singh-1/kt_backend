using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KTransport.API.Services
{
    /// <summary>A retrieved object: its bytes, content type, and length (if known).</summary>
    public sealed record StoredFile(Stream Content, string ContentType, long? Length);

    /// <summary>
    /// Abstraction over object storage so the rest of the app never talks to a concrete
    /// vendor. The only implementation today is <see cref="S3FileStorage"/> (MinIO/S3);
    /// a local-disk or Azure implementation can be dropped in without touching callers.
    /// </summary>
    public interface IFileStorage
    {
        Task PutAsync(string objectKey, Stream content, string contentType, CancellationToken ct = default);

        /// <summary>Returns null if the object does not exist.</summary>
        Task<StoredFile?> GetAsync(string objectKey, CancellationToken ct = default);

        /// <summary>Idempotent — deleting a missing object is not an error.</summary>
        Task DeleteAsync(string objectKey, CancellationToken ct = default);
    }
}
