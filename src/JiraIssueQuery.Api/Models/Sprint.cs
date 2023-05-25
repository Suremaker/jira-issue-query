namespace JiraIssueQuery.Api.Models;

public record Sprint(string Name, DateTimeOffset Start, DateTimeOffset End)
{
	public  bool IsIn(DateTimeOffset? resolved) => Start <= resolved && End >= resolved;
}