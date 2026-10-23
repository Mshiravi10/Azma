using Azma.Common.Results;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Azma.Common.Application.Behaviors;

/// <summary>
/// Runs all FluentValidation validators registered for a request.
/// If any fail and the response type is <see cref="Result"/> or <see cref="Result{T}"/>,
/// the failure is returned without throwing. Otherwise <see cref="ValidationException"/> is thrown.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any()) return await next();

        var ctx = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(ctx, cancellationToken)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count == 0) return await next();

        return TryWrapAsResult(failures, out var wrapped)
            ? wrapped!
            : throw new ValidationException(failures);
    }

    private static bool TryWrapAsResult(List<ValidationFailure> failures, out TResponse? wrapped)
    {
        wrapped = default;
        var error = Error.Validation(
            "validation_failed",
            string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}")));

        var responseType = typeof(TResponse);

        if (responseType == typeof(Result))
        {
            wrapped = (TResponse)(object)Result.Failure(error);
            return true;
        }

        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var inner = responseType.GetGenericArguments()[0];
            var failureMethod = typeof(Result)
                .GetMethods()
                .First(m => m.Name == nameof(Result.Failure) && m.IsGenericMethod)
                .MakeGenericMethod(inner);
            wrapped = (TResponse)failureMethod.Invoke(null, new object[] { error })!;
            return true;
        }

        return false;
    }
}
