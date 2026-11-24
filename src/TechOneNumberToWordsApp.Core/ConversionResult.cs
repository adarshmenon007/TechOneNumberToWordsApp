using System.Diagnostics.CodeAnalysis;

namespace TechOneNumberToWordsApp.Core;

/// <summary>
/// The outcome of a conversion: either the words, or an error. Callers check <see cref="IsSuccess"/>
/// first; the compiler then knows <see cref="Words"/> is not null (D7).
/// </summary>
public sealed class ConversionResult
{
    private ConversionResult(string? words, ConversionError? error)
    {
        Words = words;
        Error = error;
    }

    /// <summary>True when the amount was converted to words.</summary>
    [MemberNotNullWhen(true, nameof(Words))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Words is not null;

    /// <summary>The amount in words, for example "ONE DOLLAR". Null when the conversion failed.</summary>
    public string? Words { get; }

    /// <summary>What was wrong with the input. Null when the conversion succeeded.</summary>
    public ConversionError? Error { get; }

    /// <summary>Creates a successful result. Only this library builds results; callers just read them (D7).</summary>
    internal static ConversionResult Success(string words) => new(words, null);

    /// <summary>Creates a failed result carrying the reason.</summary>
    internal static ConversionResult Failure(ConversionError error) => new(null, error);
}
