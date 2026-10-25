using Ardalis.Result;

namespace TicketTide.Web;

internal static class ResultExtensions
{
    /// <summary>
    /// Turns a failed <see cref="Result"/> into an HTTP response: NotFound is 404, Invalid (a broken business rule)
    /// is 400, Conflict is 409, and anything else is 500. The body is RFC 9457 problem details.
    /// </summary>
    public static Microsoft.AspNetCore.Http.IResult ToFailureResponse(this Ardalis.Result.IResult result)
    {
        var status = result.Status switch
        {
            ResultStatus.NotFound => StatusCodes.Status404NotFound,
            ResultStatus.Invalid => StatusCodes.Status400BadRequest,
            ResultStatus.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        var messages = result.ValidationErrors.Select(e => e.ErrorMessage).Concat(result.Errors).ToList();
        if (messages.Count == 0 && status == StatusCodes.Status404NotFound)
        {
            return TypedResults.NotFound(); // Like GET /orders/{orderId}: an unknown order is just 404.
        }

        return TypedResults.Problem(statusCode: status, detail: messages.Count > 0 ? string.Join(" ", messages) : null);
    }
}
