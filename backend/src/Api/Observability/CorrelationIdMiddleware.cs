using System.Diagnostics;
using PaymentReconciliation.SharedKernel.Diagnostics;

namespace PaymentReconciliation.Api.Observability;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        var activity = Activity.Current;
        activity?.SetTag("correlation.id", correlationId);
        activity?.AddBaggage("correlation.id", correlationId);

        using var correlationScope = CorrelationContext.Push(correlationId);
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = activity?.TraceId.ToString(),
            ["RequestMethod"] = context.Request.Method,
            ["RequestPath"] = context.Request.Path.Value
        });

        var startedAt = Stopwatch.GetTimestamp();
        logger.LogInformation(
            "HTTP {Method} {Path} started.",
            context.Request.Method,
            context.Request.Path.Value);

        try
        {
            await next(context);

            logger.LogInformation(
                "HTTP {Method} {Path} completed with {StatusCode} in {ElapsedMs:0.0} ms.",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "HTTP {Method} {Path} failed after {ElapsedMs:0.0} ms.",
                context.Request.Method,
                context.Request.Path.Value,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            throw;
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var supplied = context.Request.Headers["X-Correlation-ID"].ToString();

        if (!string.IsNullOrWhiteSpace(supplied)
            && supplied.Length <= 128
            && supplied.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.'))
        {
            return supplied;
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }
}
