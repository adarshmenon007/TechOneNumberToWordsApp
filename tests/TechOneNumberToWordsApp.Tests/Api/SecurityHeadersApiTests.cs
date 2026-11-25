using System.Net;
using System.Text;

namespace TechOneNumberToWordsApp.Tests.Api;

/// <summary>
/// The four security headers must be on every response, including errors (D34). The values are checked exactly,
/// not just for presence. The 500 case uses a converter that throws, so it runs under <c>Testing</c> only (D45).
/// </summary>
public class SecurityHeadersApiTests
{
    [Fact]
    [Trait("Id", "SEC-001")]
    [Trait("Decision", "D34")]
    public async Task SuccessfulResponse_HasSecurityHeaders()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await PostAsync(client, """{"amount":"5"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        SecurityHeadersAssert.Present(response);
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
        SecurityHeadersAssert.Present(response);
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
        SecurityHeadersAssert.Present(response);
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
        SecurityHeadersAssert.Present(response);
    }

    // The caller owns the factory and the client and keeps them alive until it has read the response.
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        return await client.PostAsync("/api/convert", content, TestContext.Current.CancellationToken);
    }
}
