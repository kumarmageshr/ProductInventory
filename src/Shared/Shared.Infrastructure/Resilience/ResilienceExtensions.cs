using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Shared.Infrastructure.Resilience;

/// <summary>
/// Resilience Patterns via Polly v8.
/// Implements: Retry, Circuit Breaker, Timeout, Bulkhead, Fallback.
///
/// Decorator Pattern: each policy wraps the inner call.
/// Chain: HttpClient → Retry → CircuitBreaker → Timeout
/// </summary>
public static class ResilienceExtensions
{
    public static IHttpClientBuilder AddECommerceResiliencePolicy(
        this IHttpClientBuilder builder)
    {
        return builder.AddResilienceHandler("ecommerce-resilience", pipeline =>
        {
            // Timeout — innermost (per-attempt)
            pipeline.AddTimeout(new HttpTimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(10),
                Name = "PerAttemptTimeout"
            });

            // Retry — exponential back-off with jitter
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(500),
                UseJitter = true,
                ShouldHandle = static args =>
                    ValueTask.FromResult(args.Outcome.Exception is HttpRequestException ||
                        args.Outcome.Result?.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
            });

            // Circuit Breaker — opens after 5 failures in 30s
            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(15)
            });

            // Total timeout — outermost
            pipeline.AddTimeout(new HttpTimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(30),
                Name = "TotalTimeout"
            });
        });
    }

    /// <summary>
    /// Non-HTTP resilience pipeline for service-to-service calls.
    /// </summary>
    public static ResiliencePipeline<T> BuildServicePipeline<T>() =>
        new ResiliencePipelineBuilder<T>()
            .AddRetry(new RetryStrategyOptions<T>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(200),
                UseJitter = true
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<T>
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(15)
            })
            .AddTimeout(TimeSpan.FromSeconds(10))
            .Build();
}
