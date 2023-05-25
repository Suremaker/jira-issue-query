namespace JiraIssueQuery.Api.Aggregators;

internal class IssueAggregate
{
	public string GroupValue { get; init; } = string.Empty;

	public Dictionary<string, object> SubGroups { get; } = new();
}