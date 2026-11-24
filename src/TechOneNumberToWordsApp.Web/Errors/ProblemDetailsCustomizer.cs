using Microsoft.AspNetCore.Http;
using TechOneNumberToWordsApp.Core;

namespace TechOneNumberToWordsApp.Web.Errors;

/// <summary>
/// Gives the errors the framework makes (empty-body 400, 404, 405, 415) and the unexpected 500 an <c>errorCode</c>
/// and a title, so every error body has the same shape (D24a, D41).
/// </summary>
public static class ProblemDetailsCustomizer
{
    public static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;

        // The endpoint's own errors already carry a code and a title; leave them alone (D41).
        if (problem.Extensions.ContainsKey(ApiProblem.ErrorCodeKey))
        {
            return;
        }

        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            // The same words every time and nothing from the exception; the traceId links the response to the log (D24).
            problem.Extensions[ApiProblem.ErrorCodeKey] = ApiErrorCode.ServerError.ToString();
            problem.Title = ApiProblem.ServerErrorTitle;
            problem.Detail = ApiProblem.ServerErrorDetail;
            return;
        }

        if (problem.Status == StatusCodes.Status413PayloadTooLarge)
        {
            // The one "Invalid request" error with a detail: the same words as AmountTooLong, since a body that large
            // is an amount that is too long (C1).
            problem.Extensions[ApiProblem.ErrorCodeKey] = ApiErrorCode.PayloadTooLarge.ToString();
            problem.Title = ApiProblem.InvalidRequestTitle;
            problem.Detail = AmountToWordsConverter.AmountTooLongMessage;
            return;
        }

        var code = problem.Status switch
        {
            StatusCodes.Status400BadRequest => ApiErrorCode.InvalidRequest,
            StatusCodes.Status404NotFound => ApiErrorCode.NotFound,
            StatusCodes.Status405MethodNotAllowed => ApiErrorCode.MethodNotAllowed,
            StatusCodes.Status415UnsupportedMediaType => ApiErrorCode.UnsupportedMediaType,
            _ => (ApiErrorCode?)null,
        };

        if (code is null)
        {
            return;
        }

        problem.Extensions[ApiProblem.ErrorCodeKey] = code.Value.ToString();
        problem.Title = ApiProblem.InvalidRequestTitle;
    }
}
