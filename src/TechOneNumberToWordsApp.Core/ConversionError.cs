namespace TechOneNumberToWordsApp.Core;

/// <summary>Why an amount could not be converted (D7).</summary>
/// <param name="Code">Which rule was broken; stable, so callers and tests can rely on it.</param>
/// <param name="Message">User-facing explanation of what is wrong with the input.</param>
public sealed record ConversionError(ConversionErrorCode Code, string Message);
