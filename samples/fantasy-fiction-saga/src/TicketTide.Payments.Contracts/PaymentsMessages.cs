namespace TicketTide.Payments.Contracts;

// Messages sent to Payments over RabbitMQ.

/// <summary>
/// Charge the fan. The payment token comes from the payment provider's checkout widget,
/// so card data never touches TicketTide. Use <c>tok_declined</c> to simulate a declined card.
/// Replies with <see cref="CardCharged"/> or <see cref="CardDeclined"/>.
/// </summary>
public sealed record ChargeCard(Guid OrderId, decimal Amount, string PaymentToken);

/// <summary>Compensating command: refund a charge. Replies with <see cref="CardRefunded"/>.</summary>
public sealed record RefundCard(Guid OrderId);

// Replies Payments publishes.

public sealed record CardCharged(Guid OrderId);

public sealed record CardDeclined(Guid OrderId, string Reason);

public sealed record CardRefunded(Guid OrderId);
