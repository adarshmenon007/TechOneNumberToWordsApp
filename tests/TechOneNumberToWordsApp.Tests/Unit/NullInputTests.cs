using TechOneNumberToWordsApp.Core;

namespace TechOneNumberToWordsApp.Tests.Unit;

/// <summary>
/// Null is a programming error, not something a user can type, so it throws instead of returning a
/// conversion error (D23, D24e). The web layer turns a missing amount into its own 400 before it gets here.
/// It is a C# test because JSON data rows hold text, and null is not text.
/// </summary>
public class NullInputTests
{
    [Fact]
    [Trait("Id", "VAL-044")]
    [Trait("Decision", "D23")]
    public void Convert_NullInput_ThrowsArgumentNullException()
    {
        var converter = new AmountToWordsConverter();

        var exception = Assert.Throws<ArgumentNullException>(() => converter.Convert(null!));

        Assert.Equal("input", exception.ParamName);
    }
}
