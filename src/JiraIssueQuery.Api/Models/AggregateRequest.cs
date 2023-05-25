namespace JiraIssueQuery.Api.Models;

public record AggregateRequest(string Jql, FieldGrouping Group, FieldGrouping SubGroup, FieldAggregation Value)
{
	public IEnumerable<string> GetSelectFields()
	{
		yield return Group.Field;
		yield return SubGroup.Field;
		yield return Value.Field;
	}
}