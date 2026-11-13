using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Orders.Contracts;

public interface IOrdersFacade
{
    Task<Result<OrderAllocation>> AllocatePaymentAsync(
        Guid orderId,
        decimal amount,
        string currency,
        Guid paymentId,
        AuditActor actor,
        CancellationToken cancellationToken = default);

    Task<Result<OrderAllocation>> ReversePaymentAllocationAsync(
        Guid orderId,
        Guid paymentId,
        decimal amount,
        string currency,
        AuditActor actor,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<OrderCandidate>>> FindCandidatesByOrderNumberAsync(
        string orderNumber,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<OrderCandidate>>> FindCandidatesByCustomerAndAmountAsync(
        string customerReference,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<OrderCandidate>>> FindCandidatesByAmountAndDateAsync(
        decimal amount,
        string currency,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default);

    Task<Result<OrderCandidate>> GetCandidateAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> GetOrderNumbersAsync(
        IEnumerable<Guid> orderIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> FindOrderIdsByNumberAsync(
        string search,
        CancellationToken cancellationToken = default);
}
