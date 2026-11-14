using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PaymentReconciliation.Api;
using PaymentReconciliation.Api.Observability;
using PaymentReconciliation.Api.Middlewares;
using PaymentReconciliation.Modules.Auditing;
using PaymentReconciliation.Modules.Auditing.Endpoints;
using PaymentReconciliation.Modules.Orders;
using PaymentReconciliation.Modules.Orders.Endpoints;
using PaymentReconciliation.Modules.Payments;
using PaymentReconciliation.Modules.Payments.Endpoints;
using PaymentReconciliation.Modules.Reconciliation;
using PaymentReconciliation.Modules.Reconciliation.Endpoints;
using PaymentReconciliation.Modules.Identity;
using PaymentReconciliation.Modules.Identity.Endpoints;
using PaymentReconciliation.Modules.Identity.Infrastructure;
using PaymentReconciliation.Modules.Reporting;
using PaymentReconciliation.Modules.Reporting.Endpoints;
using PaymentReconciliation.Modules.Support;
using PaymentReconciliation.Modules.Support.Endpoints;
using PaymentReconciliation.Modules.Tenancy;
using PaymentReconciliation.Modules.Tenancy.Endpoints;
using PaymentReconciliation.Modules.Tenancy.Infrastructure;
using PaymentReconciliation.Modules.Webhooks;
using PaymentReconciliation.Modules.Webhooks.Endpoints;
using PaymentReconciliation.Persistence;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "O";
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddLedgerlineObservability(builder.Configuration, builder.Environment);

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddCqrsPipeline();
builder.Services.AddCqrsHandlers(typeof(Program).Assembly);

builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddTenancyModule(builder.Configuration);
builder.Services.AddScoped<IClock, SystemClock>();

builder.Services.AddAuthorization(options => options.FallbackPolicy = TenancyAuthorization.TenantScoped);

builder.Services.AddSupportModule();
builder.Services.AddOrdersModule();
builder.Services.AddPaymentsModule();
builder.Services.AddWebhooksModule(builder.Configuration);
builder.Services.AddReconciliationModule();
builder.Services.AddAuditingModule();
builder.Services.AddReportingModule();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

builder.Services.AddCors(options => options.AddPolicy("web", policy => policy
    .WithOrigins("http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseCors("web");
app.UseAuthentication();
app.UseTenantResolution();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("Ledgerline API"));
}

app.MapOrderEndpoints();
app.MapPaymentEndpoints();
app.MapWebhookEndpoints();
app.MapReconciliationEndpoints();
app.MapAuditEndpoints();
app.MapDashboardEndpoints();
app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapTenantEndpoints();
app.MapInvitationEndpoints();
app.MapUsageEndpoints();
app.MapPlatformEndpoints();
app.MapSupportEndpoints();
app.MapPlanEndpoints();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthResponse
}).AllowAnonymous();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponse
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponse
}).AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    if (app.Configuration.GetValue("Database:SeedDemoData", true))
    {
        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
    }
}

await using (var planScope = app.Services.CreateAsyncScope())
{
    await planScope.ServiceProvider.GetRequiredService<PlanSeeder>().SeedAsync();
}

if (app.Environment.IsDevelopment() && app.Configuration.GetValue("Database:SeedDemoData", true))
{
    await using var demoScope = app.Services.CreateAsyncScope();
    await demoScope.ServiceProvider.GetRequiredService<DemoDataSeeder>().EnsureDemoSubscriptionAsync();
}

await app.RunAsync();

static Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    return context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(entry => new { name = entry.Key, status = entry.Value.Status.ToString() })
    });
}

public partial class Program;
