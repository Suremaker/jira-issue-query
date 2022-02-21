using System.Text.Json;
using JiraIssueQuery.Api.Clients;
using JiraIssueQuery.Api.Mappers;
using JiraIssueQuery.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace JiraIssueQuery.Api.Controllers
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
        [ResponseCache(Duration = 60 * 5)]
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
        [ResponseCache(Duration = 60 * 5)]
        public async Task<IEnumerable<JiraIssue>> QuerySimplifiedIssues(string jql, string? select)
        {
            var mapper = new JiraIssueMapper(await _client.QueryFieldKeyToName(), await _client.QueryStatuses());
            var issues = await _client.QueryJql(jql, null, select);
            return issues.Select(mapper.Map);
        }
    }
}