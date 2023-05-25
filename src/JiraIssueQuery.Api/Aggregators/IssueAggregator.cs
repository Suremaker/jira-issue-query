using System.Globalization;
using JiraIssueQuery.Api.Mappers;
using JiraIssueQuery.Api.Models;

namespace JiraIssueQuery.Api.Aggregators;

public class IssueAggregator
{
    private readonly IReferenceDataMapper _referenceDataMapper;

    public IssueAggregator(IReferenceDataMapper referenceDataMapper)
    {
        _referenceDataMapper = referenceDataMapper;
    }

    public IEnumerable<JiraIssueAggregate> Aggregate(IEnumerable<JiraIssue> issues, FieldGrouping group, FieldGrouping subGroup, FieldAggregation value)
    {
        group = group with { Field = _referenceDataMapper.TryResolveFieldName(group.Field) };
        subGroup = subGroup with { Field = _referenceDataMapper.TryResolveFieldName(subGroup.Field) };
        value = value with { Field = _referenceDataMapper.TryResolveFieldName(value.Field) };

        var groupValueConverter = GetConverter(group);
        var subGroupValueConverter = GetConverter(subGroup);

        var aggregates = issues.SelectMany(i => ExpandArray(i, group.Field))
            .GroupBy(p => groupValueConverter.Invoke(p.groupValue), p => p.issue)
            .Select(g => AggregateSubGroup(g.Key, g, subGroup, subGroupValueConverter, value))
            .ToArray();
        FillEmptyValues(aggregates);
        return aggregates.OrderBy(x => x.GroupValue).Select(aggregate => ToDictionary(aggregate, group.Field));
    }

    public IEnumerable<JiraIssueAggregate> Compare(IEnumerable<JiraIssueAggregate> baselineAggregate,
        IEnumerable<JiraIssueAggregate> compareAggregate, string baselineGroupField, string compareGroupField)
    {
        baselineGroupField = _referenceDataMapper.TryResolveFieldName(baselineGroupField);
        compareGroupField = _referenceDataMapper.TryResolveFieldName(compareGroupField);
        var baseline = baselineAggregate.ToDictionary(x => x[baselineGroupField]);
        var compare = compareAggregate.ToDictionary(x => x[compareGroupField]);

        return baseline.OrderBy(b => b.Key)
            .Select(b => Compare(baselineGroupField, b.Value, compare.TryGetValue(b.Key, out var o) ? o : null));
    }

    private Func<string, string> GetConverter(FieldGrouping f)
    {
        return f.Type switch
        {
            AggregateFieldType.Text => value => value,
            AggregateFieldType.Date => value => GetDate(value, f.Format ?? "o"),
            _ => throw new InvalidOperationException($"Unsupported type: {f.Type}")
        };
    }

    private void FillEmptyValues(IssueAggregate[] aggregates)
    {
        var subGroup = aggregates
            .SelectMany(a => a.SubGroups.Select(g => g.Key))
            .Distinct()
            .ToArray();

        foreach (var a in aggregates)
            foreach (var s in subGroup)
            {
                if (!a.SubGroups.ContainsKey(s))
                    a.SubGroups[s] = 0.0;
            }
    }

    private JiraIssueAggregate ToDictionary(IssueAggregate a, string groupField)
    {
        var result = new JiraIssueAggregate
        {
            [groupField] = a.GroupValue
        };

        foreach (var (subGroupValue, aggregatedValue) in a.SubGroups)
        {
            var key = string.IsNullOrWhiteSpace(subGroupValue) ? Constants.Unset : subGroupValue;
            result[key] = aggregatedValue;
        }

        return result;
    }

    private IssueAggregate AggregateSubGroup(string groupValue, IGrouping<string, JiraIssue> group, FieldGrouping subGroup, Func<string, string> subGroupValueConverter, FieldAggregation value)
    {
        var result = new IssueAggregate { GroupValue = groupValue };

        var allIssues = group.ToArray();
        var subGroups = allIssues
            .SelectMany(i => ExpandArray(i, subGroup.Field))
            .GroupBy(x => subGroupValueConverter.Invoke(x.groupValue), x => x.issue)
            .ToArray();

        foreach (var g in subGroups)
            result.SubGroups[g.Key] = AggregateValueField(allIssues, g, value.Field, value.Operation);
        result.SubGroups[Constants.All] = AggregateValueField(allIssues, subGroups.SelectMany(g => g), value.Field, value.Operation);

        return result;
    }

    private double AggregateValueField(IReadOnlyList<JiraIssue> allIssues, IEnumerable<JiraIssue> subGroup, string valueField, AggregateOperation operation)
    {
        var query = subGroup.Select(i => GetFieldValue(i, valueField));
        return operation switch
        {
            AggregateOperation.Count => query.Count(),
            AggregateOperation.Sum => query.Where(x => x != null).Cast<double>().Sum(),
            AggregateOperation.Min => query.Where(x => x != null).Cast<double>().DefaultIfEmpty().Min(),
            AggregateOperation.Max => query.Where(x => x != null).Cast<double>().DefaultIfEmpty().Max(),
            AggregateOperation.Avg => query.Where(x => x != null).Cast<double>().DefaultIfEmpty().Average(),
            AggregateOperation.CountRatio => allIssues.Any() ? query.Count() / (double)allIssues.Count : 0.0,
            AggregateOperation.SumRatio => GetSumRatio(query, allIssues, valueField),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };
    }

    private double GetSumRatio(IEnumerable<object?> query, IReadOnlyList<JiraIssue> allIssues, string valueField)
    {
        var totalSum = allIssues.Select(i => GetFieldValue(i, valueField)).Where(x => x != null).Cast<double>().Sum();
        if (totalSum == 0)
            return 0.0;
        var groupSum = query.Where(x => x != null).Cast<double>().Sum();
        return groupSum / totalSum;
    }

    private IEnumerable<(string groupValue, JiraIssue issue)> ExpandArray(JiraIssue issue, string groupField)
    {
        var value = GetFieldValue(issue, groupField);
        if (value is object?[] array)
        {
            foreach (var arrayValue in array)
                yield return (arrayValue?.ToString() ?? string.Empty, issue);
        }
        else
            yield return (value?.ToString() ?? string.Empty, issue);
    }

    private object? GetFieldValue(JiraIssue issue, string fieldName) => issue.TryGetValue(fieldName, out var value) ? value : null;

    private string GetDate(string? value, string dateFormat) => string.IsNullOrWhiteSpace(value) ? string.Empty : DateTimeOffset.Parse(value).ToString(dateFormat, CultureInfo.InvariantCulture);

    private JiraIssueAggregate Compare(string groupField, JiraIssueAggregate baseline, JiraIssueAggregate? other)
    {
        var result = new JiraIssueAggregate
        {
            { groupField, baseline[groupField] }
        };
        foreach (var pair in baseline.Where(p => p.Key != groupField))
        {
            var otherValue = (other?.TryGetValue(pair.Key, out var v) ?? false) ? (double)v : 0.0;
            var value = (double)pair.Value;
            var resultValue = value > 0 ? otherValue / value : 0.0;
            result[pair.Key] = resultValue;
        }

        return result;
    }
}