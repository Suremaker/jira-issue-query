using System.Text.Json;

namespace JiraIssueQuery.Api.Mappers;

static class Extensions
{
    public static JsonElement? TryGetProperty(this JsonElement e, string name)
        => e.TryGetProperty(name, out var p) ? p : null;

    public static string? TryGetString(this JsonElement e)
        => e.ValueKind == JsonValueKind.String ? e.GetString() : null;

    public static DateTimeOffset? TryParseDateTimeOffset(this JsonElement e)
        => e.ValueKind == JsonValueKind.String ? DateTimeOffset.Parse(e.GetString()!) : null;

    public static DateTimeOffset ParseDateTimeOffset(this JsonElement e)
        => TryParseDateTimeOffset(e) ?? throw new FormatException("Provided element has no value");
}