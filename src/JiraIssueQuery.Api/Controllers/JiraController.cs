using System.Text.Json;
using JiraIssueQuery.Api.Aggregators;
using JiraIssueQuery.Api.Clients;
using JiraIssueQuery.Api.Filters;
using JiraIssueQuery.Api.Mappers;
using JiraIssueQuery.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace JiraIssueQuery.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [TypeFilter(typeof(ExceptionFilter))]
    public class JiraController : ControllerBase
    {
        private readonly IJiraClient _client;
        private readonly JiraIssueMapper _mapper;
        private readonly IReferenceDataMapper _referenceDataMapper;
        private readonly IssueAggregator _aggregator;

        public JiraController(IJiraClient client, JiraIssueMapper mapper, IReferenceDataMapper referenceDataMapper, IssueAggregator aggregator)
        {
            _client = client;
            _mapper = mapper;
            _referenceDataMapper = referenceDataMapper;
            _aggregator = aggregator;
        }

        [HttpGet("field-names")]
        [ResponseCache(Duration = 60 * 5)]
        public async Task<IReadOnlyList<FieldDetails>> GetFieldNames(CancellationToken cancellationToken)
        {
            return await _client.QueryFields(cancellationToken);
        }

        [HttpGet("issues")]
        public async Task<IEnumerable<JsonElement>> QueryIssues(string jql, string? expand, string? select, CancellationToken cancellationToken)
        {
            await _referenceDataMapper.Refresh(cancellationToken);
            var selectFields = ConstructSelectFields(select?.Split(','));
            return await _client.QueryJql(jql, expand, selectFields, cancellationToken);
        }

        [HttpGet("issues-simplified")]
        [ResponseCache(Duration = 60 * 5)]
        public async Task<IEnumerable<JiraIssue>> QuerySimplifiedIssues(string jql, string? select, CancellationToken cancellationToken)
        {
            return await QuerySimplified(jql, cancellationToken, select?.Split(',') ?? Array.Empty<string>());
        }

        [HttpGet("aggregate")]
        [ResponseCache(Duration = 60 * 5)]
        public async Task<IEnumerable<JiraIssueAggregate>> Aggregate([FromQuery] AggregateRequest request, CancellationToken cancellationToken)
        {
            var issues = await QuerySimplified(request.Jql, cancellationToken, request.GetSelectFields().ToArray());
            return _aggregator.Aggregate(issues, request.Group, request.SubGroup, request.Value);
        }

        [HttpGet("compare-aggregates")]
        [ResponseCache(Duration = 60 * 5)]
        public async Task<IEnumerable<JiraIssueAggregate>> ComposeAggregations([FromQuery] AggregateComparisonRequest request, CancellationToken cancellationToken)
        {
            var baselineRequest = request.GetBaseline();
            var compareRequest = request.GetCompare();
            var baseAggregate = await Aggregate(baselineRequest, cancellationToken);
            var otherAggregate = await Aggregate(compareRequest, cancellationToken);
            return _aggregator.Compare(baseAggregate, otherAggregate, baselineRequest.Group.Field, compareRequest.Group.Field);
        }

        private async Task<IEnumerable<JiraIssue>> QuerySimplified(string jql, CancellationToken cancellationToken, params string[] selectFields)
        {
            await _referenceDataMapper.Refresh(cancellationToken);
            var issues = await _client.QueryJql(jql, null, ConstructSelectFields(selectFields), cancellationToken);
            return issues.Select(_mapper.Map);
        }

        private string? ConstructSelectFields(string[]? selectFields)
        {
            if (selectFields == null || selectFields.Length == 0)
                return null;
            var result = string.Join(',', _referenceDataMapper.GetFieldIdsToInclude(selectFields));

            return string.IsNullOrEmpty(result)
                ? null
                : result;
        }
    }
}