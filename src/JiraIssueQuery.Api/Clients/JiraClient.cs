using System.Text.Json;
using JiraIssueQuery.Api.Models;
using Microsoft.Extensions.Options;

namespace JiraIssueQuery.Api.Clients
{
    public class JiraClient : IJiraClient
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly ILogger<JiraClient> _logger;
        private readonly SemaphoreSlim _requestThrottler;

        public JiraClient(IOptions<Config> config, IHttpClientFactory clientFactory, ILogger<JiraClient> logger)
        {
            _clientFactory = clientFactory;
            _logger = logger;
            _requestThrottler = new SemaphoreSlim(config.Value.JiraQueryThroughput);
        }

        public async Task<IReadOnlyList<JsonElement>> QueryJql(string jqlQuery, string? expand, string? select, CancellationToken cancellationToken)
        {
            jqlQuery = Uri.EscapeDataString(jqlQuery);
            var elements = new List<JsonElement>();
            var start = 0;
            var client = GetClient();

            while (true)
            {
                var query = $"/rest/api/3/search?jql={jqlQuery}&startAt={start}&maxResults=50";
                if (!string.IsNullOrEmpty(expand))
                    query += $"&expand={expand}";
                if (!string.IsNullOrEmpty(select))
                    query += $"&fields={select}";

                var doc = await GetDocument(client, query, cancellationToken);
                var issues = doc.RootElement.GetProperty("issues");

                elements.AddRange(issues.EnumerateArray());
                start = doc.RootElement.GetProperty("startAt").GetInt32();
                var total = doc.RootElement.GetProperty("total").GetInt32();
                var maxResults = doc.RootElement.GetProperty("maxResults").GetInt32();
                start += maxResults;
                if (start >= total)
                    break;
            }
            return elements;
        }

        public async Task<IReadOnlyList<FieldDetails>> QueryFields(CancellationToken cancellationToken)
        {
            var doc = await GetDocument(GetClient(), "/rest/api/3/field", cancellationToken);

            return doc.RootElement.EnumerateArray().Select(x => FieldDetails.From(
                    x.GetProperty("key").GetString()!,
                    x.GetProperty("name").GetString()!,
                    x.GetProperty("clauseNames").EnumerateArray().Select(n => n.GetString()!).ToArray()
                ))
                .ToArray();
        }

        public async Task<IReadOnlyList<Status>> QueryStatuses(CancellationToken cancellationToken)
        {
            var doc = await GetDocument(GetClient(), "/rest/api/3/status", cancellationToken);

            return doc.RootElement.EnumerateArray()
                .Select(x => new Status
                {
                    Id = x.GetProperty("id").GetString()!,
                    Name = x.GetProperty("name").GetString()!,
                    Category = x.GetProperty("statusCategory").GetProperty("key").GetString()!
                })
                .ToArray();
        }

        private HttpClient GetClient() => _clientFactory.CreateClient(nameof(IJiraClient));

        private async Task<JsonDocument> GetDocument(HttpClient client, string query, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Querying: {query}", query);
                await _requestThrottler.WaitAsync(cancellationToken);
                using var resp = await client.GetAsync(query, cancellationToken);
                if (!resp.IsSuccessStatusCode)
                {
                    var errorResponse = await resp.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError(errorResponse);
                }
                resp.EnsureSuccessStatusCode();
                await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
                return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            }
            finally
            {
                _requestThrottler.Release();
            }
        }
    }
}