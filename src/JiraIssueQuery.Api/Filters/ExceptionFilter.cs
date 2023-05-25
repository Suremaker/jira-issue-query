using JiraIssueQuery.Api.Clients;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace JiraIssueQuery.Api.Filters;

public class ExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var problem = new ProblemDetails
        {
            Status = context.Exception is JiraException ? 400 : 500,
            Title = "One or more errors occurred.",
            Detail = context.Exception.Message
        };
        context.Result = new ObjectResult(problem)
        {
            StatusCode = problem.Status
        };
    }
}