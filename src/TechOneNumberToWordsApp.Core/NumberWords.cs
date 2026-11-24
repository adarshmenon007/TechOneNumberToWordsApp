namespace TechOneNumberToWordsApp.Core;

/// <summary>
/// Spells a whole number in English capitals, for example 45 as "FORTY-FIVE" (O1).
/// </summary>
/// <remarks>
/// It covers 0 up to 999,999,999,999,999, the largest amount the service accepts (D58).
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

    // Indexed by group position, counted from the right in groups of three digits.
    // The first entry is empty because the lowest group has no scale word.
    private static readonly string[] Scales = ["", "THOUSAND", "MILLION", "BILLION", "TRILLION"];

    /// <summary>Spells <paramref name="number"/> in words.</summary>
    public static string Spell(long number)
    {
        if (number < 1000)
        {
            return SpellBelowThousand(number);
        }

        // Split into groups of three digits, lowest first: 1,234,005 becomes 5, 234, 1.
        var groups = new List<long>();
        for (var remaining = number; remaining > 0; remaining /= 1000)
        {
            groups.Add(remaining % 1000);
        }

        if (groups.Count > Scales.Length)
        {
            // The converter checks the range before it gets here (D58); reaching this is a bug in our code.
            throw new ArgumentOutOfRangeException(nameof(number), number, "The number is larger than the largest scale word.");
        }

        // Write the groups from the highest down, each followed by its scale word. Empty groups are skipped.
        var words = new List<string>();
        for (var position = groups.Count - 1; position >= 0; position--)
        {
            var group = groups[position];
            if (group == 0)
            {
                continue;
            }

            // AND goes before the final group when it is 1 to 99 and a larger group came first (D8):
            // 1,005 is ONE THOUSAND AND FIVE, but 1,100 is ONE THOUSAND ONE HUNDRED.
            if (position == 0 && group < 100)
            {
                words.Add("AND");
            }

            words.Add(SpellBelowThousand(group));
            if (position > 0)
            {
                words.Add(Scales[position]);
            }
        }

        return string.Join(' ', words);
    }

    private static string SpellBelowThousand(long number)
    {
        if (number < 100)
        {
            return SpellBelowHundred(number);
        }

        var hundreds = $"{BelowTwenty[number / 100]} HUNDRED";
        var rest = number % 100;

        // Inside a group of three digits, AND links the hundreds to whatever follows them (D8):
        // 123 is ONE HUNDRED AND TWENTY-THREE, but 200 is just TWO HUNDRED.
        return rest == 0 ? hundreds : $"{hundreds} AND {SpellBelowHundred(rest)}";
    }

    private static string SpellBelowHundred(long number)
    {
        if (number < 20)
        {
            return BelowTwenty[number];
        }

        var tens = Tens[number / 10];
        var units = number % 10;

        // 21 to 99 are written with a hyphen between the tens and the units: FORTY-FIVE.
        return units == 0 ? tens : $"{tens}-{BelowTwenty[units]}";
    }
}
