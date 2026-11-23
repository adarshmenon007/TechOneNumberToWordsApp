namespace TechOneNumberToWordsApp.Core;

/// <summary>
/// Converts an amount of Australian dollars, typed as text, into words
/// (for example "123.45" becomes "ONE HUNDRED AND TWENTY-THREE DOLLARS AND FORTY-FIVE CENTS").
/// It holds no state, so one instance can be shared by every request (D23).
/// </summary>
/// <remarks>
/// Built test-first. This slice handles a single digit only; every later rule (more digits,
/// cents, separators, rejections) is added when its tests are written.
/// </remarks>
public sealed class AmountToWordsConverter
{
    private static readonly string[] DigitWords =
    [
        "ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE",
    ];

    /// <summary>Converts the amount the user typed into words.</summary>
    /// <param name="input">The raw amount, for example "5".</param>
    public ConversionResult Convert(string input)
    {
        // Only the ASCII characters '0' to '9' count as digits (D10); char.IsDigit would also accept
        // digits from other scripts. Anything else is a later slice's job, so fail loudly rather than guess.
        if (input.Length != 1 || input[0] < '0' || input[0] > '9')
        {
            throw new NotImplementedException("Only a single digit is supported so far; more rules arrive test-first.");
        }

        var dollars = input[0] - '0';

        // Exactly one dollar is singular; every other amount, including zero, is plural (D58).
        var unit = dollars == 1 ? "DOLLAR" : "DOLLARS";

        return ConversionResult.Success($"{DigitWords[dollars]} {unit}");
    }
}
