namespace KTransport.API.Models
{
    public class JwtSettings
    {
        public string Secret { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int ExpiryInMinutes { get; set; }

        /// <summary>Lifetime of a refresh token in days (default 7). Refresh tokens rotate on each use.</summary>
        public int RefreshTokenDays { get; set; } = 7;
    }
}