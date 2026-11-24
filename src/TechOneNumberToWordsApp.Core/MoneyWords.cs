namespace TechOneNumberToWordsApp.Core;

/// <summary>
/// Writes a dollars-and-cents amount in words, choosing the unit names and the AND between them (D58, D8).
/// </summary>
internal static class MoneyWords
{
    /// <summary>For example 123.45 becomes "ONE HUNDRED AND TWENTY-THREE DOLLARS AND FORTY-FIVE CENTS".</summary>
    public static string Spell(MoneyAmount amount)
    {
        // Zero cents are left out: 1.00 is ONE DOLLAR, and 0.00 is ZERO DOLLARS.
        if (amount.MinorUnits == 0)
        {
            return WithUnit(amount.MajorUnits, "DOLLAR", "DOLLARS");
        }

        var cents = WithUnit(amount.MinorUnits, "CENT", "CENTS");

        // With no whole dollars the cents stand alone: 0.45 is FORTY-FIVE CENTS.
        // Otherwise AND always joins dollars and cents (D8).
        return amount.MajorUnits == 0
            ? cents
            : $"{WithUnit(amount.MajorUnits, "DOLLAR", "DOLLARS")} AND {cents}";
    }

    // Exactly one takes the singular unit; every other count, including zero, takes the plural (D58).
    private static string WithUnit(long count, string singular, string plural) =>
        $"{NumberWords.Spell(count)} {(count == 1 ? singular : plural)}";
}
