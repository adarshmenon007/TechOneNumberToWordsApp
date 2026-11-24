namespace TechOneNumberToWordsApp.Web.Errors;

/// <summary>
/// The error codes the web layer adds to the library's own (D24a). The member names are the text clients see in
/// <c>errorCode</c>, so renaming one is a breaking change (D24d).
/// </summary>
public enum ApiErrorCode
{
    /// <summary>The request body is missing or is not the JSON the endpoint expects (D24e, E5).</summary>
    InvalidRequest,

    /// <summary>The JSON has no <c>amount</c>, or it is null (D24e).</summary>
    MissingField,

    /// <summary>The request was not sent as JSON (D24c).</summary>
    UnsupportedMediaType,

    /// <summary>No endpoint has that address (D41).</summary>
    NotFound,

    /// <summary>The address exists but not for that HTTP method (D41).</summary>
    MethodNotAllowed,
}
