using Humanizer;

namespace JiraIssueQuery.Api.Models;

public record FieldDetails
{
    public string Key { get; init; } = string.Empty;
    public IReadOnlyList<string> ClauseNames { get; init; } = Array.Empty<string>();
    public string Name { get; init; } = string.Empty;

    public bool MatchKey(string name) => string.Equals(Key, name, StringComparison.OrdinalIgnoreCase);

    public bool Match(string name)
    {
        if (MatchKey(name))
            return true;
        if (ClauseNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            return true;
        return string.Equals(Name, name, StringComparison.OrdinalIgnoreCase);
    }

    public static FieldDetails From(string key, string name, string[] names) => new()
    {
        Key = key,
        ClauseNames = names,
        Name = SanitizeName(name)
    };

    private static string SanitizeName(string name) => name.Replace("[", "").Replace("]", "").Replace("-", " ").Pascalize();
}