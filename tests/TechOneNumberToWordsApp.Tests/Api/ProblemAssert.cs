using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TechOneNumberToWordsApp.Tests.Api;

internal static class ProblemAssert
{
    /// <summary>
    /// Checks a request error (D24, D41): the status, the problem media type (only the media type, because ASP.NET
    /// adds a charset), the errorCode, the title "Invalid request" and the status repeated in the body. Errors the
    /// framework makes carry no detail (D24 amendment), so this checks there is none.
    /// </summary>
    public static async Task RequestErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string errorCode,
        CancellationToken cancellation)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellation);
        Assert.Equal(errorCode, problem.GetProperty("errorCode").GetString());
        Assert.Equal("Invalid request", problem.GetProperty("title").GetString());
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.False(problem.TryGetProperty("detail", out _), "A request error must not carry a detail.");
    }
}
