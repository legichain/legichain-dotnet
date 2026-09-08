using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Legichain.Models;

namespace Legichain;

/// <summary>Official client for the Legichain AML/KYC/Travel-Rule API.</summary>
/// <example>
/// <code>
/// using var lc = new LegichainClient(Environment.GetEnvironmentVariable("LEGICHAIN_API_KEY")!);
/// var r = await lc.ScreenPersonAsync(new PersonQuery("Vladimir Putin", "RU", "1952-10-07"));
/// switch (r.Summary.Recommendation)
/// {
///     case Recommendation.Block:  DenyOnboarding();  break;
///     case Recommendation.Review: QueueForCompliance(r.ScreeningId); break;
///     case Recommendation.Clear:  Approve();         break;
/// }
/// </code>
/// </example>
public sealed class LegichainClient : IDisposable
{
    /// <summary>The control plane. It serves every account's login and
    /// billing and knows which region owns the rest, so it is a safe place
    /// to start from when you do not know.</summary>
    private const string DefaultBaseUrl = "https://api.legichain.com";
    private const string Version        = "0.1.0";

    /// <summary>The API refusing a call for an account it does not serve.</summary>
    private const string WrongRegion    = "REG_001_WRONG_REGION";

    /// <summary>Where a region answers when all we have is its code. The
    /// API publishes the real host in its 421 and that is preferred; this
    /// covers a reply naming a region and nothing else.</summary>
    private static string RegionHost(string code) =>
        $"https://{code}-api.legichain.com";

    private readonly HttpClient            _http;
    private readonly bool                  _ownsHttp;
    // Both move by themselves the first time the API says this account
    // is served elsewhere.
    private string                         _baseUrl;
    private string?                        _region;
    private readonly JsonSerializerOptions _jsonOptions;

    public LegichainClient(
        string apiKey,
        string baseUrl = DefaultBaseUrl,
        HttpClient? httpClient = null,
        TimeSpan? requestTimeout = null,
        IReadOnlyDictionary<string, string>? defaultHeaders = null,
        string? region = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("apiKey is required", nameof(apiKey));

        // A region code starts the client at that region's host, saving one
        // redirect on the first call. An explicit baseUrl always wins.
        _region   = region;
        _baseUrl  = (region is not null && baseUrl == DefaultBaseUrl
                        ? RegionHost(region) : baseUrl).TrimEnd('/');
        _ownsHttp = httpClient is null;
        _http     = httpClient ?? new HttpClient();
        if (requestTimeout is { } t) _http.Timeout = t;
        else if (_http.Timeout == default) _http.Timeout = TimeSpan.FromSeconds(30);

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"legichain-dotnet/{Version}");
        if (defaultHeaders is not null)
            foreach (var kv in defaultHeaders)
                _http.DefaultRequestHeaders.TryAddWithoutValidation(kv.Key, kv.Value);

