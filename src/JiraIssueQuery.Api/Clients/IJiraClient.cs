using System.Text.Json;
using JiraIssueQuery.Api.Models;

namespace JiraIssueQuery.Api.Clients
{
    public interface IJiraClient
    {
        Task<IReadOnlyList<JsonElement>> QueryJql(string jqlQuery, string? expand, string? select);
        Task<IReadOnlyDictionary<string, string>> QueryFieldKeyToName();
        Task<IEnumerable<Status>> QueryStatuses();
    }
}
