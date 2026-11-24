namespace TechOneNumberToWordsApp.Core;

/// <summary>
/// Reads the amount the user typed. Hand-written on purpose (D11): no <c>decimal.Parse</c>, no floating
/// point, no culture settings, so the result cannot depend on the machine it runs on.
/// </summary>
/// <remarks>
/// Built test-first. For now it reads digits with at most one decimal point and at most two decimal digits.
/// Separators and the rejection rules are added when their tests are written.
/// </remarks>
internal static class AmountParser
{
    /// <summary>Reads text such as "123.45", ".5" or "5." into dollars and cents.</summary>
    public static MoneyAmount Parse(string input)
    {
        long dollars = 0;
        var cents = 0;
        var seenPoint = false;
        var seenDigit = false;
        var decimalDigits = 0;

        foreach (var character in input)
        {
            if (character == '.' && !seenPoint)
            {
                seenPoint = true;
                continue;
            }

            // Compare against '0' and '9' explicitly: char.IsDigit also accepts digits from other scripts (D10).
            if (character < '0' || character > '9')
            {
                throw new NotImplementedException("Only the digits 0-9 and one decimal point are supported so far; more rules arrive test-first.");
            }

            seenDigit = true;
            var digit = character - '0';

            if (!seenPoint)
            {
                // Shift the digits read so far one place left and add the new one: "45" is 4 * 10 + 5.
                // checked makes a number too big for a long fail loudly instead of wrapping to a wrong value.
                dollars = checked((dollars * 10) + digit);
                continue;
            }

            // The first decimal digit is tens of cents (".5" is 50 cents), the second is single cents (".45" is 45).
            decimalDigits++;
            if (decimalDigits > 2)
            {
                throw new NotImplementedException("More than two decimal places are not supported so far; more rules arrive test-first.");
            }

            cents += decimalDigits == 1 ? digit * 10 : digit;
        }

        if (!seenDigit)
        {
            throw new NotImplementedException("Input without digits is not handled yet; rejections arrive test-first.");
        }

        return MoneyAmount.Create(dollars, cents);
    }
}
