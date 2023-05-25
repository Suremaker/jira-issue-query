namespace JiraIssueQuery.Api.Models;

public record AggregateComparisonRequest(string BaselineJql, string CompareJql, FieldGrouping Group, FieldGrouping? CompareGroup,
	FieldGrouping BaselineSubGroup, FieldGrouping? CompareSubGroup, FieldAggregation Value)
{
	public AggregateRequest GetBaseline() => new(BaselineJql, Group, BaselineSubGroup, Value);
	public AggregateRequest GetCompare() => new(CompareJql, CompareGroup ?? Group, CompareSubGroup ?? BaselineSubGroup, Value);
}