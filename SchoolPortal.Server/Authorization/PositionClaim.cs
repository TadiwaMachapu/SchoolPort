using System.Text.Json;
using System.Text.Json.Serialization;
using SchoolPortal.Data.Entities;

namespace SchoolPortal.Server.Authorization;

/// <summary>
/// Compact representation of one position assignment inside the JWT "pos" claim
/// (STEP 3 Section B). Carries effective dates so the server can enforce expiry
/// statelessly on every request; it deliberately does NOT carry permissions —
/// permissions are derived server-side from the catalogue and never stored in tokens.
/// </summary>
public sealed class PositionClaim
{
    [JsonPropertyName("k")] public string Key { get; set; } = null!;
    [JsonPropertyName("f")] public DateTime EffectiveFrom { get; set; }
    [JsonPropertyName("t")] public DateTime? EffectiveTo { get; set; }
    [JsonPropertyName("s")] public List<ScopeClaim> Scopes { get; set; } = new();

    public bool IsActiveAt(DateTime utcNow) =>
        EffectiveFrom <= utcNow && (EffectiveTo == null || EffectiveTo >= utcNow);

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(IEnumerable<PositionClaim> positions) =>
        JsonSerializer.Serialize(positions, Options);

    /// <summary>Returns an empty list for null/missing/malformed claims — a token that
    /// cannot be parsed grants nothing (deny by default), it never throws into a 500.</summary>
    public static IReadOnlyList<PositionClaim> Parse(string? posClaimJson)
    {
        if (string.IsNullOrWhiteSpace(posClaimJson)) return Array.Empty<PositionClaim>();
        try
        {
            return JsonSerializer.Deserialize<List<PositionClaim>>(posClaimJson, Options)
                   ?? (IReadOnlyList<PositionClaim>)Array.Empty<PositionClaim>();
        }
        catch (JsonException)
        {
            return Array.Empty<PositionClaim>();
        }
    }
}

public sealed class ScopeClaim
{
    [JsonPropertyName("st")] public ScopeType ScopeType { get; set; }
    [JsonPropertyName("id")] public Guid? ScopeRefId { get; set; }
    [JsonPropertyName("v")] public string? ScopeValue { get; set; }
}
