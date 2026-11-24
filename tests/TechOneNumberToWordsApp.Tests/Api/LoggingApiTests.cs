using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TechOneNumberToWordsApp.Web.Endpoints;

namespace TechOneNumberToWordsApp.Tests.Api;

/// <summary>
/// What the API logs (D33): a rejected amount is logged with its errorCode and never its text, a successful
/// conversion logs nothing, and an unexpected exception is logged once with the exception and the trace id (C2).
/// Every test creates its own factory, so its log holds only its own entries.
/// </summary>
public class LoggingApiTests
{
    // Text a user could type that must never reach a log. It is an invalid amount, so the converter rejects it.
    private const string Marker = "12abc-MARKER-7";

    // The endpoint's log category is its full type name (a static class cannot be an ILogger type argument).
    private static readonly string EndpointCategory = typeof(ConvertEndpoint).FullName!;

    [Fact]
    [Trait("Id", "API-013")]
    [Trait("Decision", "D33")]
    public async Task RejectedAmount_LogsOneInformationEntryWithTheErrorCode()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await PostAmountAsync(client, Marker);

        var entry = Assert.Single(factory.Logs.Entries, e => e.Category == EndpointCategory);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains(new KeyValuePair<string, object?>("ErrorCode", "InvalidCharacter"), entry.State);
    }

    [Fact]
    [Trait("Id", "API-014")]
    [Trait("Decision", "D33")]
    public async Task SuccessfulConversion_LogsNothingFromTheEndpoint()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await PostAmountAsync(client, "5");

        Assert.True(response.IsSuccessStatusCode);
        Assert.DoesNotContain(factory.Logs.Entries, e => e.Category == EndpointCategory && e.Level >= LogLevel.Information);
    }

    [Fact]
    [Trait("Id", "SEC-011")]
    [Trait("Decision", "D33")]
    public async Task RejectedAmount_TheRawInputIsInNoLogEntry()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await PostAmountAsync(client, Marker);

        var entries = factory.Logs.Entries;

        // Without this the test would pass just because nothing was logged.
        Assert.Contains(entries, e => e.Category == EndpointCategory);
        foreach (var entry in entries)
        {
            Assert.DoesNotContain(Marker, Flatten(entry));
        }
    }

    [Fact]
    [Trait("Id", "API-015")]
    [Trait("Decision", "D33")]
    [Trait("Decision", "C2")]
    public async Task UnexpectedException_LogsOneErrorWithTheExceptionAndTheTraceId()
    {
        const string message = "boom-for-the-log";
        using var factory = ApiFactory.WithConverterThatThrows(message);
        using var client = factory.CreateHttpsClient();
        using var response = await PostAmountAsync(client, "5");

        // The entry comes from the framework's exception handler, not from our code.
        var entry = Assert.Single(factory.Logs.Entries, e => e.Level >= LogLevel.Error);
        Assert.NotNull(entry.Exception);
        Assert.Equal(message, entry.Exception.Message);

        // The response shows the full W3C id, "00-<TraceId>-<SpanId>-01"; the log scope holds only the TraceId (C2).
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var responseTraceId = problem.GetProperty("traceId").GetString()!.Split('-')[1];
        var scopeTraceIds = entry.Scopes
            .OfType<IEnumerable<KeyValuePair<string, object?>>>()
            .SelectMany(scope => scope)
            .Where(item => item.Key == "TraceId")
            .Select(item => item.Value?.ToString());
        Assert.Contains(responseTraceId, scopeTraceIds);
    }

    // The caller owns the factory and the client and keeps them alive until it has read the response.
    private static async Task<HttpResponseMessage> PostAmountAsync(HttpClient client, string amount)
    {
        using var content = new StringContent(JsonSerializer.Serialize(new { amount }), Encoding.UTF8, "application/json");

        return await client.PostAsync("/api/convert", content, TestContext.Current.CancellationToken);
    }

    // Everything one entry holds, as text: the message, every structured value, every scope value and the exception.
    private static string Flatten(LogEntry entry)
    {
        var parts = new List<string?> { entry.Message, entry.Exception?.ToString() };
        parts.AddRange(entry.State.Select(item => $"{item.Key}={item.Value}"));
        foreach (var scope in entry.Scopes)
        {
            parts.Add(scope?.ToString());
            if (scope is IEnumerable<KeyValuePair<string, object?>> items)
            {
                parts.AddRange(items.Select(item => $"{item.Key}={item.Value}"));
            }
        }

        return string.Join('\n', parts);
    }
}
