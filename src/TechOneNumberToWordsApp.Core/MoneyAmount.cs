namespace TechOneNumberToWordsApp.Core;

/// <summary>
/// An amount of Australian money split into whole dollars and cents, both held as whole numbers
/// so no floating point is involved (D11).
/// </summary>
/// <remarks>
/// A class, not a struct: every struct has a built-in "default" value that skips the checks in
/// <see cref="Create"/>, so an invalid amount could be made without going through them.
/// </remarks>
internal sealed class MoneyAmount
{
    private MoneyAmount(long majorUnits, int minorUnits)
    {
        MajorUnits = majorUnits;
        MinorUnits = minorUnits;
    }

    /// <summary>Whole dollars.</summary>
    public long MajorUnits { get; }

    /// <summary>Cents, from 0 to 99.</summary>
    public int MinorUnits { get; }

    /// <summary>
    /// Creates an amount. The validator rejects bad user input before it gets here, so a value out of range
    /// means a bug in our own code; that is why this throws instead of returning a conversion error.
    /// </summary>
    public static MoneyAmount Create(long majorUnits, int minorUnits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(majorUnits);
        ArgumentOutOfRangeException.ThrowIfNegative(minorUnits);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minorUnits, 99);

        return new MoneyAmount(majorUnits, minorUnits);
    }
}
