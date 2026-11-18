using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PaymentReconciliation.Modules.Payments.Contracts;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Web;

namespace PaymentReconciliation.Modules.Reporting.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports").WithTags("Reports");

        group.MapGet("/export/payments", async (
            IPaymentsFacade paymentsFacade,
            IFeatureGate featureGate,
            CancellationToken cancellationToken) =>
        {
            var feature = await featureGate.EnsureEnabledAsync("csv_export", cancellationToken);
            if (feature.IsFailure)
            {
                return feature.Error!.ToProblemResult();
            }

            var payments = await paymentsFacade.GetPaymentsForExportAsync(10_000, cancellationToken);

            var csv = new StringBuilder();
            csv.AppendLine("payment_id,provider,provider_payment_id,order_id,amount,currency,status,received_at");

            foreach (var payment in payments)
            {
                csv.Append(Csv(payment.Id.ToString()));
                csv.Append(',').Append(Csv(payment.Provider));
                csv.Append(',').Append(Csv(payment.ProviderPaymentId));
                csv.Append(',').Append(Csv(payment.OrderId?.ToString()));
                csv.Append(',').Append(payment.Amount.ToString(CultureInfo.InvariantCulture));
                csv.Append(',').Append(Csv(payment.Currency));
                csv.Append(',').Append(Csv(payment.Status.ToString()));
                csv.Append(',').Append(Csv(payment.ReceivedAt.ToString("O", CultureInfo.InvariantCulture)));
                csv.AppendLine();
            }

            var fileName = $"payments-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv";
            return Results.File(
                Encoding.UTF8.GetBytes(csv.ToString()),
                "text/csv",
                fileName);
        })
        .RequireAuthorization(AuthorizationPolicies.TenantMember)
        .WithName("ExportPayments")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static string Csv(string? value)
        => $"\"{(value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
