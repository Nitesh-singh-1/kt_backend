using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KTransport.API.DTOs.Gst;
using KTransport.API.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KTransport.API.Services.Gst;

/// <summary>
/// Real NIC E-Way Bill adapter via a GSP (Vayana, Masters India, Taxilla,
/// etc.). Implements the published auth + payload flow:
///
///   1. Build 32-byte app_key.
///   2. RSA-encrypt app_key with NIC's PUBLIC key (PEM path in config).
///   3. POST {ACTION:"ACCESSTOKEN", username, password, app_key} as
///      {Data: &lt;RSA-encrypted plaintext base64&gt;} to the auth endpoint.
///   4. Response: {status, authtoken, sek}. Decrypt `sek` using app_key
///      (AES/ECB/PKCS7). Store authtoken + decrypted SEK for ~6h.
///   5. For every subsequent call, AES-encrypt the request JSON with
///      SEK, send as {Data: &lt;cipher&gt;}; decrypt the response's Data
///      field with the same SEK.
///
/// What's SCAFFOLDED here but NOT fully wired (requires manual completion
/// once a GSP is picked and credentials are provisioned):
///   - [MANUAL] NIC public key PEM — download from the GSP's dev portal
///              and set EWayBill:NicPublicKeyPemPath.
///   - [MANUAL] Exact header schema — some GSPs use client-id/client-secret,
///              others (Vayana) use X-Asp-Auth-Token + X-Asp-Auth-Signature.
///              Fill in BuildBaseHeaders() per your GSP's docs.
///   - [MANUAL] Response payload shape per GSP — the NIC-shaped JSON is
///              wrapped differently by each GSP. The caller code assumes
///              {"status","authtoken","sek"} directly; adjust if your GSP
///              nests it (e.g. Vayana puts it under "Data").
///
/// Until the above is done, keep EWayBill:Adapter=Mock. This file compiles
/// and runs; it will fail with a clear "NIC adapter not configured" error
/// at call-time if invoked without creds.
/// </summary>
public class NicEwayBillGspAdapter : IEwayBillGspAdapter
{
    public string AdapterName => "Nic";

    private readonly HttpClient _http;
    private readonly ILogger<NicEwayBillGspAdapter> _logger;
    private readonly EWayBillOptions _opts;

    private string? _authToken;
    private byte[]? _sek;
    private DateTime _tokenExpiresAt = DateTime.MinValue;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public NicEwayBillGspAdapter(IHttpClientFactory httpFactory, IOptions<EWayBillOptions> opts, ILogger<NicEwayBillGspAdapter> logger)
    {
        _opts = opts.Value;
        _logger = logger;
        _http = httpFactory.CreateClient("nic-ewb");
        _http.Timeout = TimeSpan.FromSeconds(_opts.RequestTimeoutSeconds);
        if (!string.IsNullOrWhiteSpace(_opts.BaseUrl))
            _http.BaseAddress = new Uri(_opts.BaseUrl);
    }

    // --------------------- auth ---------------------

    private async Task EnsureAuthenticatedAsync(CancellationToken ct)
    {
        if (_authToken != null && _sek != null && DateTime.UtcNow < _tokenExpiresAt) return;

        await _authLock.WaitAsync(ct);
        try
        {
            if (_authToken != null && _sek != null && DateTime.UtcNow < _tokenExpiresAt) return;

            ValidateConfigOrThrow();

            var appKey = GenerateAppKey();
            var innerPayload = new
            {
                action = "ACCESSTOKEN",
                username = _opts.Username,
                password = _opts.Password,
                app_key = Convert.ToBase64String(appKey)
            };
            var innerJson = JsonSerializer.Serialize(innerPayload, JsonOpts);

            var nicPublicKey = LoadNicPublicKey();
            using var rsa = RSA.Create();
            rsa.ImportFromPem(nicPublicKey.AsSpan());
            var cipher = rsa.Encrypt(Encoding.UTF8.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes(innerJson))), RSAEncryptionPadding.Pkcs1);
            var wire = JsonSerializer.Serialize(new { Data = Convert.ToBase64String(cipher) }, JsonOpts);

            using var req = new HttpRequestMessage(HttpMethod.Post, BuildAuthPath())
            {
                Content = new StringContent(wire, Encoding.UTF8, "application/json")
            };
            AttachGspHeaders(req);
            using var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadAsStringAsync(ct);

