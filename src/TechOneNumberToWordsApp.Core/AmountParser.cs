namespace TechOneNumberToWordsApp.Core;

/// <summary>
/// Reads the amount the user typed. Hand-written on purpose (D11): no <c>decimal.Parse</c>, no floating
/// point, no culture settings, so the result cannot depend on the machine it runs on.
/// </summary>
/// <remarks>
/// The input must already have passed <see cref="AmountValidator"/>, so this class does no checking of its own:
/// it holds only the digits 0-9, correctly placed commas, at most one decimal point with at most two digits
/// after it, and at least one digit.
/// </remarks>
internal static class AmountParser
{
    /// <summary>Reads text such as "1,234.50", ".5" or "5." into dollars and cents.</summary>
    public static MoneyAmount Parse(string input)
    {
        long dollars = 0;
        var cents = 0;
        var seenPoint = false;
        var decimalDigits = 0;

        foreach (var character in input)
        {
            if (character == ',')
            {
                // Thousands commas carry no value; their placement was checked by the validator (E3).
                continue;
            }

            if (character == '.')
            {
                seenPoint = true;
                continue;
            }

            var digit = character - '0';

            if (!seenPoint)
            {
                // Shift the digits read so far one place left and add the new one: "45" is 4 * 10 + 5.
                // The validator limits the number to 15 significant digits, so this cannot overflow a long.
                dollars = (dollars * 10) + digit;
                continue;
            }

            // The first decimal digit is tens of cents (".5" is 50 cents), the second is single cents (".45" is 45).
            decimalDigits++;
            cents += decimalDigits == 1 ? digit * 10 : digit;
        }

        return MoneyAmount.Create(dollars, cents);
    }
}
