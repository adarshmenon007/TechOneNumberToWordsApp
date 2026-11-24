namespace TechOneNumberToWordsApp.Web.Errors;

/// <summary>
/// Builds the error responses the endpoint makes itself (D24, D24b). Every error body is ProblemDetails with an
/// <c>errorCode</c> extension that holds the code's name as text (D24d).
/// </summary>
public static class ApiProblem
{
    /// <summary>The title of errors about the amount the user typed (D24b).</summary>
    public const string InvalidAmountTitle = "Invalid amount";

    /// <summary>The title of errors about the request itself (D24b).</summary>
    public const string InvalidRequestTitle = "Invalid request";

    /// <summary>The name of the extension that carries the error code.</summary>
    public const string ErrorCodeKey = "errorCode";

    /// <summary>A 400 about the request, for example a missing amount. It carries no detail (D24 amendment).</summary>
    public static IResult BadRequest(ApiErrorCode code) =>
        TypedResults.Problem(
            title: InvalidRequestTitle,
            statusCode: StatusCodes.Status400BadRequest,
            extensions: ErrorCode(code.ToString()));

    /// <summary>A 400 about the amount: the library's error code and its message.</summary>
    public static IResult InvalidAmount(string errorCode, string message) =>
        TypedResults.Problem(
            title: InvalidAmountTitle,
            detail: message,
            statusCode: StatusCodes.Status400BadRequest,
            extensions: ErrorCode(errorCode));

    private static Dictionary<string, object?> ErrorCode(string code) => new() { [ErrorCodeKey] = code };
}
