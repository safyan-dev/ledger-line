using System.Reflection;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PaymentReconciliation.SharedKernel.Diagnostics;

namespace PaymentReconciliation.Api.Observability;

public static class ObservabilityRegistration
{
    public static IServiceCollection AddLedgerlineObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var endpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                       ?? configuration["Observability:OtlpEndpoint"]
                       ?? "http://localhost:4317";
        var serviceName = configuration["OTEL_SERVICE_NAME"]
                          ?? configuration["Observability:ServiceName"]
                          ?? "ledgerline-api";
        var serviceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = environment.EnvironmentName
                }))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/health")
                        && !context.Request.Path.StartsWithSegments("/scalar")
                        && !context.Request.Path.StartsWithSegments("/openapi");
                })
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddSource(LedgerlineMetrics.ActivitySourceName)
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(endpoint);
                    options.Protocol = OtlpExportProtocol.Grpc;
                }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddNpgsqlInstrumentation(_ => { })
                .AddMeter(LedgerlineMetrics.MeterName)
                .AddView(
                    "ledgerline.webhooks.processing.duration",
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = [0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10]
                    })
                .AddView(
                    "ledgerline.reconciliation.processing.duration",
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = [0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10]
                    })
                .AddOtlpExporter((exporterOptions, readerOptions) =>
                {
                    exporterOptions.Endpoint = new Uri(endpoint);
                    exporterOptions.Protocol = OtlpExportProtocol.Grpc;
                    readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 10_000;
                    readerOptions.PeriodicExportingMetricReaderOptions.ExportTimeoutMilliseconds = 5_000;
                }));

        return services;
    }
}
