using TechOneNumberToWordsApp.Core;
using TechOneNumberToWordsApp.Web.Errors;
using TechOneNumberToWordsApp.Web.Models;

namespace TechOneNumberToWordsApp.Web.Endpoints;

/// <summary>
/// <c>POST /api/convert</c> (D16). It only checks that the request has an amount and maps the converter's result to
/// HTTP; every rule about the amount itself lives in the library (D23).
/// </summary>
public static class ConvertEndpoint
{
    public static IEndpointRouteBuilder MapConvertEndpoint(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/convert", Handle);
        return routes;
    }

    // The request is nullable on purpose: an empty body arrives as null, and this method answers it (D24e).
    private static IResult Handle(ConvertRequest? request, AmountToWordsConverter converter)
    {
        // Checked in the order of D27: the body, then the amount, then the converter.
        if (request is null)
        {
            return ApiProblem.BadRequest(ApiErrorCode.InvalidRequest);
        }

        if (request.Amount is null)
        {
            return ApiProblem.BadRequest(ApiErrorCode.MissingField);
        }

        var result = converter.Convert(request.Amount);

        return result.IsSuccess
            ? TypedResults.Ok(new ConvertResponse(result.Words))
            : ApiProblem.InvalidAmount(result.Error.Code.ToString(), result.Error.Message);
    }
}
