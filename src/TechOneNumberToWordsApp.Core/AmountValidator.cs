using System.Buffers;
using System.Globalization;
using System.Text;

namespace TechOneNumberToWordsApp.Core;

/// <summary>
/// Checks the text the user typed, before it is parsed. The checks run in a fixed order and the first
/// one that fails is the error returned (D27), so the parser only ever sees valid input.
/// </summary>
/// <remarks>
/// Built test-first. Checks 1 to 7 are here; the decimal point, grouping, decimal places and size
/// checks are added with their tests.
/// </remarks>
internal static class AmountValidator
{
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
            return Error(
                ConversionErrorCode.AmountTooLong,
                $"The amount is too long. Please enter no more than {AmountToWordsConverter.MaxInputLength} characters.");
        }

        // 2. Nothing left once spaces and tabs are trimmed.
        var text = TrimSpacesAndTabs(input);
        if (text.Length == 0)
        {
            return Error(ConversionErrorCode.AmountRequired, "Please enter an amount.");
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
        return FindInvalidCharacter(text);
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
            or UnicodeCategory.ParagraphSeparator);

    private static ConversionError Error(ConversionErrorCode code, string message) => new(code, message);
}
