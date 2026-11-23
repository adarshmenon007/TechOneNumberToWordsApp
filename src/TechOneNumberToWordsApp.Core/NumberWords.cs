namespace TechOneNumberToWordsApp.Core;

/// <summary>
/// Spells a whole number in English capitals, for example 45 as "FORTY-FIVE" (O1).
/// </summary>
/// <remarks>
/// Built test-first. For now it covers 0 to 99; hundreds and the larger scales are added with their tests.
/// </remarks>
internal static class NumberWords
{
    // 0 to 19 each have their own word, so one lookup covers them.
    private static readonly string[] BelowTwenty =
    [
        "ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE",
        "TEN", "ELEVEN", "TWELVE", "THIRTEEN", "FOURTEEN", "FIFTEEN", "SIXTEEN", "SEVENTEEN", "EIGHTEEN", "NINETEEN",
    ];

    // Indexed by the tens digit, so index 4 is "FORTY". Indexes 0 and 1 are never used (below twenty).
    private static readonly string[] Tens =
    [
        "", "", "TWENTY", "THIRTY", "FORTY", "FIFTY", "SIXTY", "SEVENTY", "EIGHTY", "NINETY",
    ];

    /// <summary>Spells <paramref name="number"/> in words.</summary>
    public static string Spell(long number)
    {
        if (number < 20)
        {
            return BelowTwenty[number];
        }

        if (number < 100)
        {
            var tens = Tens[number / 10];
            var units = number % 10;

            // 21 to 99 are written with a hyphen between the tens and the units: FORTY-FIVE.
            return units == 0 ? tens : $"{tens}-{BelowTwenty[units]}";
        }

        throw new NotImplementedException("Numbers of 100 or more are not supported so far; more rules arrive test-first.");
    }
}
