using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Legichain.Models;

public sealed record HitFlags(
    [property: JsonPropertyName("sanctions")]             bool Sanctions = false,
    [property: JsonPropertyName("pep")]                   bool Pep = false,
    [property: JsonPropertyName("adverse_media")]         bool AdverseMedia = false,
    [property: JsonPropertyName("sanctions_inferred")]    bool SanctionsInferred = false,
    [property: JsonPropertyName("pep_inferred")]          bool PepInferred = false,
    [property: JsonPropertyName("crypto_wallet")]         bool CryptoWallet = false,
    [property: JsonPropertyName("wanted")]                bool Wanted = false,
    [property: JsonPropertyName("regulatory_action")]     bool RegulatoryAction = false
);

public sealed record Hit(
    [property: JsonPropertyName("id")]                string Id,
    [property: JsonPropertyName("name")]              string Name,
    [property: JsonPropertyName("entity_type")]       string EntityType,
    [property: JsonPropertyName("match_confidence")]  int MatchConfidence,
    [property: JsonPropertyName("risk_level")]        RiskLevel RiskLevel,
    [property: JsonPropertyName("flags")]             HitFlags Flags,
    [property: JsonPropertyName("countries")]         IReadOnlyList<string>? Countries = null,
    [property: JsonPropertyName("aliases")]           IReadOnlyList<string>? Aliases = null,
    [property: JsonPropertyName("birth_date")]        string? BirthDate = null,
    [property: JsonPropertyName("source")]            string? Source = null,
    [property: JsonPropertyName("source_url")]        string? SourceUrl = null,
    [property: JsonPropertyName("sanction_programs")] IReadOnlyList<string>? SanctionPrograms = null,
    [property: JsonPropertyName("pep_role")]          string? PepRole = null,
    [property: JsonPropertyName("pep_country")]       string? PepCountry = null,
    [property: JsonPropertyName("listed_at")]         string? ListedAt = null,
    [property: JsonPropertyName("removed_at")]        string? RemovedAt = null,
    [property: JsonPropertyName("notes")]             string? Notes = null
);

public sealed record ScreeningSummary(
    [property: JsonPropertyName("total_hits")]      int TotalHits,
    [property: JsonPropertyName("risk_level")]      RiskLevel RiskLevel,
    [property: JsonPropertyName("recommendation")]  Recommendation Recommendation,
    [property: JsonPropertyName("top_hit_score")]   int? TopHitScore = null,
    [property: JsonPropertyName("sanctions_count")] int SanctionsCount = 0,
    [property: JsonPropertyName("pep_count")]       int PepCount = 0,
    [property: JsonPropertyName("adverse_count")]   int AdverseCount = 0
);

public sealed record ScreeningResponse(
    [property: JsonPropertyName("screening_id")]       string ScreeningId,
    [property: JsonPropertyName("query_type")]         string QueryType,
    [property: JsonPropertyName("hits")]               IReadOnlyList<Hit> Hits,
    [property: JsonPropertyName("summary")]            ScreeningSummary Summary,
    [property: JsonPropertyName("cost_credits")]       int CostCredits,
    [property: JsonPropertyName("credits_remaining")]  long CreditsRemaining,
    [property: JsonPropertyName("latency_ms")]         int? LatencyMs = null,
    [property: JsonPropertyName("cached")]             bool Cached = false,
    [property: JsonPropertyName("test_mode")]          bool TestMode = false,
    [property: JsonPropertyName("created_at")]         string? CreatedAt = null
);

public sealed record BatchAsyncResponse(
    [property: JsonPropertyName("job_id")]       string JobId,
    [property: JsonPropertyName("status")]       string Status,
    [property: JsonPropertyName("item_count")]   int ItemCount,
    [property: JsonPropertyName("eta_seconds")]  int? EtaSeconds = null
);

public sealed record JobStatus(
    [property: JsonPropertyName("job_id")]    string JobId,
    [property: JsonPropertyName("status")]    string Status,
    [property: JsonPropertyName("progress")]  double Progress = 0.0,
    [property: JsonPropertyName("results")]   IReadOnlyList<ScreeningResponse>? Results = null,
    [property: JsonPropertyName("error")]     string? Error = null
);

public sealed record StatusIncident(
    [property: JsonPropertyName("id")]         string Id,
    [property: JsonPropertyName("title")]      string Title,
    [property: JsonPropertyName("status")]     string Status,
    [property: JsonPropertyName("severity")]   string Severity,
    [property: JsonPropertyName("started_at")] string StartedAt
);

public sealed record StatusPayload(
    [property: JsonPropertyName("overall")]    string Overall,
    [property: JsonPropertyName("services")]   IReadOnlyDictionary<string, string> Services,
    [property: JsonPropertyName("incidents")]  IReadOnlyList<StatusIncident> Incidents,
    [property: JsonPropertyName("checked_at")] string CheckedAt
);
