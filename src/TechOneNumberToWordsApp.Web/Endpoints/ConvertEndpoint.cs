using TechOneNumberToWordsApp.Core;
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

    private static IResult Handle(ConvertRequest request, AmountToWordsConverter converter)
    {
        // Temporary: a missing amount becomes a 400 MissingField in the next step, which starts with its tests (D24e).
        ArgumentNullException.ThrowIfNull(request.Amount);

        var result = converter.Convert(request.Amount);

        if (result.IsSuccess)
        {
            return TypedResults.Ok(new ConvertResponse(result.Words));
        }

        // The errorCode is the enum member's name as text, a public contract (D24d). The 400 keeps its own title (D41).
        return TypedResults.Problem(
            title: "Invalid amount",
            detail: result.Error.Message,
            statusCode: StatusCodes.Status400BadRequest,
            extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code.ToString() });
    }
}
