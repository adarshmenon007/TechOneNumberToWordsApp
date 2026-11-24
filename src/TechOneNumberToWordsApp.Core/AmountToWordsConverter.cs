namespace TechOneNumberToWordsApp.Core;

/// <summary>
/// Converts an amount of Australian dollars, typed as text, into words
/// (for example "123.45" becomes "ONE HUNDRED AND TWENTY-THREE DOLLARS AND FORTY-FIVE CENTS").
/// It holds no state, so one instance can be shared by every request (D23).
/// </summary>
/// <remarks>
/// Built test-first: each rule (separators, rejections) is added when its tests are written.
/// </remarks>
public sealed class AmountToWordsConverter
{
    /// <summary>Converts the amount the user typed into words.</summary>
    /// <param name="input">The raw amount, for example "45.50".</param>
    public ConversionResult Convert(string input)
    {
        var amount = AmountParser.Parse(input);

        return ConversionResult.Success(MoneyWords.Spell(amount));
    }
}
