namespace TechOneNumberToWordsApp.Core;

/// <summary>
/// Why an amount could not be converted. For now it carries only the message shown to the user;
/// the error code arrives with the first rejection test (D7, D27).
/// </summary>
/// <param name="Message">User-facing explanation of what is wrong with the input.</param>
public sealed record ConversionError(string Message);
