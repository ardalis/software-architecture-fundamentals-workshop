using TicketTide.Checkout.Data;
using TicketTide.Inventory.Contracts;
using TicketTide.Messaging;
using TicketTide.Payments.Contracts;
using TicketTide.Ticketing.Contracts;

namespace TicketTide.Checkout.Saga;

/// <summary>Every reply from the other modules arrives on the checkout queue and is passed to the orchestrator.</summary>
internal sealed class ReplyHandlers(CheckoutOrchestrator orchestrator) :
    IMessageHandler<SeatsHeld>,
    IMessageHandler<SeatsRejected>,
    IMessageHandler<SeatsReleased>,
    IMessageHandler<CardCharged>,
    IMessageHandler<CardDeclined>,
    IMessageHandler<CardRefunded>,
    IMessageHandler<TicketsIssued>,
    IMessageHandler<TicketingFailed>,
    IMessageHandler<TicketsVoided>
{
    public Task HandleAsync(SeatsHeld message, MessageContext context, CancellationToken ct) =>
        orchestrator.SeatsHeldAsync(message.OrderId, ct);

    public Task HandleAsync(SeatsRejected message, MessageContext context, CancellationToken ct) =>
        orchestrator.StepFailedAsync(message.OrderId, SagaStep.HoldSeats,
            $"Inventory could not hold the seats: {message.Reason}", ct);

    public Task HandleAsync(SeatsReleased message, MessageContext context, CancellationToken ct) =>
        orchestrator.CompensatedAsync(message.OrderId, SagaStep.HoldSeats, "Inventory released the seats.", ct);

    public Task HandleAsync(CardCharged message, MessageContext context, CancellationToken ct) =>
        orchestrator.CardChargedAsync(message.OrderId, ct);

    public Task HandleAsync(CardDeclined message, MessageContext context, CancellationToken ct) =>
        orchestrator.StepFailedAsync(message.OrderId, SagaStep.ChargeCard,
            $"Payments could not charge the card: {message.Reason}", ct);

    public Task HandleAsync(CardRefunded message, MessageContext context, CancellationToken ct) =>
        orchestrator.CompensatedAsync(message.OrderId, SagaStep.ChargeCard, "Payments refunded the card.", ct);

    public Task HandleAsync(TicketsIssued message, MessageContext context, CancellationToken ct) =>
        orchestrator.TicketsIssuedAsync(message.OrderId, message.TicketCodes, ct);

    public Task HandleAsync(TicketingFailed message, MessageContext context, CancellationToken ct) =>
        orchestrator.StepFailedAsync(message.OrderId, SagaStep.IssueTickets,
            $"Ticketing could not issue the tickets: {message.Reason}", ct);

    public Task HandleAsync(TicketsVoided message, MessageContext context, CancellationToken ct) =>
        orchestrator.CompensatedAsync(message.OrderId, SagaStep.IssueTickets, "Ticketing voided the tickets.", ct);
}
