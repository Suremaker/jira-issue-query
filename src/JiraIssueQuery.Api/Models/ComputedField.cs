namespace JiraIssueQuery.Api.Models;

public record ComputedField
{
    public ComputedField(string name, params string[] dependentFields)
    {
        Name = $"X-{name}";
        DependentFields = dependentFields;
    }

    public IReadOnlyList<string> DependentFields { get; }
    public string Name { get; }
}