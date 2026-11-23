namespace TechOneNumberToWordsApp.Tests.TestData;

/// <summary>
/// One row of <c>conversion-cases.json</c> (D43). A row has either <see cref="ExpectedWords"/>
/// (a successful conversion) or <see cref="ExpectedErrorCode"/> (a rejected input), never both.
/// </summary>
internal sealed record ConversionCase
{
    /// <summary>Stable test ID: category prefix and number, e.g. "AND-003" (D47).</summary>
    public required string Id { get; init; }

    /// <summary>The design decisions this row exercises, e.g. "D8" (D47).</summary>
    public required string[] Decisions { get; init; }

    public required string Description { get; init; }

    /// <summary>The raw amount exactly as a user would type it, including any odd characters.</summary>
    public required string Input { get; init; }

    public string? ExpectedWords { get; init; }

    public string? ExpectedErrorCode { get; init; }

    /// <summary>Optional text the error message must contain, e.g. "(U+00A0)".</summary>
    public string? MessageContains { get; init; }
}
