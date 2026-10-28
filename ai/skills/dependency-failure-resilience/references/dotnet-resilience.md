# .NET Resilience Reference

Package: `Microsoft.Extensions.Http.Resilience` (Polly v8 underneath). For non-HTTP calls use `Microsoft.Extensions.Resilience` / `Polly.Core` `ResiliencePipeline`.

## Standard handler (start here)

```csharp
builder.Services
    .AddHttpClient<IPaymentGateway, StripePaymentGateway>(c =>
        c.BaseAddress = new Uri(builder.Configuration["Payments:BaseUrl"]!))
    .AddStandardResilienceHandler(o =>
    {
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
        o.Retry.MaxRetryAttempts = 2;          // exponential backoff + jitter by default
        o.CircuitBreaker.FailureRatio = 0.5;
        o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    });
```

The standard pipeline is: rate limiter (bulkhead) > total timeout > retry > circuit breaker > attempt timeout. Retries apply only to transient results (5xx, 408, 429, `HttpRequestException`). Callers inject `IPaymentGateway`, never the concrete client.

In Aspire, ServiceDefaults usually calls `ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler())`; override per client when a dependency needs different limits.

## Non-idempotent POSTs

Either send an idempotency key header the vendor honors, or disable retries for that client:

```csharp
o.Retry.ShouldHandle = args => ValueTask.FromResult(
    args.Outcome.Result?.RequestMessage?.Method == HttpMethod.Get
    && HttpClientResiliencePredicates.IsTransient(args.Outcome));
```

## Custom pipeline with fallback

```csharp
services.AddResiliencePipeline<string, SeatMap>("seatmap", b => b
    .AddFallback(new() { FallbackAction = _ => Outcome.FromResultAsValueTask(cache.LastKnown) })
    .AddTimeout(TimeSpan.FromSeconds(2)));
// inject ResiliencePipelineProvider<string>, then GetPipeline<SeatMap>("seatmap")
```

## Fault injection for tests

Polly v8 chaos strategies (`AddChaosFault`, `AddChaosLatency`, `AddChaosOutcome`) or a `DelegatingHandler` stub returning 503 or delaying past the timeout. Assert the fallback result and user-facing message, not just that no exception escaped.
