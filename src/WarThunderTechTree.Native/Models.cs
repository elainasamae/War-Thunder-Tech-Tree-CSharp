using System.Text.Json;
using System.Text.Json.Serialization;

namespace WarThunderTechTree.Native;

internal sealed class CountriesIndex
{
    [JsonPropertyName("generated_at")] public string GeneratedAt { get; set; } = "";
    [JsonPropertyName("game_data_version")] public string? GameDataVersion { get; set; }
    [JsonPropertyName("countries")] public List<CountryEntry> Countries { get; set; } = [];
}

internal sealed class CountryEntry
{
    [JsonPropertyName("code")] public string Code { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("name_en")] public string NameEnglish { get; set; } = "";
    [JsonPropertyName("flag")] public string? Flag { get; set; }
    public override string ToString() => $"{Name}  {NameEnglish}";
}

internal sealed class VehicleNamesIndex
{
    [JsonPropertyName("vehicles")] public Dictionary<string, string> Vehicles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    [JsonPropertyName("groups")] public Dictionary<string, string> Groups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class TreeData
{
    [JsonPropertyName("country")] public string Country { get; set; } = "";
    [JsonPropertyName("country_code")] public string CountryCode { get; set; } = "";
    [JsonPropertyName("country_name_en")] public string CountryNameEnglish { get; set; } = "";
    [JsonPropertyName("vehicle_type")] public string VehicleType { get; set; } = "ground";
    [JsonPropertyName("vehicle_type_name")] public string VehicleTypeName { get; set; } = "陆军";
    [JsonPropertyName("generated_at")] public string GeneratedAt { get; set; } = "";
    [JsonPropertyName("vehicles")] public List<VehicleRecord> Vehicles { get; set; } = [];
}

internal sealed class VehicleRecord
{
    [JsonPropertyName("rank")] public string Rank { get; set; } = "I";
    [JsonPropertyName("rank_number")] public int RankNumber { get; set; }
    [JsonPropertyName("tree_section")] public string TreeSection { get; set; } = "researchable";
    [JsonPropertyName("tree_row")] public int? TreeRow { get; set; }
    [JsonPropertyName("tree_column")] public int? TreeColumn { get; set; }
    [JsonPropertyName("group_id")] public string? GroupId { get; set; }
    [JsonPropertyName("group_name")] public string? GroupName { get; set; }
    [JsonPropertyName("group_position")] public int? GroupPosition { get; set; }
    [JsonPropertyName("premium_kind")] public string? PremiumKind { get; set; }
    [JsonPropertyName("unit_id")] public string UnitId { get; set; } = "";
    [JsonPropertyName("vehicle")] public string NameEnglish { get; set; } = "";
    [JsonPropertyName("requirement_id")] public string? RequirementId { get; set; }
    [JsonPropertyName("requirement_name")] public string? RequirementName { get; set; }
    [JsonPropertyName("image_url")] public string? ImageUrl { get; set; }
    [JsonPropertyName("url")] public string? WikiUrl { get; set; }
    [JsonPropertyName("tree_order")] public int TreeOrder { get; set; }
    [JsonPropertyName("research_display")] public string? ResearchDisplay { get; set; }
    [JsonPropertyName("research_rp")] public long? ResearchRp { get; set; }
    [JsonPropertyName("purchase_display")] public string? PurchaseDisplay { get; set; }
    [JsonPropertyName("purchase_sl")] public long? PurchaseSl { get; set; }
    [JsonPropertyName("battle_rating_ab")] public string? BattleRatingAb { get; set; }
    [JsonPropertyName("battle_rating_rb")] public string? BattleRatingRb { get; set; }
    [JsonPropertyName("battle_rating_sb")] public string? BattleRatingSb { get; set; }

    [JsonIgnore] public string NameChinese { get; set; } = "";

    public string Rating(string mode) => mode switch
    {
        "ab" => BattleRatingAb ?? "—",
        "sb" => BattleRatingSb ?? "—",
        _ => BattleRatingRb ?? "—"
    };

    public string DisplayName(bool chinese) => chinese && !string.IsNullOrWhiteSpace(NameChinese) ? NameChinese : NameEnglish;
}

internal sealed record VehicleDetails(
    JsonElement? Auxiliary,
    JsonElement? Specifications,
    JsonElement? Ammunition,
    JsonElement? Loadouts,
    JsonElement? Modifications);

internal sealed record ResearchRoute(IReadOnlyList<VehicleRecord> Vehicles, long TotalRp, long TotalSl, int ExtraCount);
