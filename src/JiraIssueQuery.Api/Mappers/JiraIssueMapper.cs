using System.Text.Json;
using JiraIssueQuery.Api.Models;

namespace JiraIssueQuery.Api.Mappers;

public class JiraIssueMapper
{
    private readonly IReferenceDataMapper _refDataMapper;
    public JiraIssueMapper(IReferenceDataMapper refDataMapper)
    {
        _refDataMapper = refDataMapper;
    }

    public JiraIssue Map(JsonElement issue)
    {
        var r = new JiraIssue
        {
            { "Key", issue.GetProperty("key").ToString() },
            { ComputedFields.Url.Name, ExtractTicketUrl(issue) }
        };
        foreach (var (key, value) in MapFields(issue.GetProperty("fields")))
            r.Add(key, value);

        foreach (var (key, value) in MapAdditionalFields(issue.GetProperty("fields")))
        {
            if (value != null)
                r.Add(key, value);
        }

        return r;
    }

    private static string ExtractTicketUrl(JsonElement issue)
    {
        var selfUri = new Uri(issue.GetProperty("self").ToString());
        var key = issue.GetProperty("key").ToString();
        var url = new Uri(selfUri, $"/browse/{key}");
        return url.ToString();
    }

    private IEnumerable<(string key, object? value)> MapAdditionalFields(JsonElement fields)
    {
        yield return (ComputedFields.StatusCategory.Name,
            fields.TryGetProperty("status")?.GetProperty("statusCategory").GetProperty("name").GetString()
            ?? string.Empty);

        yield return (ComputedFields.Age.Name, GetIssueAge(fields));

        var times = GetIssueTimesInStatus(fields);
        if (times.Any())
        {
            yield return (ComputedFields.TimeInStatus.Name, times.ToDictionary(x => x.Status.Name, x => x.Time));
            yield return (ComputedFields.TimeInCategory.Name, times.GroupBy(x => x.Status.Category).ToDictionary(x => x.Key, x => x.Sum(p => p.Time)));
        }
        var resolved = fields.TryGetProperty("resolutiondate")?.TryParseDateTimeOffset();

        if (resolved != null)
        {
            yield return (ComputedFields.LeadTime.Name, GetIssueLeadTime(fields, resolved.Value));
            yield return (ComputedFields.CycleTime.Name, GetIssueCycleTime(times));
        }

        var sprints = GetSprints(fields).ToArray();
        var completedSprint = GetCompletedSprint(sprints, resolved);
        yield return (ComputedFields.CarriedOverSprint.Name, GetCarriedOverSprint(sprints, resolved));
        yield return (ComputedFields.CompletedSprint.Name, completedSprint?.Name);
        yield return (ComputedFields.CompletedSprintStartDate.Name, completedSprint?.Start);
        yield return (ComputedFields.CompletedSprintEndDate.Name, completedSprint?.End);
        yield return (ComputedFields.TimeSinceStatusCategoryChange.Name, GetIssueTimeSinceStatusCategoryChange(fields));
    }

