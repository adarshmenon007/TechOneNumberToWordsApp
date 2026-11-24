using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace TechOneNumberToWordsApp.Tests.Api;

/// <summary>
/// What the API answers when something unexpected goes wrong (D24). The converter is replaced with a registration that
/// throws (D23, D45). These run under <c>Testing</c> only: Development turns on the developer exception page, which
/// shows the stack trace by design, so the generic 500 is not what a developer sees there.
/// </summary>
public class ServerErrorApiTests
{
    // Text that must never reach the client: the exception's message.
    private const string Secret = "boom-secret-4821";

    [Fact]
    [Trait("Id", "API-012")]
    [Trait("Decision", "D24")]
    [Trait("Decision", "D41")]
    public async Task UnexpectedException_Is500ServerError()
    {
        using var response = await PostWithBrokenConverterAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("ServerError", problem.GetProperty("errorCode").GetString());
        Assert.Equal("Server error", problem.GetProperty("title").GetString());
        Assert.Equal("An unexpected error occurred.", problem.GetProperty("detail").GetString());
        Assert.Equal(500, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()), "The response must carry a traceId.");
    }

    [Fact]
    [Trait("Id", "SEC-012")]
    [Trait("Decision", "D24")]
    public async Task UnexpectedException_BodyLeaksNothing()
    {
        using var response = await PostWithBrokenConverterAsync();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain(Secret, body);                                   // the exception message
        Assert.DoesNotContain(nameof(InvalidOperationException), body);        // the exception type name
        Assert.DoesNotContain("   at ", body);                                 // a stack frame
        Assert.DoesNotContain("TechOneNumberToWordsApp", body);                // our own namespaces and file paths

        var problem = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.False(problem.TryGetProperty("exception", out _), "The body must not carry an exception object.");
    }

    // A valid amount, so the failure can only come from the converter that throws when it is resolved.
    private static async Task<HttpResponseMessage> PostWithBrokenConverterAsync()
    {
        using var factory = ApiFactory.WithConverterThatThrows(Secret);
        using var client = factory.CreateHttpsClient();
        using var content = new StringContent("""{"amount":"5"}""", Encoding.UTF8, "application/json");

        return await client.PostAsync("/api/convert", content, TestContext.Current.CancellationToken);
    }
}
