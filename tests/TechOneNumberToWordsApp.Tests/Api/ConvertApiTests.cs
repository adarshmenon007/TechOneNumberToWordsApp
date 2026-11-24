using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TechOneNumberToWordsApp.Tests.TestData;

namespace TechOneNumberToWordsApp.Tests.Api;

/// <summary>
/// Runs every row of <c>conversion-cases.json</c> a second time, through <c>POST /api/convert</c> (D44). The IDs and
/// traits are the same as in the unit tests, so a row is selected in both places by one filter.
/// </summary>
public class ConvertApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    public static IEnumerable<TheoryDataRow<string>> Cases() =>
        ConversionCases.All.Select(c => new TheoryDataRow<string>(c.Id)
        {
            TestDisplayName = $"{c.Id}: {c.Description} (HTTP)",
            Traits = new Dictionary<string, HashSet<string>>
            {
                ["Id"] = [c.Id],
                ["Decision"] = [.. c.Decisions],
            },
        });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Api_Convert_MatchesExpectedOutcome(string caseId)
    {
        var testCase = ConversionCases.Get(caseId);
        var cancellation = TestContext.Current.CancellationToken;
        using var client = factory.CreateHttpsClient();

        // The input travels as JSON text, so invisible and non-ASCII characters are escaped on the way (D44).
        using var response = await client.PostAsJsonAsync("/api/convert", new { amount = testCase.Input }, cancellation);

        // Only the media type is compared: ASP.NET sends "application/json; charset=utf-8".
        var mediaType = response.Content.Headers.ContentType?.MediaType;

        if (testCase.ExpectedWords is not null)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", mediaType);

            var success = await response.Content.ReadFromJsonAsync<JsonElement>(cancellation);
            Assert.Equal(testCase.ExpectedWords, success.GetProperty("words").GetString());
            return;
        }

        // The loader guarantees a row has either expectedWords or expectedErrorCode.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", mediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellation);
        Assert.Equal(testCase.ExpectedErrorCode, problem.GetProperty("errorCode").GetString());
        Assert.Equal("Invalid amount", problem.GetProperty("title").GetString());

        if (testCase.MessageContains is not null)
        {
            Assert.Contains(testCase.MessageContains, problem.GetProperty("detail").GetString());
        }
    }
}
