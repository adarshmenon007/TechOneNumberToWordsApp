using TechOneNumberToWordsApp.Core;

namespace TechOneNumberToWordsApp.Tests.Unit;

/// <summary>
/// A lone surrogate is half of an emoji-style character with no partner. JSON cannot carry one
/// (System.Text.Json rejects or replaces it), and xUnit's theory data would also mangle it, so these
/// cases are built inside the test instead of in <c>conversion-cases.json</c>.
/// The message must give the real code point (not U+FFFD) and no quoted character, because there is
/// nothing visible to show (E4).
/// </summary>
public class LoneSurrogateTests
{
    [Fact]
    [Trait("Id", "VAL-021")]
    [Trait("Decision", "E4")]
    public void Convert_LoneSurrogate_ReportsItsCodePointOnly()
    {
        var cases = new (string Input, string ExpectedCodePoint)[]
        {
            ("5\uD83D", "(U+D83D)"), // high surrogate at the end, no partner
            ("\uDE005", "(U+DE00)"), // low surrogate at the start
            ("5\uD83D5", "(U+D83D)"), // high surrogate followed by a digit
        };

        foreach (var (input, expectedCodePoint) in cases)
        {
            var result = new AmountToWordsConverter().Convert(input);

            Assert.False(result.IsSuccess, $"Input ending {expectedCodePoint} was accepted.");
            Assert.Equal(ConversionErrorCode.InvalidCharacter, result.Error.Code);
            Assert.Contains(expectedCodePoint, result.Error.Message);
            Assert.DoesNotContain("'", result.Error.Message);
        }
    }
}
