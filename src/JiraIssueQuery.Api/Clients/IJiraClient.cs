using System.Text.Json;
using JiraIssueQuery.Api.Models;

namespace JiraIssueQuery.Api.Clients
{
    public interface IJiraClient
    {
        Task<IReadOnlyList<JsonElement>> QueryJql(string jqlQuery, string? expand, string? select, CancellationToken cancellationToken);
        Task<IReadOnlyList<FieldDetails>> QueryFields(CancellationToken cancellationToken);
        Task<IReadOnlyList<Status>> QueryStatuses(CancellationToken cancellationToken);
    }
}
