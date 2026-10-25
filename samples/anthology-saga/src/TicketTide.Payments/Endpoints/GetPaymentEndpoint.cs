using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using TicketTide.Payments.Data;

namespace TicketTide.Payments.Endpoints;

public sealed record PaymentRequest(Guid OrderId);

public sealed record PaymentResponse(Guid OrderId, decimal Amount, string Status, string? FailureReason);

internal sealed class GetPaymentEndpoint(PaymentsDbContext db) : Endpoint<PaymentRequest, PaymentResponse>
{
    public override void Configure()
    {
        Get("/payments/{orderId}");
        AllowAnonymous();
        Summary(s => s.Summary = "The payment for an order: Charged or Declined.");
    }

    public override async Task HandleAsync(PaymentRequest req, CancellationToken ct)
    {
        var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.OrderId == req.OrderId, ct);
        if (payment is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(new PaymentResponse(payment.OrderId, payment.Amount, payment.Status.ToString(), payment.FailureReason), ct);
    }
}
