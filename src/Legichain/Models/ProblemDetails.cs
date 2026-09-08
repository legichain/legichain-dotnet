using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Legichain.Models;

/// <summary>RFC 7807 problem body returned by the API on every error.</summary>
public sealed record ProblemDetails(
    [property: JsonPropertyName("type")]     string? Type,
    [property: JsonPropertyName("title")]    string? Title,
    [property: JsonPropertyName("status")]   int? Status,
    [property: JsonPropertyName("detail")]   string? Detail,
    [property: JsonPropertyName("instance")] string? Instance,
    [property: JsonPropertyName("code")]     string? Code,
    // Set on a 421 (REG_001_WRONG_REGION): the region that owns this
    // account, and the host that serves it. The client re-pins there and
    // retries, so callers rarely see this error at all.
    [property: JsonPropertyName("region")]       string? Region = null,
    [property: JsonPropertyName("api_base_url")] string? ApiBaseUrl = null,
    [property: JsonExtensionData]
    IDictionary<string, JsonElement>? Extensions = null
);
