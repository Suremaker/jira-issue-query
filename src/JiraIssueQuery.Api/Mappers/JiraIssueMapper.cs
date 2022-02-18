using System.Text.Json;
using Humanizer;
using JiraMetrics.Api.Controllers;
using JiraMetrics.Api.Models;

namespace JiraMetrics.Api.Mappers;

public class JiraIssueMapper
{
    private readonly IReadOnlyDictionary<string, string> _fieldToNameMapping;
    private readonly Dictionary<string, Status> _statusIdMap;
    private readonly string? _timeInStatusFieldKey;
    public JiraIssueMapper(IReadOnlyDictionary<string, string> fieldToNameMapping, IEnumerable<Status> statusIdToNameMap)
    {
        _fieldToNameMapping = fieldToNameMapping;
        _statusIdMap = statusIdToNameMap.ToDictionary(x => x.Id);
        _timeInStatusFieldKey = _fieldToNameMapping
            .Where(v => v.Value == "[CHART] Time in Status")
            .Select(x => x.Key)
            .FirstOrDefault();
    }

    public JiraIssue Map(JsonElement issue)
    {
        return new JiraIssue
        {
            Key = issue.GetProperty("key").ToString(),
            Fields = MapFields(issue.GetProperty("fields")),
            AdditionalFields = MapAdditionalFields(issue.GetProperty("fields"))
        };
    }

    private IReadOnlyDictionary<string, object> MapAdditionalFields(JsonElement fields)
    {
        var result = new Dictionary<string, object>
        {
            {"StatusCategory",fields.TryGetProperty("status")?.GetProperty("statusCategory").GetProperty("name").GetString()??string.Empty},
        };
        if (_timeInStatusFieldKey != null)
        {
            var value = fields.TryGetProperty(_timeInStatusFieldKey)?.GetString() ?? string.Empty;
            var times = value.Split('|').Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(DecodeTimeInStatus).ToArray();
            result["TimeInStatus"] = times.ToDictionary(x => x.Status.Name, x => x.Time);
            result["TimeInCategory"] = times.GroupBy(x => x.Status.Category).ToDictionary(x => x.Key, x => x.Sum(p => p.Time));
        }
        return result;
    }

    private (Status Status, long Time) DecodeTimeInStatus(string v)
    {
        var parts = v.Split(':');
        return (_statusIdMap[parts[0].Trim('_', '*')], long.Parse(parts[2].Trim('_', '*')));
    }

    private IReadOnlyDictionary<string, object> MapFields(JsonElement fields)
    {
        var result = new Dictionary<string, object>();
        foreach (var property in fields.EnumerateObject())
        {
            var name = ResolveName(property);
            object? value = property.Value.ValueKind switch
            {
                JsonValueKind.Undefined => null,
                JsonValueKind.Object => MapObject(property),
                JsonValueKind.Array => property.Value,
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => null
            };
            if (value != null)
                result[name] = value;
        }

        return result;
    }

    private static object? MapObject(JsonProperty property)
    {
        if (property.Value.TryGetProperty("type")?.GetString() == "doc")
            return null;

        var names = new[] { "displayName", "name", "value" };
        foreach (var name in names)
        {
            var prop = property.Value.TryGetProperty(name);
            if (prop != null)
                return prop.Value.GetString();
        }

        return property.Name switch
        {
            "votes" => property.Value.GetProperty("votes").GetInt32(),
            _ => property.Value
        };
    }

    private string ResolveName(JsonProperty property)
    {
        return SanitizeName(_fieldToNameMapping.TryGetValue(property.Name, out var n) ? n : property.Name);
    }

    private string SanitizeName(string name)
    {
	    return name.Replace("[", "").Replace("]", "").Replace("-", " ").Pascalize();
    }
}