using UnityEngine;

/// <summary>
/// Whimsical random chef-name generator. "Sleepy Crane", "Wandering Camel", etc.
/// 给玩家自动取名用，不填名字时调用。
/// </summary>
public static class ChefNameGenerator
{
    public const int MaxLength = 20;

    private static readonly string[] Adjectives =
    {
        "Sleepy", "Wandering", "Curious", "Drunken", "Hungry", "Quiet",
        "Lucky", "Brave", "Lazy", "Dancing", "Whispering", "Tipsy",
        "Spicy", "Sweet", "Bitter", "Salty", "Sunburnt", "Restless",
        "Velvet", "Iron", "Glass", "Wooden", "Cosmic", "Humble"
    };

    private static readonly string[] Nouns =
    {
        "Crane", "Camel", "Phoenix", "Mongoose", "Otter", "Tiger",
        "Crow", "Heron", "Rabbit", "Lynx", "Octopus", "Wolf",
        "Magpie", "Beetle", "Sparrow", "Salmon", "Dragonfly", "Mantis",
        "Pigeon", "Raccoon", "Hedgehog", "Owl", "Cricket", "Deer"
    };

    public static string Generate()
    {
        string adj = Adjectives[Random.Range(0, Adjectives.Length)];
        string noun = Nouns[Random.Range(0, Nouns.Length)];
        var name = $"{adj} {noun}";
        return name.Length > MaxLength ? name.Substring(0, MaxLength) : name;
    }

    public static string Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Generate();
        string trimmed = raw.Trim();
        return trimmed.Length > MaxLength ? trimmed.Substring(0, MaxLength) : trimmed;
    }
}
