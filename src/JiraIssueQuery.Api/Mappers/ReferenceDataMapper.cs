using JiraIssueQuery.Api.Clients;
using JiraIssueQuery.Api.Models;
using Microsoft.Extensions.Options;

namespace JiraIssueQuery.Api.Mappers;

public class ReferenceDataMapper : IReferenceDataMapper
{
    private readonly IJiraClient _client;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _semaphore = new(1);
    private DateTimeOffset _lastUpdate = DateTimeOffset.MinValue;

    private IReadOnlyList<FieldDetails> _fields = Array.Empty<FieldDetails>();
    private IReadOnlyList<Status> _statuses = Array.Empty<Status>();
    private readonly MappingConfig _config;
    public string? TimeInStatusFieldKey { get; private set; }


    public ReferenceDataMapper(IJiraClient client, IOptions<MappingConfig> config)
    {
        _client = client;
        _config = config.Value;
    }

    public async Task Refresh(CancellationToken cancellationToken)
    {
        if (!RequireRefresh())
            return;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (!RequireRefresh())
                return;
            _fields = await _client.QueryFields(cancellationToken);
            _statuses = await _client.QueryStatuses(cancellationToken);
            TimeInStatusFieldKey = FindTimeInStatusFieldKey();
            _lastUpdate = DateTimeOffset.UtcNow;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Status GetStatusById(string id) => _statuses.FirstOrDefault(s => s.Id == id) ?? new() { Id = id, Category = "Unknown", Name = $"Unknown ({id})" };

    public IReadOnlyList<string> GetCycleTimeStatuses() => _config.CycleTimeStatuses;

    public string TryResolveFieldName(string fieldKey) => TryResolveField(fieldKey)?.Name ?? TryResolveComputedField(fieldKey)?.Name ?? fieldKey;

    public IEnumerable<string> GetFieldIdsToInclude(string[] selectFields) => selectFields.SelectMany(GetFieldIdsToInclude).Distinct();

    public string? TryResolveFieldKey(string field) => TryResolveField(field)?.Key;

    private IEnumerable<string> GetFieldIdsToInclude(string field)
    {
        var key = TryResolveFieldKey(field);
        if (key != null)
            return new[] { key };

        return ComputedFields.Fields.Where(f => f.Name.Equals(field, StringComparison.OrdinalIgnoreCase))
            .SelectMany(f => f.DependentFields)
            .Select(TryResolveFieldKey)
            .Where(x => x != null)!;
    }

    private string? FindTimeInStatusFieldKey() => TryResolveField("[CHART] Time in Status")?.Key;

    private bool RequireRefresh() => _lastUpdate + _interval < DateTimeOffset.UtcNow;

    private FieldDetails? TryResolveField(string field) => _fields.FirstOrDefault(f => f.Match(field));

    private ComputedField? TryResolveComputedField(string field)
        => ComputedFields.Fields.FirstOrDefault(x => x.Name.Equals(field, StringComparison.OrdinalIgnoreCase));
}