using TechOneNumberToWordsApp.Core;

namespace TechOneNumberToWordsApp.Tests.Unit;

/// <summary>
/// The web layer reuses the text of the AmountTooLong error for the 413 response (C1), so the library must expose the
/// one message it uses, not a copy. It is a C# test because it reads a property, not a conversion row.
/// </summary>
public class AmountTooLongMessageTests
{
    [Fact]
    [Trait("Id", "LIM-012")]
    [Trait("Decision", "D23")]
    [Trait("Decision", "C1")]
    public void AmountTooLongMessage_IsTheMessageOfTheAmountTooLongError()
    {
        var converter = new AmountToWordsConverter();

        // 31 characters: one more than the cap (D29), so check 1 rejects it.
        var result = converter.Convert(new string('1', 31));

        Assert.False(result.IsSuccess);
        Assert.Equal(ConversionErrorCode.AmountTooLong, result.Error.Code);
        Assert.Equal(result.Error.Message, AmountToWordsConverter.AmountTooLongMessage);
    }
}
