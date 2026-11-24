using System.Buffers;
using System.Globalization;
using System.Text;

namespace TechOneNumberToWordsApp.Core;

/// <summary>
/// Checks the text the user typed, before it is parsed. The checks run in a fixed order and the first
/// one that fails is the error returned (D27), so the parser only ever sees valid input.
/// </summary>
/// <remarks>
/// The numbered comments in <see cref="Validate"/> follow the order in the design (D27).
/// </remarks>
internal static class AmountValidator
{
    // Shown for check 2 (nothing entered) and check 9 (no digits), which are the same mistake to the user.
    private const string AmountRequiredMessage = "Please enter an amount.";

    // The largest amount written with commas and a point, such as 999,999,999,999,999.99, built from the limits
    // so the message cannot drift from them. InvariantCulture keeps the commas and point the same on every machine (D11).
    private static readonly string LargestAmountText = BuildLargestAmountText();

    /// <summary>
    /// Removes spaces and tabs from both ends. <c>string.Trim()</c> with no arguments would also remove
    /// other whitespace, which must be reported as an invalid character instead (D10).
    /// </summary>
    public static string TrimSpacesAndTabs(string input) => input.Trim(' ', '\t');

    /// <summary>Returns the first problem found, or null when the amount is acceptable so far.</summary>
    public static ConversionError? Validate(string input)
    {
        // 1. The length cap counts the raw text, before trimming (D29).
        if (input.Length > AmountToWordsConverter.MaxInputLength)
        {
            return Error(ConversionErrorCode.AmountTooLong, AmountToWordsConverter.AmountTooLongMessage);
        }

        // 2. Nothing left once spaces and tabs are trimmed.
        var text = TrimSpacesAndTabs(input);
        if (text.Length == 0)
        {
            return Error(ConversionErrorCode.AmountRequired, AmountRequiredMessage);
        }

        // 3. A currency symbol anywhere ($, €, £, ...), by Unicode category rather than a fixed list (D22).
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.CurrencySymbol)
            {
                return Error(
                    ConversionErrorCode.CurrencySymbolNotAllowed,
                    "Please enter the amount without a currency symbol. Amounts are in Australian dollars.");
            }
        }

        // 4 and 5. A sign at the start. A minus or plus anywhere else is an invalid character (check 7).
        if (text[0] == '-')
        {
            return Error(
                ConversionErrorCode.NegativeAmount,
                "Negative amounts are not supported. Please enter an amount of zero or more.");
        }

        if (text[0] == '+')
        {
            return Error(ConversionErrorCode.SignNotAllowed, "Please enter the amount without a plus sign.");
        }

        // 6. A space or tab between the characters (those at the ends are already trimmed).
        if (text.Contains(' ') || text.Contains('\t'))
        {
            return Error(ConversionErrorCode.ContainsWhitespace, "Please remove the spaces or tabs inside the amount.");
        }

        // 7. Anything that is not a digit, decimal point or comma. Only the ASCII digits count (D10).
        var invalidCharacter = FindInvalidCharacter(text);
        if (invalidCharacter is not null)
        {
            return invalidCharacter;
        }

        // From here on the text holds only digits, commas and decimal points.
        // Split it at the decimal point: "1,234.5" has the whole part "1,234" and the decimal part "5".
        var firstPoint = text.IndexOf('.');
        var whole = firstPoint < 0 ? text : text[..firstPoint];
        var decimals = firstPoint < 0 ? string.Empty : text[(firstPoint + 1)..];

        // 8. More than one decimal point. The extra points end up in the decimal part.
        if (decimals.Contains('.'))
        {
            return Error(
                ConversionErrorCode.MultipleDecimalPoints,
                "The amount has more than one decimal point. Please use a single decimal point.");
        }

        // 9. No digits at all (".", ",", ",,") is the same as entering nothing (E2).
        if (text.AsSpan().IndexOfAnyInRange('0', '9') < 0)
        {
            return Error(ConversionErrorCode.AmountRequired, AmountRequiredMessage);
        }

        // 10. Commas must group the whole part in threes (E3), and cannot appear after the decimal point (D28).
        if (whole.Contains(',') && !HasValidGrouping(whole))
        {
            return Error(
                ConversionErrorCode.InvalidThousandsSeparator,
                "The commas in the amount are in the wrong place. Use a comma only between groups of three digits, for example 1,234,567.");
        }

        if (decimals.Contains(','))
        {
            return Error(ConversionErrorCode.InvalidThousandsSeparator, "Commas cannot be used after the decimal point.");
        }

        // 11. Too many decimal places (E1). "1.500" is rejected rather than guessed at: some countries read it as 1500.
        if (decimals.Length > AmountToWordsConverter.MaxDecimalPlaces)
        {
            return Error(
                ConversionErrorCode.TooManyDecimalPlaces,
                FormattableString.Invariant(
                    $"The amount has too many decimal places. Please enter at most {AmountToWordsConverter.MaxDecimalPlaces} digits after the decimal point."));
        }

        // 12. Too large. Leading zeros and commas do not count towards the digit limit (D58).
        if (CountSignificantDigits(whole) > AmountToWordsConverter.MaxIntegerDigits)
        {
            return Error(
                ConversionErrorCode.AmountTooLarge,
                $"The amount is too large. The largest amount is {LargestAmountText}.");
        }

        return null;
    }

    private static string BuildLargestAmountText()
    {
        long largestWhole = 0;
        for (var digit = 0; digit < AmountToWordsConverter.MaxIntegerDigits; digit++)
        {
            largestWhole = (largestWhole * 10) + 9;
        }

        return largestWhole.ToString("N0", CultureInfo.InvariantCulture)
            + "."
            + new string('9', AmountToWordsConverter.MaxDecimalPlaces);
    }

    // Valid grouping: a first group of 1 to 3 digits that does not start with 0, then groups of exactly 3 digits.
    // That rejects ",123" (empty first group), "1,,234" and "1,234," (empty groups), "1,23" and "1,2345"
    // (wrong size), and "0,001" (a first group starting with 0 can read as a fraction in other countries).
    private static bool HasValidGrouping(string whole)
    {
        var groups = whole.Split(',');

        if (groups[0].Length is < 1 or > 3 || groups[0][0] == '0')
        {
            return false;
        }

        for (var index = 1; index < groups.Length; index++)
        {
            if (groups[index].Length != 3)
            {
                return false;
            }
        }

        return true;
    }

    // Digits from the first non-zero digit onwards, skipping commas: "000,000" has 0, "0012" has 2.
    private static int CountSignificantDigits(string whole)
    {
        var count = 0;
        foreach (var character in whole)
        {
            if (character == ',')
            {
                continue;
            }

            if (count > 0 || character != '0')
            {
                count++;
            }
        }

        return count;
    }

    private static ConversionError? FindInvalidCharacter(string text)
    {
        // Step through whole code points, not UTF-16 characters, so an emoji is reported once (F1).
        for (var index = 0; index < text.Length;)
        {
            var status = Rune.DecodeFromUtf16(text.AsSpan(index), out var rune, out var consumed);

            // A lone surrogate cannot be decoded (InvalidData, or NeedMoreData when it ends the text); report the
            // character's own code point, not the replacement character U+FFFD.
            var isLoneSurrogate = status != OperationStatus.Done;
            var codePoint = isLoneSurrogate ? text[index] : rune.Value;
            index += consumed;

            if (IsAllowed(codePoint))
            {
                continue;
            }

            // Visible characters are quoted so look-alikes (a full-width 5) are clear; invisible ones
            // (control, format, separators, lone surrogates) would show as a blank, so only the code point is given (E4).
            var shown = !isLoneSurrogate && IsVisible(rune) ? $"'{rune}' " : string.Empty;
            return Error(
                ConversionErrorCode.InvalidCharacter,
                $"Invalid character {shown}(U+{codePoint:X4}). Use only digits, a decimal point and commas.");
        }

        return null;
    }

    private static bool IsAllowed(int codePoint) =>
        codePoint is >= '0' and <= '9' or '.' or ',';

    private static bool IsVisible(Rune rune) =>
        Rune.GetUnicodeCategory(rune) is not (
            UnicodeCategory.Control
            or UnicodeCategory.Format
            or UnicodeCategory.SpaceSeparator
            or UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator
            or UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark
            or UnicodeCategory.PrivateUse
            or UnicodeCategory.OtherNotAssigned);

    private static ConversionError Error(ConversionErrorCode code, string message) => new(code, message);
}
