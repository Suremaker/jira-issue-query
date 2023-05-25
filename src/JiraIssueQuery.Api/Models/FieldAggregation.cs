namespace JiraIssueQuery.Api.Models;

public record FieldAggregation(string Field = "Key", AggregateOperation Operation = AggregateOperation.Count);