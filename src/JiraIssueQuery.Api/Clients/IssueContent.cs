namespace JiraMetrics.Api.Clients
{
    public class IssueContent
    {
        public IDictionary<string, string> Fields { get; } = new Dictionary<string, string>();
    }
}