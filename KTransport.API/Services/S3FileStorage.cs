using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KTransport.API.Services
{
    /// <summary>
    /// S3-compatible object storage. Points at MinIO on a self-hosted VPS and at real
    /// AWS S3 in the cloud — the only difference is the <c>Storage</c> config section.
    /// Uses path-style addressing (ForcePathStyle) which MinIO requires.
    /// </summary>
    public class S3FileStorage : IFileStorage
    {
        private readonly IAmazonS3 _s3;
        private readonly FileStorageOptions _opts;
        private readonly ILogger<S3FileStorage> _log;

        public S3FileStorage(IAmazonS3 s3, IOptions<FileStorageOptions> opts, ILogger<S3FileStorage> log)
        {
            _s3 = s3;
            _opts = opts.Value;
            _log = log;
        }

        public async Task PutAsync(string objectKey, Stream content, string contentType, CancellationToken ct = default)
        {
            var request = new PutObjectRequest
            {
                BucketName = _opts.Bucket,
                Key = objectKey,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false,
            };
            await _s3.PutObjectAsync(request, ct);
        }

        public async Task<StoredFile?> GetAsync(string objectKey, CancellationToken ct = default)
        {
            try
            {
                var response = await _s3.GetObjectAsync(_opts.Bucket, objectKey, ct);
                var buffer = new MemoryStream();
                await response.ResponseStream.CopyToAsync(buffer, ct);
                buffer.Position = 0;
                var contentType = string.IsNullOrWhiteSpace(response.Headers.ContentType)
                    ? "application/octet-stream"
                    : response.Headers.ContentType;
                return new StoredFile(buffer, contentType, response.ContentLength);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task DeleteAsync(string objectKey, CancellationToken ct = default)
        {
            try
            {
                await _s3.DeleteObjectAsync(_opts.Bucket, objectKey, ct);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Idempotent: nothing to delete.
                _log.LogDebug("DeleteAsync no-op; object {ObjectKey} already absent.", objectKey);
            }
        }
    }
}
