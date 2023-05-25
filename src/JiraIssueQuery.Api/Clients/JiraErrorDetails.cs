namespace JiraIssueQuery.Api.Clients;

internal record JiraErrorDetails(string[]? ErrorMessages, string[]? WarningMessages)
{
    public string[] ErrorMessages { get; } = ErrorMessages ?? Array.Empty<string>();
    public string[] WarningMessages { get; } = WarningMessages ?? Array.Empty<string>();
}