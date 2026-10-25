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
        orchestrator.StepRejectedAsync(message.OrderId, $"Inventory could not hold the seats: {message.Reason}", ct);

    public Task HandleAsync(SeatsReleased message, MessageContext context, CancellationToken ct) =>
        orchestrator.CompensatedAsync(message.OrderId, "Inventory released the seats.", ct);

    public Task HandleAsync(CardCharged message, MessageContext context, CancellationToken ct) =>
        orchestrator.CardChargedAsync(message.OrderId, ct);

    public Task HandleAsync(CardDeclined message, MessageContext context, CancellationToken ct) =>
        orchestrator.StepRejectedAsync(message.OrderId, $"Payments could not charge the card: {message.Reason}", ct);

    public Task HandleAsync(CardRefunded message, MessageContext context, CancellationToken ct) =>
        orchestrator.CompensatedAsync(message.OrderId, "Payments refunded the card (if it was charged).", ct);

    public Task HandleAsync(TicketsIssued message, MessageContext context, CancellationToken ct) =>
        orchestrator.TicketsIssuedAsync(message.OrderId, message.TicketCodes, ct);

    public Task HandleAsync(TicketingFailed message, MessageContext context, CancellationToken ct) =>
        orchestrator.TicketingFailedAsync(message.OrderId, message.Reason, ct);

    public Task HandleAsync(TicketsVoided message, MessageContext context, CancellationToken ct) =>
        orchestrator.CompensatedAsync(message.OrderId, "Ticketing voided the tickets (if any were issued).", ct);
}
