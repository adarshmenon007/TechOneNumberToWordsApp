namespace TechOneNumberToWordsApp.Core;

/// <summary>
/// Converts an amount of Australian dollars, typed as text, into words
/// (for example "123.45" becomes "ONE HUNDRED AND TWENTY-THREE DOLLARS AND FORTY-FIVE CENTS").
/// It holds no state, so one instance can be shared by every request (D23).
/// </summary>
/// <remarks>
/// Built test-first: each rule (the rejections) is added when its tests are written.
/// </remarks>
public sealed class AmountToWordsConverter
{
    /// <summary>Converts the amount the user typed into words.</summary>
    /// <param name="input">The raw amount, for example "45.50".</param>
    public ConversionResult Convert(string input)
    {
        // Only spaces and tabs are trimmed, and only at the two ends (D9, D10).
        // string.Trim() with no arguments would also strip other kinds of whitespace.
        var trimmed = input.Trim(' ', '\t');

        var amount = AmountParser.Parse(trimmed);

        return ConversionResult.Success(MoneyWords.Spell(amount));
    }
}
