namespace TechOneNumberToWordsApp.Web.Models;

/// <summary>
/// The body of <c>POST /api/convert</c> (D3, D16). The amount is JSON text, not a number, so commas and the
/// exact digits the user typed arrive unchanged. It can be null here because the JSON may leave it out (D24e).
/// </summary>
public sealed record ConvertRequest(string? Amount);
