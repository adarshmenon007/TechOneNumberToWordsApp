namespace TechOneNumberToWordsApp.Core;

/// <summary>
/// Reads the amount the user typed. Hand-written on purpose (D11): no <c>decimal.Parse</c>, no floating
/// point, no culture settings, so the result cannot depend on the machine it runs on.
/// </summary>
/// <remarks>
/// Built test-first. For now it reads a whole number made only of the digits '0' to '9'.
/// Cents, separators and the rejection rules are added when their tests are written.
/// </remarks>
internal static class AmountParser
{
    /// <summary>Turns a string of digits into the number of whole dollars it spells.</summary>
    public static long ParseDollars(string input)
    {
        if (input.Length == 0)
        {
            throw new NotImplementedException("Empty input is not handled yet; rejections arrive test-first.");
        }

        long dollars = 0;
        foreach (var character in input)
        {
            // Compare against '0' and '9' explicitly: char.IsDigit also accepts digits from other scripts (D10).
            if (character < '0' || character > '9')
            {
                throw new NotImplementedException("Only the digits 0-9 are supported so far; more rules arrive test-first.");
            }

            // Shift the digits read so far one place left and add the new one: "45" is 4 * 10 + 5.
            // checked makes a number too big for a long fail loudly instead of wrapping to a wrong value.
            dollars = checked((dollars * 10) + (character - '0'));
        }

        return dollars;
    }
}
