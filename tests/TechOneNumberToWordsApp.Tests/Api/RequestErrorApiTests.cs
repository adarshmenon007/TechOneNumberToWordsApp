using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace TechOneNumberToWordsApp.Tests.Api;

/// <summary>
/// What the API answers when the request itself is wrong, not the amount (D24e, D41, E5). Each test runs under
/// <c>Testing</c> and under <c>Development</c>: Development is what <c>dotnet run</c> uses, and the framework
/// behaves differently there (D24 amendment). Every factory a test creates is disposed.
/// </summary>
public class RequestErrorApiTests
{
    private const string ConvertPath = "/api/convert";

    public static TheoryData<string> Environments => ["Testing", "Development"];

    [Theory]
    [MemberData(nameof(Environments))]
    [Trait("Id", "API-003")]
    [Trait("Decision", "D18")]
    [Trait("Decision", "D24e")]
    public async Task EmptyBody_IsInvalidRequest(string environment)
    {
        using var response = await PostAsync(environment, "application/json", string.Empty);

        await ProblemAssert.RequestErrorAsync(response, HttpStatusCode.BadRequest, "InvalidRequest", TestContext.Current.CancellationToken);
    }

    [Theory]
    [MemberData(nameof(Environments))]
    [Trait("Id", "API-004")]
    [Trait("Decision", "D24e")]
    public async Task AmountAbsent_IsMissingField(string environment)
    {
        using var response = await PostAsync(environment, "application/json", "{}");

        await ProblemAssert.RequestErrorAsync(response, HttpStatusCode.BadRequest, "MissingField", TestContext.Current.CancellationToken);
    }

    [Theory]
    [MemberData(nameof(Environments))]
    [Trait("Id", "API-005")]
    [Trait("Decision", "D24e")]
    public async Task AmountNull_IsMissingField(string environment)
    {
        using var response = await PostAsync(environment, "application/json", """{"amount":null}""");

        await ProblemAssert.RequestErrorAsync(response, HttpStatusCode.BadRequest, "MissingField", TestContext.Current.CancellationToken);
    }

    [Theory]
    [MemberData(nameof(Environments))]
    [Trait("Id", "API-006")]
    [Trait("Decision", "D24e")]
    [Trait("Decision", "D41")]
    public async Task MalformedJson_IsInvalidRequest(string environment)
    {
        using var response = await PostAsync(environment, "application/json", """{"amount":""");

        await ProblemAssert.RequestErrorAsync(response, HttpStatusCode.BadRequest, "InvalidRequest", TestContext.Current.CancellationToken);
    }

    [Theory]
    [MemberData(nameof(Environments))]
    [Trait("Id", "API-007")]
    [Trait("Decision", "E5")]
    [Trait("Decision", "D41")]
    public async Task AmountSentAsJsonNumber_IsInvalidRequest(string environment)
    {
        using var response = await PostAsync(environment, "application/json", """{"amount":5}""");

        await ProblemAssert.RequestErrorAsync(response, HttpStatusCode.BadRequest, "InvalidRequest", TestContext.Current.CancellationToken);
    }

    [Theory]
    [MemberData(nameof(Environments))]
    [Trait("Id", "API-008")]
    [Trait("Decision", "E5")]
    public async Task UnknownExtraField_IsIgnored(string environment)
    {
        using var response = await PostAsync(environment, "application/json", """{"amount":"5","extra":1}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("FIVE DOLLARS", body.GetProperty("words").GetString());
    }

    [Theory]
    [MemberData(nameof(Environments))]
    [Trait("Id", "API-009")]
    [Trait("Decision", "D24c")]
    [Trait("Decision", "D41")]
    public async Task NonJsonContentType_IsUnsupportedMediaType(string environment)
    {
        using var response = await PostAsync(environment, "text/plain", "5");

        await ProblemAssert.RequestErrorAsync(response, HttpStatusCode.UnsupportedMediaType, "UnsupportedMediaType", TestContext.Current.CancellationToken);
    }

    [Theory]
    [MemberData(nameof(Environments))]
    [Trait("Id", "API-010")]
    [Trait("Decision", "D41")]
    public async Task UnknownRoute_IsNotFound(string environment)
    {
        using var factory = new ApiFactory(environment);
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/nothing-here", TestContext.Current.CancellationToken);

        await ProblemAssert.RequestErrorAsync(response, HttpStatusCode.NotFound, "NotFound", TestContext.Current.CancellationToken);
    }

    [Theory]
    [MemberData(nameof(Environments))]
    [Trait("Id", "API-011")]
    [Trait("Decision", "D41")]
    public async Task WrongMethod_IsMethodNotAllowed(string environment)
    {
        using var factory = new ApiFactory(environment);
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(ConvertPath, TestContext.Current.CancellationToken);

        await ProblemAssert.RequestErrorAsync(response, HttpStatusCode.MethodNotAllowed, "MethodNotAllowed", TestContext.Current.CancellationToken);
    }

    // Posts raw text with the given content type, so the tests control exactly what goes on the wire.
    private static async Task<HttpResponseMessage> PostAsync(string environment, string contentType, string body)
    {
        using var factory = new ApiFactory(environment);
        using var client = factory.CreateHttpsClient();
        using var content = new StringContent(body, Encoding.UTF8, contentType);

        return await client.PostAsync(ConvertPath, content, TestContext.Current.CancellationToken);
    }
}
