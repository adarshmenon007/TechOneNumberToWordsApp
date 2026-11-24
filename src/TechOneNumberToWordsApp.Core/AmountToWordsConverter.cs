namespace TechOneNumberToWordsApp.Core;

/// <summary>
/// Converts an amount of Australian dollars, typed as text, into words
/// (for example "123.45" becomes "ONE HUNDRED AND TWENTY-THREE DOLLARS AND FORTY-FIVE CENTS").
/// It holds no state, so one instance can be shared by every request (D23).
/// </summary>
/// <remarks>
/// Built test-first: each rule (the remaining rejections) is added when its tests are written.
/// </remarks>
public sealed class AmountToWordsConverter
{
    /// <summary>The longest raw input accepted, counting every character including spaces (D29).</summary>
    public const int MaxInputLength = 30;

    /// <summary>Converts the amount the user typed into words, or says what is wrong with it.</summary>
    /// <param name="input">The raw amount, for example "45.50".</param>
    public ConversionResult Convert(string input)
    {
        // Check first, so the parser only ever sees input that has passed every rule (D27).
        var error = AmountValidator.Validate(input);
        if (error is not null)
        {
            return ConversionResult.Failure(error);
        }

        var amount = AmountParser.Parse(AmountValidator.TrimSpacesAndTabs(input));

        return ConversionResult.Success(MoneyWords.Spell(amount));
    }
}
