namespace JiraIssueQuery.Api
{
    public class Config
    {
        public string JiraUri { get; set; } = string.Empty;
        public int JiraQueryThroughput { get; set; } = 10;
    }
}