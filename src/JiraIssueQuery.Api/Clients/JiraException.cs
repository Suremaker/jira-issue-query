namespace JiraIssueQuery.Api.Clients;

internal class JiraException : Exception
{
    public JiraException(JiraErrorDetails? message) : base(Format(message))
    {
    }

    private static string Format(JiraErrorDetails? message) => message == null ? string.Empty : string.Join(" ", message.ErrorMessages.Concat(message.WarningMessages));
}