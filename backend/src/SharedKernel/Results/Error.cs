namespace PaymentReconciliation.SharedKernel.Results;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    Unprocessable,
    Unexpected
}

public sealed record Error(
    string Code,
    string Message,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? Failures = null)
{
    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? failures = null)
        => new(code, message, ErrorType.Validation, failures);

    public static Error NotFound(string code, string message)
        => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message)
        => new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message)
        => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message)
        => new(code, message, ErrorType.Forbidden);

    public static Error Unprocessable(string code, string message)
        => new(code, message, ErrorType.Unprocessable);

    public static Error Unexpected(string code, string message)
        => new(code, message, ErrorType.Unexpected);
}
