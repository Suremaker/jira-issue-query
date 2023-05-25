using JiraIssueQuery.Api.Models;

namespace JiraIssueQuery.Api.Mappers;

public interface IReferenceDataMapper
{
    Task Refresh(CancellationToken cancellationToken);
    string? TimeInStatusFieldKey { get; }
    Status GetStatusById(string id);
    string TryResolveFieldName(string fieldKey);
    IReadOnlyList<string> GetCycleTimeStatuses();
    IEnumerable<string> GetFieldIdsToInclude(string[] selectFields);
    string? TryResolveFieldKey(string name);
}