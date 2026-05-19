namespace EnterpriseTicketing.Application.Common.Models;

/// <summary>
/// Discriminated union representing success or failure from application operations.
/// Using Result instead of throwing exceptions for expected failures (e.g., not found, validation error)
/// improves performance (no stack unwind), makes failure handling explicit, and enables
/// functional composition in command handlers.
///
/// Use exceptions only for truly unexpected errors (infrastructure failures, bugs).
/// </summary>
public sealed class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; }
    public string? ErrorCode { get; }

    private Result(bool isSuccess, string? error = null, string? errorCode = null)
    {
        IsSuccess = isSuccess;
        Error = error;
        ErrorCode = errorCode;
    }

    public static Result Success() => new(true);
    public static Result Failure(string error, string? errorCode = null) => new(false, error, errorCode);
}

public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }

    private Result(bool isSuccess, T? value = default, string? error = null, string? errorCode = null)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        ErrorCode = errorCode;
    }

    public static Result<T> Success(T value) => new(true, value);
    public static Result<T> Failure(string error, string? errorCode = null) => new(false, error: error, errorCode: errorCode);

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, TResult> onFailure)
        => IsSuccess ? onSuccess(Value!) : onFailure(Error!);
}
