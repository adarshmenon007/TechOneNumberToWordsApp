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
    private static IResult Handle(ConvertRequest? request, AmountToWordsConverter converter, ILoggerFactory loggerFactory)
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

        if (result.IsSuccess)
        {
            return TypedResults.Ok(new ConvertResponse(result.Words));
        }

        // A typo is not a system problem, so Information. Only the code is logged, never what the user typed (D33).
        // The endpoint is a static class, which cannot be an ILogger type argument, so its category comes from the type.
        var errorCode = result.Error.Code.ToString();
        var logger = loggerFactory.CreateLogger(typeof(ConvertEndpoint));
        logger.LogInformation("Conversion rejected: {ErrorCode}", errorCode);

        return ApiProblem.InvalidAmount(errorCode, result.Error.Message);
    }
}
