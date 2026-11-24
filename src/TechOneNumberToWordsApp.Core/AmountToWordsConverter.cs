namespace TechOneNumberToWordsApp.Core;

/// <summary>
/// Converts an amount of Australian dollars, typed as text, into words
/// (for example "123.45" becomes "ONE HUNDRED AND TWENTY-THREE DOLLARS AND FORTY-FIVE CENTS").
/// It holds no state, so one instance can be shared by every request (D23).
/// </summary>
public sealed class AmountToWordsConverter
{
    /// <summary>The longest raw input accepted, counting every character including spaces (D29).</summary>
    internal const int MaxInputLength = 30;

    /// <summary>The most digits allowed before the decimal point, not counting leading zeros (D58).</summary>
    internal const int MaxIntegerDigits = 15;

    /// <summary>
    /// The most digits allowed after the decimal point (E1). This must stay 2: cents are fixed (D40), and the parser,
    /// <c>MoneyAmount</c> and the cents wording all assume two digits.
    /// </summary>
    internal const int MaxDecimalPlaces = 2;

    /// <summary>The text of the AmountTooLong error (C1).</summary>
    public static string AmountTooLongMessage => throw new NotImplementedException();

    /// <summary>Converts the amount the user typed into words, or says what is wrong with it.</summary>
    /// <param name="input">The raw amount, for example "45.50".</param>
    public ConversionResult Convert(string input)
    {
        // Null can only come from a bug in the caller; a user can only type text (D23).
        ArgumentNullException.ThrowIfNull(input);

        // Check first, so the parser only ever sees input that has passed every rule (D27).
        var error = AmountValidator.Validate(input);
        if (error is not null)
        {
            return ConversionResult.Failure(error);
        }

        // The validator needs the raw text to measure the length cap (D29); the parser only wants the trimmed text.
        var amount = AmountParser.Parse(AmountValidator.TrimSpacesAndTabs(input));

        return ConversionResult.Success(MoneyWords.Spell(amount));
    }
}
