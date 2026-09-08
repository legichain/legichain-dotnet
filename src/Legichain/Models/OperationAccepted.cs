using System.Text.Json.Serialization;

namespace Legichain.Models;

/// <summary>Durable receipt. Acceptance is not a completed result.</summary>
public sealed record OperationAccepted(
 [property: JsonPropertyName("operation_id")] string OperationId,
 [property: JsonPropertyName("status")] string Status,
 [property: JsonPropertyName("status_url")] string StatusUrl,
 [property: JsonPropertyName("deadline_at")] string DeadlineAt,
 [property: JsonPropertyName("protocol")] int Protocol,
 [property: JsonPropertyName("items")] int? Items = null
);
