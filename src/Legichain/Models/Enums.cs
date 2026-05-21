using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Legichain.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RiskLevel
{
    [EnumMember(Value = "low")]    Low,
    [EnumMember(Value = "medium")] Medium,
    [EnumMember(Value = "high")]   High,
    [EnumMember(Value = "severe")] Severe,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Recommendation
{
    [EnumMember(Value = "clear")]  Clear,
    [EnumMember(Value = "review")] Review,
    [EnumMember(Value = "block")]  Block,
}
