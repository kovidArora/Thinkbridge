namespace OrderSystem.Application.Common;

public enum ResultErrorType
{
    None,
    Validation,
    NotFound,
    Conflict,
    PaymentDeclined
}

/// <summary>
/// Typed outcome of an application-layer operation. Replaces the anonymous
/// `new { success = false, message = "..." }` objects from the original
/// controller, and carries enough information (ErrorType) for the
/// controller to map failures onto the correct HTTP status code.
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public ResultErrorType ErrorType { get; }

    private Result(bool isSuccess, T? value, string? error, ResultErrorType errorType)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        ErrorType = errorType;
    }

    public static Result<T> Success(T value) => new(true, value, null, ResultErrorType.None);

    public static Result<T> Failure(string error, ResultErrorType errorType) =>
        new(false, default, error, errorType);
}
