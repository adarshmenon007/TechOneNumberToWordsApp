using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TechOneNumberToWordsApp.Core;

namespace TechOneNumberToWordsApp.Tests.Api;

/// <summary>
/// The request size limit and the Server header need a real Kestrel server: the in-memory TestServer enforces no
/// Kestrel limit and never sends a Server header (D34, D37). The server listens on plain http, because neither
/// behaviour has anything to do with HTTPS and CI has no development certificate (D37). The HTTPS redirection middleware
/// is in the pipeline (D35); these tests would fail with a 307 if it got in the way of plain http.
/// </summary>
public class RequestLimitApiTests
{
    // The limit is 1 KB (D37). A body is JSON text: 11 characters before the digits and 2 after, all one byte each.
    private const int LimitBytes = 1024;
    private const int BodyOverhead = 13;

    [Fact]
    [Trait("Id", "SEC-006")]
    [Trait("Decision", "D37")]
    [Trait("Decision", "C1")]
    public async Task BodyOverTheLimit_WithDeclaredLength_Is413()
    {
        using var factory = StartKestrel();
        using var client = factory.CreateClient();
        using var content = new StringContent(BodyWithDigits(3000), Encoding.UTF8, "application/json");
        Assert.NotNull(content.Headers.ContentLength);

        using var response = await client.PostAsync("/api/convert", content, TestContext.Current.CancellationToken);

        await AssertPayloadTooLargeAsync(response);
    }

    [Fact]
    [Trait("Id", "SEC-007")]
    [Trait("Decision", "D37")]
    public async Task BodyOverTheLimit_Chunked_Is413()
    {
        using var factory = StartKestrel();
        using var client = factory.CreateClient();
        using var content = new UnknownLengthContent(Encoding.UTF8.GetBytes(BodyWithDigits(3000)));
        Assert.Null(content.Headers.ContentLength);

        using var response = await client.PostAsync("/api/convert", content, TestContext.Current.CancellationToken);

        await AssertPayloadTooLargeAsync(response);
    }

    [Fact]
    [Trait("Id", "SEC-008")]
    [Trait("Decision", "D34")]
    public async Task Response_HasNoServerHeader()
    {
        using var factory = StartKestrel();
        using var client = factory.CreateClient();
        using var content = new StringContent("""{"amount":"5"}""", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/convert", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Server"), "The Server header must not be sent.");
    }

    [Fact]
    [Trait("Id", "SEC-010")]
    [Trait("Decision", "D37")]
    public async Task BodyAtTheLimit_ReachesTheConverter_AndOneByteMoreDoesNot()
    {
        using var factory = StartKestrel();
        using var client = factory.CreateClient();

        // Exactly 1,024 bytes: the limit lets it through, so the converter runs and rejects the long amount.
        var atLimit = BodyWithDigits(LimitBytes - BodyOverhead);
        Assert.Equal(LimitBytes, Encoding.UTF8.GetByteCount(atLimit));
        using var atLimitContent = new StringContent(atLimit, Encoding.UTF8, "application/json");
        using var atLimitResponse = await client.PostAsync("/api/convert", atLimitContent, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, atLimitResponse.StatusCode);
        var problem = await atLimitResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("AmountTooLong", problem.GetProperty("errorCode").GetString());

        // 1,025 bytes: the limit stops it before the converter.
        var overLimit = BodyWithDigits(LimitBytes - BodyOverhead + 1);
        Assert.Equal(LimitBytes + 1, Encoding.UTF8.GetByteCount(overLimit));
        using var overLimitContent = new StringContent(overLimit, Encoding.UTF8, "application/json");
        using var overLimitResponse = await client.PostAsync("/api/convert", overLimitContent, TestContext.Current.CancellationToken);

        await AssertPayloadTooLargeAsync(overLimitResponse);
    }

    // A real Kestrel server on a free port, plain http (D37).
    private static ApiFactory StartKestrel()
    {
        var factory = new ApiFactory();
        factory.UseKestrel(0);
        factory.StartServer();
        return factory;
    }

    private static string BodyWithDigits(int digits) => $$"""{"amount":"{{new string('1', digits)}}"}""";

    // The 413 carries its own errorCode, not AmountTooLong's, but reuses AmountTooLong's wording (C1).
    private static async Task AssertPayloadTooLargeAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("PayloadTooLarge", problem.GetProperty("errorCode").GetString());
        Assert.Equal("Invalid request", problem.GetProperty("title").GetString());
        Assert.Equal(AmountToWordsConverter.AmountTooLongMessage, problem.GetProperty("detail").GetString());
        Assert.Equal(413, problem.GetProperty("status").GetInt32());
    }

    // Content of unknown length, so HttpClient sends it with Transfer-Encoding: chunked.
    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] _bytes;

        public UnknownLengthContent(byte[] bytes)
        {
            _bytes = bytes;
            Headers.ContentType = new("application/json");
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(_bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
