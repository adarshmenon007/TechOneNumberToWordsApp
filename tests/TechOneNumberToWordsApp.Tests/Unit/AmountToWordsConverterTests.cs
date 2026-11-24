using TechOneNumberToWordsApp.Core;
using TechOneNumberToWordsApp.Tests.TestData;

namespace TechOneNumberToWordsApp.Tests.Unit;

/// <summary>
/// Runs every row of <c>conversion-cases.json</c> through the converter (D43, D44).
/// The test receives only the case ID; the row's ID and decisions travel as traits (D47, D48),
/// so <c>--filter "Decision=D8"</c> selects the matching rows.
/// </summary>
public class AmountToWordsConverterTests
{
    public static IEnumerable<TheoryDataRow<string>> Cases() =>
        ConversionCases.All.Select(c => new TheoryDataRow<string>(c.Id)
        {
            TestDisplayName = $"{c.Id}: {c.Description}",
            Traits = new Dictionary<string, HashSet<string>>
            {
                ["Id"] = [c.Id],
                ["Decision"] = [.. c.Decisions],
            },
        });

    [Theory]
    [MemberData(nameof(Cases))]
    public void Convert_MatchesExpectedOutcome(string caseId)
    {
        var testCase = ConversionCases.Get(caseId);

        var result = new AmountToWordsConverter().Convert(testCase.Input);

        if (testCase.ExpectedWords is not null)
        {
            Assert.True(result.IsSuccess, $"{caseId}: '{testCase.Input}' was rejected: {result.Error?.Message}");
            Assert.Equal(testCase.ExpectedWords, result.Words);
            return;
        }

        // The loader guarantees a row has either expectedWords or expectedErrorCode.
        Assert.False(result.IsSuccess, $"{caseId}: '{testCase.Input}' was accepted as: {result.Words}");
        Assert.NotNull(result.Error);
        Assert.Equal(testCase.ExpectedErrorCode, result.Error.Code.ToString());

        if (testCase.MessageContains is not null)
        {
            Assert.Contains(testCase.MessageContains, result.Error.Message);
        }
    }
}
