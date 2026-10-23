using Azma.Common.Results;
using Microsoft.AspNetCore.Http;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Azma.Common.Http;

public static class ResultExtensions
{
    public static IResult ToHttp(this Result result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess
            ? HttpResults.StatusCode(successStatusCode)
            : Problem(result.Error);

    public static IResult ToHttp<T>(this Result<T> result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess
            ? HttpResults.Json(result.Value, statusCode: successStatusCode)
            : Problem(result.Error);

    public static IResult ToCreated<T>(this Result<T> result, string location) =>
        result.IsSuccess ? HttpResults.Created(location, result.Value) : Problem(result.Error);

    private static IResult Problem(Error error) =>
        HttpResults.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: error.Type switch
            {
                ErrorType.NotFound     => StatusCodes.Status404NotFound,
                ErrorType.Validation   => StatusCodes.Status400BadRequest,
                ErrorType.Conflict     => StatusCodes.Status409Conflict,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.Forbidden    => StatusCodes.Status403Forbidden,
                _                      => StatusCodes.Status500InternalServerError,
            },
            type: $"https://azma.local/errors/{error.Code}");
}
