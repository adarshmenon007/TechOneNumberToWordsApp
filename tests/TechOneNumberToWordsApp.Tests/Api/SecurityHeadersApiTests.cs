using System.Net;
using System.Text;

namespace TechOneNumberToWordsApp.Tests.Api;

/// <summary>
/// The four security headers must be on every response, including errors (D34). The values are checked exactly,
/// not just for presence. The 500 case uses a converter that throws, so it runs under <c>Testing</c> only (D45).
/// </summary>
public class SecurityHeadersApiTests
{
    private static readonly (string Name, string Value)[] Expected =
    [
        ("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'"),
        ("X-Content-Type-Options", "nosniff"),
        ("Referrer-Policy", "no-referrer"),
        ("X-Frame-Options", "DENY"),
    ];

    [Fact]
    [Trait("Id", "SEC-001")]
    [Trait("Decision", "D34")]
    public async Task SuccessfulResponse_HasSecurityHeaders()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await PostAsync(client, """{"amount":"5"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    [Fact]
    [Trait("Id", "SEC-002")]
    [Trait("Decision", "D34")]
    public async Task BadRequestResponse_HasSecurityHeaders()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await PostAsync(client, """{"amount":"abc"}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    [Fact]
    [Trait("Id", "SEC-003")]
    [Trait("Decision", "D34")]
    public async Task NotFoundResponse_HasSecurityHeaders()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/nothing-here", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    [Fact]
    [Trait("Id", "SEC-004")]
    [Trait("Decision", "D34")]
    public async Task ServerErrorResponse_HasSecurityHeaders()
    {
        using var factory = ApiFactory.WithConverterThatThrows("boom");
        using var client = factory.CreateHttpsClient();
        using var response = await PostAsync(client, """{"amount":"5"}""");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    // The caller owns the factory and the client and keeps them alive until it has read the response.
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        return await client.PostAsync("/api/convert", content, TestContext.Current.CancellationToken);
    }

    // Each header is present once, with exactly the agreed value.
    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        foreach (var (name, value) in Expected)
        {
            Assert.True(response.Headers.TryGetValues(name, out var values), $"Missing header {name}.");
            Assert.Equal(value, Assert.Single(values));
        }
    }
}
