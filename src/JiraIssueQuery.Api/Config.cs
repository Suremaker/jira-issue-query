namespace JiraMetrics.Api
{
    public class Config
    {
        public string JiraUri { get; set; }
        public int JiraQueryThroughput { get; set; } = 10;
    }
}