        _jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition     = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
        };
    }

    private Task<OperationAccepted> EnqueueOperationAsync(string path,object body,string key,string? clientToken,CancellationToken ct) {
        if (string.IsNullOrEmpty(key) || Encoding.UTF8.GetByteCount(key)>256)
            throw new ArgumentException("An explicit 1–256 byte idempotency key is required",nameof(key));
        return PostJsonAsync<OperationAccepted>(path,body,key,ct,clientToken);
    }
    private static string OperationKind(string value,params string[] allowed) {
        if (!allowed.Contains(value)) throw new ArgumentException("Unsupported operation kind");
        return value;
    }
    public Task<OperationAccepted> EnqueueScreenAsync(string kind,object body,string key,CancellationToken ct=default)
        => EnqueueOperationAsync("/v2/screen/"+OperationKind(kind,"person","company","crypto","batch"),body,key,null,ct);
    public Task<OperationAccepted> EnqueueReportAsync(string kind,object body,string key,CancellationToken ct=default)
        => EnqueueOperationAsync("/v2/reports/"+OperationKind(kind,"person","company","wallet"),body,key,null,ct);
    public Task<OperationAccepted> EnqueueKycReportAsync(string id,object body,string key,CancellationToken ct=default)
        => EnqueueOperationAsync("/v2/reports/kyc/"+Uri.EscapeDataString(id),body,key,null,ct);
    public Task<OperationAccepted> EnqueueKycEvidenceAsync(string id,string step,object body,string key,string? clientToken=null,CancellationToken ct=default)
        => EnqueueOperationAsync("/v2/kyc/applications/"+Uri.EscapeDataString(id)+"/"+OperationKind(step,"documents","selfie","liveness","nfc"),body,key,clientToken,ct);
    public Task<OperationAccepted> EnqueueAddressSubmitAsync(string id,object body,string key,CancellationToken ct=default)
        => EnqueueOperationAsync("/v2/address-verifications/"+Uri.EscapeDataString(id)+"/submit",body,key,null,ct);
    public Task<JsonElement> OperationAsync(string id,CancellationToken ct=default)
        => GetJsonAsync<JsonElement>("/v2/operations/"+Uri.EscapeDataString(id),ct);
    public Task<JsonElement> OperationTaskAsync(string id,string taskId,CancellationToken ct=default)
        => GetJsonAsync<JsonElement>("/v2/operations/"+Uri.EscapeDataString(id)+"/tasks/"+Uri.EscapeDataString(taskId),ct);
    public Task<JsonElement> OperationsAsync(string? cursor=null,string? state=null,int limit=50,CancellationToken ct=default)
        => GetJsonAsync<JsonElement>("/v2/operations?limit="+limit+(cursor==null?"":"&cursor="+Uri.EscapeDataString(cursor))+(state==null?"":"&state="+Uri.EscapeDataString(state)),ct);
    public Task<JsonElement> CancelOperationAsync(string id,CancellationToken ct=default)
        => PostJsonAsync<JsonElement>("/v2/operations/"+Uri.EscapeDataString(id)+"/cancel",new {},null,ct);

    // ── Screening ────────────────────────────────────────────────
    public Task<ScreeningResponse> ScreenPersonAsync(
        PersonQuery query, string? idempotencyKey = null, CancellationToken ct = default)
        => PostJsonAsync<ScreeningResponse>("/v1/screen/person", query, idempotencyKey, ct);

    public Task<ScreeningResponse> ScreenCompanyAsync(
        CompanyQuery query, string? idempotencyKey = null, CancellationToken ct = default)
        => PostJsonAsync<ScreeningResponse>("/v1/screen/company", query, idempotencyKey, ct);

    public Task<ScreeningResponse> ScreenCryptoAsync(
        CryptoQuery query, string? idempotencyKey = null, CancellationToken ct = default)
        => PostJsonAsync<ScreeningResponse>("/v1/screen/crypto", query, idempotencyKey, ct);

    public Task<IReadOnlyList<ScreeningResponse>> ScreenBatchAsync(
        IEnumerable<object> items, string? idempotencyKey = null, CancellationToken ct = default)
        => PostJsonAsync<IReadOnlyList<ScreeningResponse>>(
               "/v1/screen/batch", new { items = items.ToArray() }, idempotencyKey, ct);

    public Task<BatchAsyncResponse> ScreenBatchAsyncJobAsync(
        IEnumerable<object> items, string? webhookUrl = null,
        string? idempotencyKey = null, CancellationToken ct = default)
        => PostJsonAsync<BatchAsyncResponse>(
               "/v1/screen/batch/async",
               new { items = items.ToArray(), webhook_url = webhookUrl },
               idempotencyKey, ct);

    public Task<JobStatus> GetJobAsync(string jobId, CancellationToken ct = default)
        => GetJsonAsync<JobStatus>($"/v1/screen/jobs/{Uri.EscapeDataString(jobId)}", ct);

    // ── PDF reports ──────────────────────────────────────────────
    public Task<byte[]> ReportWalletAsync(CryptoQuery query, CancellationToken ct = default)
        => PostJsonForBytesAsync("/v1/reports/wallet", query, ct);

    public Task<byte[]> ReportPersonAsync(PersonQuery query, CancellationToken ct = default)
        => PostJsonForBytesAsync("/v1/reports/person", query, ct);

    public Task<byte[]> ReportCompanyAsync(CompanyQuery query, CancellationToken ct = default)
        => PostJsonForBytesAsync("/v1/reports/company", query, ct);

    // ── KYC SDK ──────────────────────────────────────────────────
    // Server-side semantics: this SDK does NOT read NFC chips. Mobile
    // clients (Flutter / React-Native / iOS / Android SDKs) extract
    // SOD + DG bytes; forward them here as base64 strings.

    public Task<Dictionary<string, object?>> KycCreateApplicationAsync(
        object body, string? idempotencyKey = null, CancellationToken ct = default)
        => PostJsonAsync<Dictionary<string, object?>>(
               "/v1/kyc/applications", body, idempotencyKey, ct);

    public Task<Dictionary<string, object?>> KycStatusAsync(
        string applicationId, bool includeExtracted = false, CancellationToken ct = default)
        => GetJsonAsync<Dictionary<string, object?>>(
               $"/v1/kyc/applications/{Uri.EscapeDataString(applicationId)}/status"
               + (includeExtracted ? "?include_extracted=true" : ""), ct);

    public Task<Dictionary<string, object?>> KycUploadDocumentAsync(
        string applicationId, string clientToken, object body, CancellationToken ct = default)
        => PostJsonAsync<Dictionary<string, object?>>(
               $"/v1/kyc/applications/{Uri.EscapeDataString(applicationId)}/documents",
               body, idempotencyKey: null, ct, clientToken);

    /// <summary>Forward an NFC chip read produced by a mobile client.
    /// Pass base64 strings for sod/dg/aa payloads. Set
    /// access_error=true (with no SOD) when the mobile reader
    /// couldn't get the chip to respond.</summary>
    public Task<Dictionary<string, object?>> KycSubmitNfcAsync(
        string applicationId, string clientToken, object body, CancellationToken ct = default)
        => PostJsonAsync<Dictionary<string, object?>>(
               $"/v1/kyc/applications/{Uri.EscapeDataString(applicationId)}/nfc",
               body, idempotencyKey: null, ct, clientToken);

    public Task<Dictionary<string, object?>> KycNfcAccessErrorAsync(
        string applicationId, string clientToken,
        string protocol = "PACE", string code = "chip_not_responding",
        CancellationToken ct = default)
        => KycSubmitNfcAsync(applicationId, clientToken,
               new { protocol, access_error = true, access_error_code = code }, ct);

    public Task<Dictionary<string, object?>> KycUploadSelfieAsync(
        string applicationId, string clientToken, object body, CancellationToken ct = default)
        => PostJsonAsync<Dictionary<string, object?>>(
               $"/v1/kyc/applications/{Uri.EscapeDataString(applicationId)}/selfie",
               body, idempotencyKey: null, ct, clientToken);

    public Task<Dictionary<string, object?>> KycLivenessChallengeAsync(
        string applicationId, string clientToken,
        int length = 3, int ttlSeconds = 60, CancellationToken ct = default)
        => PostJsonAsync<Dictionary<string, object?>>(
               $"/v1/kyc/applications/{Uri.EscapeDataString(applicationId)}/liveness/challenge",
               new { length, ttl_seconds = ttlSeconds },
               idempotencyKey: null, ct, clientToken);

    public Task<Dictionary<string, object?>> KycSubmitLivenessAsync(
        string applicationId, string clientToken, object body, CancellationToken ct = default)
        => PostJsonAsync<Dictionary<string, object?>>(
               $"/v1/kyc/applications/{Uri.EscapeDataString(applicationId)}/liveness",
               body, idempotencyKey: null, ct, clientToken);

    public Task<Dictionary<string, object?>> KycSubmitAsync(
        string applicationId, string clientToken, CancellationToken ct = default)
        => PostJsonAsync<Dictionary<string, object?>>(
               $"/v1/kyc/applications/{Uri.EscapeDataString(applicationId)}/submit",
               new {}, idempotencyKey: null, ct, clientToken);

    public Task<Dictionary<string, object?>> KycRetryAsync(
        string applicationId, string clientToken,
        string? reason = null, CancellationToken ct = default)
        => PostJsonAsync<Dictionary<string, object?>>(
               $"/v1/kyc/applications/{Uri.EscapeDataString(applicationId)}/retry",
               reason is null ? new {} : (object) new { reason },
               idempotencyKey: null, ct, clientToken);

    public Task<Dictionary<string, object?>> KycExtendTtlAsync(
        string applicationId, string clientToken, CancellationToken ct = default)
        => PostJsonAsync<Dictionary<string, object?>>(
               $"/v1/kyc/applications/{Uri.EscapeDataString(applicationId)}/extend-ttl",
               new {}, idempotencyKey: null, ct, clientToken);

    // ── KYC tenant admin (compliance officer) ───────────────────
    public Task<Dictionary<string, object?>> KycAdminListAsync(
        string? state = null, string? intent = null, string? personaId = null,
        bool? nfcRequired = null, int limit = 50, string? cursor = null,
        CancellationToken ct = default)
    {
        var qs = new List<string>();
        if (state    is not null) qs.Add($"state={Uri.EscapeDataString(state)}");
        if (intent   is not null) qs.Add($"intent={Uri.EscapeDataString(intent)}");
        if (personaId is not null) qs.Add($"persona_id={Uri.EscapeDataString(personaId)}");
        if (nfcRequired is not null) qs.Add($"nfc_required={(nfcRequired.Value ? "true" : "false")}");
        qs.Add($"limit={limit}");
        if (cursor is not null) qs.Add($"cursor={Uri.EscapeDataString(cursor)}");
        return GetJsonAsync<Dictionary<string, object?>>(
            "/v1/admin/kyc/applications?" + string.Join("&", qs), ct);
    }

    public Task<Dictionary<string, object?>> KycAdminDetailAsync(
        string applicationId, CancellationToken ct = default)
        => GetJsonAsync<Dictionary<string, object?>>(
               $"/v1/admin/kyc/applications/{Uri.EscapeDataString(applicationId)}", ct);

    public Task<Dictionary<string, object?>> KycAdminApproveAsync(
        string applicationId, string? notes = null, bool resetRisk = false,
        CancellationToken ct = default)
        => PostJsonAsync<Dictionary<string, object?>>(
               $"/v1/admin/kyc/applications/{Uri.EscapeDataString(applicationId)}/approve",
               new { notes, reset_risk = resetRisk }, idempotencyKey: null, ct);

    public Task<Dictionary<string, object?>> KycAdminRejectAsync(
        string applicationId, string reasonCode, string? notes = null,
        CancellationToken ct = default)
        => PostJsonAsync<Dictionary<string, object?>>(
               $"/v1/admin/kyc/applications/{Uri.EscapeDataString(applicationId)}/reject",
               new { reason_code = reasonCode, notes }, idempotencyKey: null, ct);

    public Task<Dictionary<string, object?>> KycAdminRequestRetryAsync(
        string applicationId, string? notes = null, CancellationToken ct = default)
        => PostJsonAsync<Dictionary<string, object?>>(
               $"/v1/admin/kyc/applications/{Uri.EscapeDataString(applicationId)}/request-retry",
               new { notes }, idempotencyKey: null, ct);

    // ── Address Verification ─────────────────────────────────────
    public Task<Dictionary<string, object?>> AddressVerificationCreateAsync(
        object body, CancellationToken ct = default)
        => PostJsonAsync<Dictionary<string, object?>>(
               "/v1/address-verifications", body, idempotencyKey: null, ct);

    public Task<Dictionary<string, object?>> AddressVerificationUploadProofAsync(
        string verificationId, string clientToken, object body, CancellationToken ct = default)
        => PostJsonAsync<Dictionary<string, object?>>(
               $"/v1/address-verifications/{Uri.EscapeDataString(verificationId)}/proof",
               body, idempotencyKey: null, ct, clientToken);

    public Task<Dictionary<string, object?>> AddressVerificationSubmitAsync(
        string verificationId, string clientToken, CancellationToken ct = default)
        => PostJsonAsync<Dictionary<string, object?>>(
               $"/v1/address-verifications/{Uri.EscapeDataString(verificationId)}/submit",
               new {}, idempotencyKey: null, ct, clientToken);

    public Task<Dictionary<string, object?>> AddressVerificationStatusAsync(
        string verificationId, CancellationToken ct = default)
        => GetJsonAsync<Dictionary<string, object?>>(
               $"/v1/address-verifications/{Uri.EscapeDataString(verificationId)}/status", ct);

    // ── Personas ─────────────────────────────────────────────────
    public Task<Dictionary<string, object?>> PersonaCreateAsync(
        object body, CancellationToken ct = default)
        => PostJsonAsync<Dictionary<string, object?>>(
               "/v1/personas", body, idempotencyKey: null, ct);

    public Task<Dictionary<string, object?>> PersonaListAsync(
        string? subjectExternalId = null, int limit = 50, string? cursor = null,
        CancellationToken ct = default)
    {
        var qs = new List<string> { $"limit={limit}" };
        if (subjectExternalId is not null)
            qs.Add($"subject_external_id={Uri.EscapeDataString(subjectExternalId)}");
        if (cursor is not null) qs.Add($"cursor={Uri.EscapeDataString(cursor)}");
        return GetJsonAsync<Dictionary<string, object?>>(
            "/v1/personas?" + string.Join("&", qs), ct);
    }

    public Task<Dictionary<string, object?>> PersonaGetAsync(
        string personaId, CancellationToken ct = default)
        => GetJsonAsync<Dictionary<string, object?>>(
               $"/v1/personas/{Uri.EscapeDataString(personaId)}", ct);

    // ── Misc ─────────────────────────────────────────────────────
    public Task<StatusPayload> GetStatusAsync(CancellationToken ct = default)
        => GetJsonAsync<StatusPayload>("/v1/status", ct);

    /// <summary>The host this client is currently talking to. Moves by
    /// itself the first time the API says the account lives elsewhere.</summary>
    public string BaseUrl => _baseUrl;

    /// <summary>The region serving this account, null until known.</summary>
    public string? Region => _region;

    // ── Internals ────────────────────────────────────────────────
    private async Task<T> PostJsonAsync<T>(
        string path, object body, string? idempotencyKey, CancellationToken ct,
        string? clientToken = null)
    {
        // Built on demand rather than once: a 421 moves the base URL, and
        // the retry has to be addressed to where the account actually is.
        using var resp = await SendAsync(() =>
        {
            var req  = new HttpRequestMessage(HttpMethod.Post, _baseUrl + path);
            var json = JsonSerializer.Serialize(body, _jsonOptions);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            if (!string.IsNullOrEmpty(idempotencyKey))
                req.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            if (!string.IsNullOrEmpty(clientToken))
                req.Headers.TryAddWithoutValidation("X-KYC-Client-Token", clientToken);
            return req;
        }, ct).ConfigureAwait(false);
        return await ReadJsonAsync<T>(resp, ct).ConfigureAwait(false);
    }

    private async Task<T> GetJsonAsync<T>(
        string path, CancellationToken ct, string? clientToken = null)
    {
        using var resp = await SendAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Get, _baseUrl + path);
            if (!string.IsNullOrEmpty(clientToken))
                req.Headers.TryAddWithoutValidation("X-KYC-Client-Token", clientToken);
            return req;
        }, ct).ConfigureAwait(false);
        return await ReadJsonAsync<T>(resp, ct).ConfigureAwait(false);
    }

    private async Task<byte[]> PostJsonForBytesAsync(string path, object body, CancellationToken ct)
    {
        using var resp = await SendAsync(() =>
        {
            var req  = new HttpRequestMessage(HttpMethod.Post, _baseUrl + path);
            var json = JsonSerializer.Serialize(body, _jsonOptions);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/pdf"));
            return req;
        }, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) await ThrowFromAsync(resp, ct).ConfigureAwait(false);
        return await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> build, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var req = build();
            HttpResponseMessage resp;
            try
            {
                resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                                  .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new LegichainNetworkException(ex.Message, ex);
            }

            if (attempt == 0 && (int)resp.StatusCode == 421
                && await TryRepinAsync(resp, ct).ConfigureAwait(false))
            {
                resp.Dispose();
                continue;
            }
            return resp;
        }
    }

    /// <summary>Move to the host that owns this account, reporting whether
    /// the caller should repeat the request. Both 421s the API can send are
    /// produced before the request reaches a handler, so nothing was written
    /// and repeating it is not a duplicate.</summary>
    private async Task<bool> TryRepinAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        // Buffer the body: if this turns out not to be a redirect, the error
        // path still has to read it, and the stream is only good once.
        string raw;
        try
        {
            raw = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
        catch { return false; }
        resp.Content = new StringContent(raw, Encoding.UTF8, "application/problem+json");

        ProblemDetails? problem;
        try
        {
            problem = JsonSerializer.Deserialize<ProblemDetails>(raw, _jsonOptions);
        }
        catch { return false; }
        if (problem is null || problem.Code != WrongRegion) return false;

        var target = problem.ApiBaseUrl;
        if (string.IsNullOrEmpty(target) && !string.IsNullOrEmpty(problem.Region))
            target = RegionHost(problem.Region!);
        if (string.IsNullOrEmpty(target)) return false;

        target = target!.TrimEnd('/');
        if (target == _baseUrl) return false;

        _baseUrl = target;
        if (!string.IsNullOrEmpty(problem.Region)) _region = problem.Region;
        return true;
    }

    private async Task<T> ReadJsonAsync<T>(HttpResponseMessage resp, CancellationToken ct)
    {
        if (!resp.IsSuccessStatusCode) await ThrowFromAsync(resp, ct).ConfigureAwait(false);
        var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var parsed = await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, ct)
                                         .ConfigureAwait(false);
        return parsed ?? throw new LegichainException(
            "INVALID_RESPONSE", (int)resp.StatusCode, "empty response body");
    }

    private async Task ThrowFromAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        var status = (int)resp.StatusCode;
        ProblemDetails? problem = null;
        string? detail = null;
        string  code   = $"HTTP_{status}";
        try
        {
            var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
            problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(stream, _jsonOptions, ct)
                                          .ConfigureAwait(false);
            if (problem is not null)
            {
                detail = problem.Detail ?? problem.Title;
                code   = problem.Code ?? code;
            }
        }
        catch { /* not RFC 7807 — fall through */ }

        throw new LegichainException(code, status, detail, problem);
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}
