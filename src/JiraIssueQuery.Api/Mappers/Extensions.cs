using System.Text.Json;

namespace JiraIssueQuery.Api.Mappers;

static class Extensions
{
    public static JsonElement? TryGetProperty(this JsonElement e, string name) 
        => e.TryGetProperty(name, out var p) ? p : null;
}