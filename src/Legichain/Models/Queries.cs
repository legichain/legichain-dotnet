using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Legichain.Models;

/// <summary>Person screening query — only <c>Name</c> is required.</summary>
public sealed record PersonQuery(
    [property: JsonPropertyName("name")]             string Name,
    [property: JsonPropertyName("country")]          string? Country = null,
    [property: JsonPropertyName("birth_date")]       string? BirthDate = null,
    [property: JsonPropertyName("address")]          string? Address = null,
    [property: JsonPropertyName("occupation")]       string? Occupation = null,
    [property: JsonPropertyName("industry")]         string? Industry = null,
    [property: JsonPropertyName("aliases")]          IReadOnlyList<string>? Aliases = null,
    [property: JsonPropertyName("national_id")]      string? NationalId = null,
    [property: JsonPropertyName("passport_number")]  string? PassportNumber = null,
    [property: JsonPropertyName("full_name")]        string? FullName = null
);

public sealed record CompanyQuery(
    [property: JsonPropertyName("name")]                 string Name,
    [property: JsonPropertyName("country")]              string? Country = null,
    [property: JsonPropertyName("registration_number")]  string? RegistrationNumber = null,
    [property: JsonPropertyName("aliases")]              IReadOnlyList<string>? Aliases = null
);

public sealed record CryptoQuery(
    [property: JsonPropertyName("address")] string Address,
    [property: JsonPropertyName("chain")]   string? Chain = null
);
