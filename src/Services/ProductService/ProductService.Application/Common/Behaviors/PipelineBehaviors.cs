using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Domain.Primitives;

namespace ProductService.Application.Common.Behaviors;

/// <summary>
/// MediatR Pipeline Behavior — Validation.
/// Chain of Responsibility: validates request before handler executes.
/// Implements Open/Closed: new validations added by registering new validators.
/// </summary>
public sealed class ValidationPipelineBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationPipelineBehavior(IEnumerable<IValidator<TRequest>> validators) =>
        _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var validationErrors = _validators
            .Select(v => v.Validate(request))
            .Where(r => !r.IsValid)
            .SelectMany(r => r.Errors)
            .Select(f => new ValidationError(f.PropertyName, f.ErrorMessage))
            .Distinct()
            .ToArray();

        if (validationErrors.Length > 0)
        {
            // Create failure result via reflection to return typed Result<T>
            return CreateValidationResult<TResponse>(validationErrors);
        }

        return await next();
    }

    private static TResponse CreateValidationResult<T>(ValidationError[] errors)
    {
        var error = new Error(
            "Validation.Failed",
            string.Join("; ", errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));

        // Return appropriate Result type
        if (typeof(T) == typeof(Result))
            return (TResponse)(object)Result.Failure(error);

        var genericResultType = typeof(T).GetGenericArguments()[0];
        var method = typeof(Result)
            .GetMethod(nameof(Result.Failure))!
            .MakeGenericMethod(genericResultType);

        return (TResponse)method.Invoke(null, [error])!;
    }
}

public sealed record ValidationError(string PropertyName, string ErrorMessage);

/// <summary>
/// Logging Behavior — Decorator Pattern via MediatR pipeline.
/// Logs command/query name, execution time, and errors.
/// </summary>
public sealed class LoggingPipelineBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingPipelineBehavior<TRequest, TResponse>> _logger;

    public LoggingPipelineBehavior(ILogger<LoggingPipelineBehavior<TRequest, TResponse>> logger) =>
        _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var start = System.Diagnostics.Stopwatch.GetTimestamp();

        _logger.LogInformation("Handling {RequestName}", requestName);

        try
        {
            var response = await next();
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(start);
            _logger.LogInformation("Handled {RequestName} in {Elapsed}ms", requestName, elapsed.TotalMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling {RequestName}", requestName);
            throw;
        }
    }
}