    private object? GetIssueTimeSinceStatusCategoryChange(JsonElement fields)
    {
        var categoryChanged = fields.TryGetProperty("statuscategorychangedate")?.ParseDateTimeOffset();
        if (categoryChanged == null)
            return null;
        var now = DateTimeOffset.UtcNow;
        return (now - categoryChanged.Value).TotalHours;
    }
    private Sprint? GetCompletedSprint(Sprint[] sprints, DateTimeOffset? resolved)
    {
        if (resolved == null)
            return null;
        var sprint = sprints.FirstOrDefault(s => s.IsIn(resolved));
        return sprint;
    }
    private object? GetCarriedOverSprint(Sprint[] sprints, DateTimeOffset? resolved)
    {
        var date = resolved ?? DateTimeOffset.UtcNow;
        return sprints.Where(s => !s.IsIn(date)).Select(s => s.Name).ToArray();
    }
    private IEnumerable<Sprint> GetSprints(JsonElement fields)
    {
        var sprintFieldId = _refDataMapper.TryResolveFieldKey("sprint");
        if (sprintFieldId == null)
            yield break;

        var sprints = fields.TryGetProperty(sprintFieldId);
        if (sprints == null || sprints.Value.ValueKind == JsonValueKind.Null)
            yield break;

        foreach (var sprint in sprints.Value.EnumerateArray())
        {
            var name = sprint.GetProperty("name").GetString() ?? string.Empty;
            var start = sprint.TryGetProperty("startDate")?.TryParseDateTimeOffset();
            var end = sprint.TryGetProperty("completeDate")?.TryParseDateTimeOffset()
                      ?? sprint.TryGetProperty("endDate")?.TryParseDateTimeOffset();

            if (start.HasValue && end.HasValue)
                yield return new(name, start.Value, end.Value);
        }
    }

    private (Status Status, long Time)[] GetIssueTimesInStatus(JsonElement fields)
    {
        var timeInStatusFieldKey = _refDataMapper.TimeInStatusFieldKey;
        if (timeInStatusFieldKey == null)
            return Array.Empty<(Status, long)>();

        var value = fields.TryGetProperty(timeInStatusFieldKey)?.GetString() ?? string.Empty;
        var times = value.Split('|').Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(DecodeTimeInStatus).ToArray();
        return times;
    }

    private double GetIssueCycleTime((Status Status, long Time)[] times)
    {
        var cycleTimeStatuses = _refDataMapper.GetCycleTimeStatuses();
        var sum = cycleTimeStatuses.Select(s => times.Where(t => t.Status.Name.Equals(s, StringComparison.OrdinalIgnoreCase)).Select(t => t.Time).FirstOrDefault())
            .DefaultIfEmpty(0).Sum();
        return TimeSpan.FromMilliseconds(sum).TotalHours;
    }

    private double? GetIssueLeadTime(JsonElement fields, DateTimeOffset resolved)
    {
        var created = fields.TryGetProperty("created")?.ParseDateTimeOffset();
        if (created == null)
            return null;
        return (resolved - created.Value).TotalHours;
    }

    private double? GetIssueAge(JsonElement fields)
    {
        var created = fields.TryGetProperty("created")?.ParseDateTimeOffset();
        if (created == null)
            return null;
        var now = DateTimeOffset.UtcNow;
        return (now - created.Value).TotalHours;
    }

    private (Status Status, long Time) DecodeTimeInStatus(string v)
    {
        var parts = v.Split(':');
        return (_refDataMapper.GetStatusById(parts[0].Trim('_', '*')), long.Parse(parts[2].Trim('_', '*')));
    }

    private IEnumerable<(string key, object value)> MapFields(JsonElement fields)
    {
        foreach (var property in fields.EnumerateObject())
        {
            var name = ResolveName(property);
            object? value = ResolveValue(property.Value);
            if (value != null)
                yield return (name, value);
        }
    }

    private object? MapArray(JsonElement property)
    {
        var result = new object?[property.GetArrayLength()];
        int i = 0;
        foreach (var element in property.EnumerateArray())
            result[i++] = ResolveValue(element);
        return result;
    }

    private object? ResolveValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Undefined => null,
            JsonValueKind.Object => MapObject(element),
            JsonValueKind.Array => MapArray(element),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => null
        };
    }

    private static object? MapObject(JsonElement element)
    {
        if (element.TryGetProperty("type")?.TryGetString() == "doc")
            return null;

        var names = new[] { "displayName", "name", "value" };
        foreach (var name in names)
        {
            var prop = element.TryGetProperty(name);
            if (prop != null)
                return prop.Value.GetString();
        }

        return element;
    }

    private string ResolveName(JsonProperty property) => _refDataMapper.TryResolveFieldName(property.Name);
}