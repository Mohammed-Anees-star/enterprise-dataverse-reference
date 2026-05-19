using FluentValidation.Results;

namespace EnterpriseTicketing.Application.Common.Exceptions;

/// <summary>
/// Thrown by the ValidationBehavior pipeline when FluentValidation finds errors.
/// Maps to HTTP 422 Unprocessable Entity at the API boundary.
/// Contains a structured dictionary of field-level errors for rich client feedback.
/// </summary>
public sealed class ValidationException : ApplicationException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException()
        : base("One or more validation failures have occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IEnumerable<ValidationFailure> failures)
        : this()
    {
        Errors = failures
            .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
            .ToDictionary(failureGroup => failureGroup.Key, failureGroup => failureGroup.ToArray());
    }
}