            // [MANUAL] If your GSP wraps the response differently, unwrap here.
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var status = root.GetProperty("status").GetString();
            if (status != "1")
                throw new InvalidOperationException($"NIC auth failed: {body}");

            _authToken = root.GetProperty("authtoken").GetString();
            var sekEncrypted = root.GetProperty("sek").GetString() ?? throw new InvalidOperationException("sek missing");

            _sek = AesDecrypt(Convert.FromBase64String(sekEncrypted), appKey);
            _tokenExpiresAt = DateTime.UtcNow.AddMinutes(_opts.TokenRenewMinutes);
            _logger.LogInformation("NIC EWB auth OK, token valid until {Expires}", _tokenExpiresAt);
        }
        finally
        {
            _authLock.Release();
        }
    }

    private void ValidateConfigOrThrow()
    {
        if (string.IsNullOrWhiteSpace(_opts.BaseUrl))
            throw new InvalidOperationException("EWayBill:BaseUrl is not set — complete GSP setup before switching Adapter=Nic.");
        if (string.IsNullOrWhiteSpace(_opts.Username) || string.IsNullOrWhiteSpace(_opts.Password))
            throw new InvalidOperationException("EWayBill:Username/Password is not set.");
        if (string.IsNullOrWhiteSpace(_opts.Gstin))
            throw new InvalidOperationException("EWayBill:Gstin is not set.");
        if (string.IsNullOrWhiteSpace(_opts.NicPublicKeyPemPath) || !File.Exists(_opts.NicPublicKeyPemPath))
            throw new InvalidOperationException("EWayBill:NicPublicKeyPemPath file missing — download the public key from your GSP's dev portal (sandbox vs prod differ).");
    }

    private string BuildAuthPath()
    {
        var provider = _opts.EwbProvider ?? "default";
        return _opts.Sandbox
            ? $"/gus/eway/{provider}/ewaybillapi/v1.03/auth"
            : $"/gus/eway/{provider}/v1.03/auth";
    }

    private string BuildApiPath(string ewbPath)
    {
        var provider = _opts.EwbProvider ?? "default";
        return _opts.Sandbox
            ? $"/gus/eway/{provider}/ewaybillapi/v1.03{ewbPath}"
            : $"/gus/eway/{provider}/v1.03{ewbPath}";
    }

    /// <summary>
    /// [MANUAL] Different GSPs use different header schemes. Pattern A:
    /// client-id / client-secret + gstin. Pattern B (Vayana): X-Asp-Auth-Token
    /// + X-Asp-Auth-Signature. We send BOTH if configured — the GSP that
    /// doesn't recognise one simply ignores it.
    /// </summary>
    private void AttachGspHeaders(HttpRequestMessage req)
    {
        req.Headers.TryAddWithoutValidation("gstin", _opts.Gstin);
        if (!string.IsNullOrWhiteSpace(_opts.ClientId))
            req.Headers.TryAddWithoutValidation("client-id", _opts.ClientId);
        if (!string.IsNullOrWhiteSpace(_opts.ClientSecret))
            req.Headers.TryAddWithoutValidation("client-secret", _opts.ClientSecret);
        if (!string.IsNullOrWhiteSpace(_opts.AspAuthToken))
            req.Headers.TryAddWithoutValidation("X-Asp-Auth-Token", _opts.AspAuthToken);
        if (!string.IsNullOrWhiteSpace(_opts.AspAuthSignatureSecret))
        {
            // [MANUAL] Signature algorithm per GSP docs — this is a
            // placeholder HMAC-SHA256 of the request path+timestamp.
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var toSign = $"{req.Method}:{req.RequestUri?.AbsolutePath}:{ts}";
            using var h = new HMACSHA256(Encoding.UTF8.GetBytes(_opts.AspAuthSignatureSecret));
            var sig = Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(toSign)));
            req.Headers.TryAddWithoutValidation("X-Asp-Auth-Timestamp", ts);
            req.Headers.TryAddWithoutValidation("X-Asp-Auth-Signature", sig);
        }
        if (!string.IsNullOrWhiteSpace(_authToken))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
    }

    private string LoadNicPublicKey() => File.ReadAllText(_opts.NicPublicKeyPemPath!);

    private static byte[] GenerateAppKey()
    {
        var k = new byte[32];
        RandomNumberGenerator.Fill(k);
        return k;
    }

    private static byte[] AesDecrypt(byte[] cipher, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(cipher, 0, cipher.Length);
    }

    private static byte[] AesEncrypt(byte[] plain, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        using var enc = aes.CreateEncryptor();
        return enc.TransformFinalBlock(plain, 0, plain.Length);
    }

    // --------------------- API ops ---------------------

    private async Task<EwbAdapterResult> CallAsync(HttpMethod method, string ewbPath, object? payload, CancellationToken ct)
    {
        try
        {
            await EnsureAuthenticatedAsync(ct);

            using var req = new HttpRequestMessage(method, BuildApiPath(ewbPath));
            if (payload != null)
            {
                var inner = JsonSerializer.Serialize(payload, JsonOpts);
                var encrypted = AesEncrypt(Encoding.UTF8.GetBytes(inner), _sek!);
                var wire = JsonSerializer.Serialize(new { Data = Convert.ToBase64String(encrypted) }, JsonOpts);
                req.Content = new StringContent(wire, Encoding.UTF8, "application/json");
            }
            AttachGspHeaders(req);

            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                return new EwbAdapterResult { Success = false, ErrorCode = $"HTTP_{(int)resp.StatusCode}", ErrorMessage = body };

            // [MANUAL] The response is encrypted when it carries sensitive
            // payload — unwrap "Data" + AES-decrypt with _sek if your GSP
            // uses that envelope. Many GSPs return plaintext JSON for
            // non-sensitive endpoints; try plaintext first.
            string decrypted = body;
            using (var doc = JsonDocument.Parse(body))
            {
                if (doc.RootElement.TryGetProperty("Data", out var dataEl) && dataEl.ValueKind == JsonValueKind.String)
                {
                    var cipher = Convert.FromBase64String(dataEl.GetString()!);
                    decrypted = Encoding.UTF8.GetString(AesDecrypt(cipher, _sek!));
                }
            }

            return ParseEwbResponse(decrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NIC EWB call failed ({Method} {Path})", method, ewbPath);
            return new EwbAdapterResult { Success = false, ErrorCode = "ADAPTER_ERROR", ErrorMessage = ex.Message };
        }
    }

    private static EwbAdapterResult ParseEwbResponse(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var r = doc.RootElement;
            string? ewbNo = TryStr(r, "ewayBillNo") ?? TryStr(r, "ewbNo");
            DateTime? ewbDate = ParseEwbDate(TryStr(r, "ewayBillDate") ?? TryStr(r, "ewbDate"));
            DateTime? validUpto = ParseEwbDate(TryStr(r, "validUpto"));
            string? status = TryStr(r, "status") ?? "ACT";
            return new EwbAdapterResult
            {
                Success = true,
                EwbNumber = ewbNo,
                EwbDate = ewbDate,
                ValidUpto = validUpto,
                Status = status,
                PayloadJson = body
            };
        }
        catch (JsonException)
        {
            return new EwbAdapterResult { Success = false, ErrorCode = "PARSE", ErrorMessage = body };
        }
    }

    private static string? TryStr(JsonElement r, string prop)
        => r.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static DateTime? ParseEwbDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParseExact(s, "dd/MM/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var d))
            return d;
        if (DateTime.TryParse(s, out var d2)) return d2;
        return null;
    }

    public Task<EwbAdapterResult> FetchAsync(string ewbNumber, CancellationToken ct = default)
        => CallAsync(HttpMethod.Get, $"/ewayapi/getewaybill?ewbNo={ewbNumber}", null, ct);

    public Task<EwbAdapterResult> GenerateAsync(GenerateEwbRequestDto request, CancellationToken ct = default)
        => CallAsync(HttpMethod.Post, "/ewayapi/genewaybill", request, ct);

    public Task<EwbAdapterResult> UpdateVehicleAsync(UpdateVehicleRequestDto request, CancellationToken ct = default)
        => CallAsync(HttpMethod.Post, "/ewayapi/vehewb", request, ct);

    public Task<EwbAdapterResult> CancelAsync(CancelEwbRequestDto request, CancellationToken ct = default)
        => CallAsync(HttpMethod.Post, "/ewayapi/canewaybill", request, ct);

    public Task<EwbAdapterResult> ExtendValidityAsync(ExtendValidityRequestDto request, CancellationToken ct = default)
        => CallAsync(HttpMethod.Post, "/ewayapi/extendvalidity", request, ct);
}
