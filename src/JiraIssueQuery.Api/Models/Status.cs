namespace JiraMetrics.Api.Models;

public record Status
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string Category { get; init; }
}