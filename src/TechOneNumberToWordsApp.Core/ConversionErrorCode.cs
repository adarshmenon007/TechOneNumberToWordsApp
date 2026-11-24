namespace TechOneNumberToWordsApp.Core;

/// <summary>
/// Why an amount was rejected. The member names are part of the public API: the web layer sends
/// them to callers as text (D24d), so renaming one is a breaking change.
/// </summary>
/// <remarks>
/// Members are added as their rejection tests are written, in the order the checks run (D27).
/// </remarks>
public enum ConversionErrorCode
{
    /// <summary>The raw text is longer than <see cref="AmountToWordsConverter.MaxInputLength"/> characters.</summary>
    AmountTooLong,

    /// <summary>Nothing was entered: empty, only spaces and tabs, or no digits at all (such as "." or ",").</summary>
    AmountRequired,

    /// <summary>The text contains a currency symbol such as $ or €.</summary>
    CurrencySymbolNotAllowed,

    /// <summary>The amount starts with a minus sign.</summary>
    NegativeAmount,

    /// <summary>The amount starts with a plus sign.</summary>
    SignNotAllowed,

    /// <summary>A space or tab sits between the digits.</summary>
    ContainsWhitespace,

    /// <summary>A character other than a digit, decimal point or comma was found.</summary>
    InvalidCharacter,

    /// <summary>The text has more than one decimal point.</summary>
    MultipleDecimalPoints,

    /// <summary>A comma is in the wrong place, or comes after the decimal point.</summary>
    InvalidThousandsSeparator,

    /// <summary>More than <see cref="AmountToWordsConverter.MaxDecimalPlaces"/> digits follow the decimal point.</summary>
    TooManyDecimalPlaces,

    /// <summary>More than <see cref="AmountToWordsConverter.MaxIntegerDigits"/> digits come before the decimal point.</summary>
    AmountTooLarge,
}
