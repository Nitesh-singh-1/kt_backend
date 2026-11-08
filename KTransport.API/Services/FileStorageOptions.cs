namespace KTransport.API.Services
{
    /// <summary>
    /// Binds the <c>Storage</c> configuration section. Targets any S3-compatible
    /// object store — MinIO in self-hosted VPS deployments, AWS S3 (or Azure Blob
    /// via its S3 gateway) in the cloud. Moving MinIO -> S3 is a config-only change.
    /// </summary>
    public class FileStorageOptions
    {
        public const string SectionName = "Storage";

        /// <summary>S3 endpoint. MinIO: http://minio:9000. AWS: leave empty to use the SDK default.</summary>
        public string ServiceUrl { get; set; } = "http://localhost:9000";

        public string AccessKey { get; set; } = "minioadmin";

        public string SecretKey { get; set; } = "minioadmin";

        public string Bucket { get; set; } = "ktransport-assets";

        public string Region { get; set; } = "us-east-1";

        /// <summary>MinIO requires path-style addressing (bucket in the path, not the host).</summary>
        public bool ForcePathStyle { get; set; } = true;

        /// <summary>
        /// Public base URL of this API (e.g. https://api.example.com), used to build the
        /// absolute logo serve URL. Leave empty to derive it from the incoming request
        /// (correct in dev and when the proxy preserves Host); set it when a reverse proxy
        /// rewrites Host so stored logo URLs stay publicly resolvable.
        /// </summary>
        public string? PublicApiBaseUrl { get; set; }
    }
}
