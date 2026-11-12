using Microsoft.AspNetCore.Http;
using PaymentReconciliation.SharedKernel.Results;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace PaymentReconciliation.SharedKernel.Web;

public static class ResultHttpExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result)
        => result.IsSuccess ? HttpResults.Ok(result.Value) : result.Error!.ToProblemResult();

    public static IResult ToHttpResult(this Result result)
        => result.IsSuccess ? HttpResults.NoContent() : result.Error!.ToProblemResult();

    public static IResult ToProblemResult<T>(this Result<T> result)
        => result.Error!.ToProblemResult();

    public static IResult ToProblemResult(this Result result)
        => result.Error!.ToProblemResult();

    public static IResult ToProblemResult(this Error error)
    {
        if (error.Type == ErrorType.Validation)
        {
            return HttpResults.ValidationProblem(
                error.Failures ?? new Dictionary<string, string[]>(),
                title: "Validation failed",
                detail: error.Message,
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = error.Code });
        }

        var statusCode = error.Type switch
        {
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unprocessable => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError
        };

        return HttpResults.Problem(
            title: error.Type.ToString(),
            detail: error.Message,
            statusCode: statusCode,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
