using System.Text.Json;
using JiraMetrics.Api.Models;
using Microsoft.Extensions.Options;

namespace JiraMetrics.Api.Clients
{
    public class JiraClient : IJiraClient
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly SemaphoreSlim _requestThrottler;

        public JiraClient(IOptions<Config> config, IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
            _requestThrottler = new SemaphoreSlim(config.Value.JiraQueryThroughput);
        }

        public async Task<IReadOnlyList<JsonElement>> QueryJql(string jqlQuery, string? expand, string? select)
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

                var doc = await GetDocument(client, query);
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

        public async Task<IReadOnlyDictionary<string, string>> QueryFieldKeyToName()
        {
            var doc = await GetDocument(GetClient(), "/rest/api/2/field");

            return doc.RootElement.EnumerateArray()
                .ToDictionary(
                    x => x.GetProperty("key").GetString()!,
                    x => x.GetProperty("name").GetString()!);
        }

        public async Task<IEnumerable<Status>> QueryStatuses()
        {
            var doc = await GetDocument(GetClient(), "/rest/api/3/status");

            return doc.RootElement.EnumerateArray()
                .Select(x => new Status
                {
                    Id = x.GetProperty("id").GetString()!,
                    Name = x.GetProperty("name").GetString()!,
                    Category = x.GetProperty("statusCategory").GetProperty("key").GetString()!
                });
        }

        private HttpClient GetClient() => _clientFactory.CreateClient(nameof(IJiraClient));

        private async Task<JsonDocument> GetDocument(HttpClient client, string query)
        {
            try
            {
                await _requestThrottler.WaitAsync();
                using var resp = await client.GetAsync(query);
                resp.EnsureSuccessStatusCode();
                await using var stream = await resp.Content.ReadAsStreamAsync();
                return await JsonDocument.ParseAsync(stream);
            }
            finally
            {
                _requestThrottler.Release();
            }
        }
    }
}