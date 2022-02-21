namespace JiraIssueQuery.Api.Models;

public class JiraIssue
{
    public string Key { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object> Fields { get; init; } = new Dictionary<string, object>();
    public IReadOnlyDictionary<string, object> AdditionalFields { get; set; } = new Dictionary<string, object>();
}