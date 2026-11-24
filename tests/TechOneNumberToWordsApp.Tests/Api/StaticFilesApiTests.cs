using System.Net;

namespace TechOneNumberToWordsApp.Tests.Api;

/// <summary>
/// The page and its files (D38). They are served by the same app, so they must carry the security headers like every
/// other response (D34), and the script must never put server text into the page as HTML (D24). The factory is a class
/// fixture, so xUnit disposes it.
/// </summary>
public class StaticFilesApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // The four files of the page: the default document, its script, its stylesheet and the icon (D38, C3).
    public static TheoryData<string, string> Files => new()
    {
        { "/", "text/html" },
        { "/app.js", "text/javascript" },
        { "/site.css", "text/css" },
        { "/favicon.svg", "image/svg+xml" },
    };

    [Theory]
    [MemberData(nameof(Files))]
    [Trait("Id", "SEC-005")]
    [Trait("Decision", "D34")]
    [Trait("Decision", "D38")]
    [Trait("Decision", "C3")]
    public async Task StaticFile_IsServedWithTheSecurityHeaders(string path, string mediaType)
    {
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
        SecurityHeadersAssert.Present(response);
    }

    [Fact]
    [Trait("Id", "SEC-014")]
    [Trait("Decision", "D24")]
    public async Task Script_NeverInsertsHtml()
    {
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/app.js", TestContext.Current.CancellationToken);
        var script = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Without these two checks the test would pass on an error body or an empty file, which contain none of the
        // forbidden words.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("textContent", script);

        // A text scan: it includes comments, so a comment in app.js must not name these either.
        foreach (var forbidden in new[] { "innerHTML", "outerHTML", "insertAdjacentHTML", "document.write" })
        {
            Assert.DoesNotContain(forbidden, script);
        }
    }
}
