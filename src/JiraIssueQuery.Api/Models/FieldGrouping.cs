namespace JiraIssueQuery.Api.Models;

public record FieldGrouping(string Field, AggregateFieldType Type = AggregateFieldType.Text, string? Format = null);