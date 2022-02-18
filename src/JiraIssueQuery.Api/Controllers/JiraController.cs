using System.Text.Json;
using System.Text.Json.Nodes;
using JiraMetrics.Api.Clients;
using JiraMetrics.Api.Mappers;
using JiraMetrics.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace JiraMetrics.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class JiraController : ControllerBase
    {
        private readonly IJiraClient _client;

        public JiraController(IJiraClient client)
        {
            _client = client;
        }

        [HttpGet("field-names")]
        public async Task<IReadOnlyDictionary<string, string>> GetFieldNames()
        {
            return await _client.QueryFieldKeyToName();
        }

        [HttpGet("issues")]
        public async Task<IEnumerable<JsonElement>> QueryIssues(string jql, string? expand, string? select)
        {
            return await _client.QueryJql(jql, expand, select);
        }

        [HttpGet("issues-simplified")]
        public async Task<IEnumerable<JiraIssue>> QuerySimplifiedIssues(string jql, string? select)
        {
            var mapper = new JiraIssueMapper(await _client.QueryFieldKeyToName(), await _client.QueryStatuses());
            var issues = await _client.QueryJql(jql, null, select);
            return issues.Select(mapper.Map);
        }
    }
}