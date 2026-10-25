using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using TicketTide.Ticketing.Data;

namespace TicketTide.Ticketing.Endpoints;

public sealed record TicketsRequest(Guid OrderId);

public sealed record TicketsResponse(Guid OrderId, string Status, int Attempts, IReadOnlyList<string> TicketCodes, string? LastError);

internal sealed class GetTicketsEndpoint(TicketingDbContext db) : Endpoint<TicketsRequest, TicketsResponse>
{
    public override void Configure()
    {
        Get("/tickets/{orderId}");
        AllowAnonymous();
        Summary(s => s.Summary = "The tickets issued for an order, and how many attempts it took.");
    }

    public override async Task HandleAsync(TicketsRequest req, CancellationToken ct)
    {
        var issue = await db.TicketIssues.AsNoTracking().FirstOrDefaultAsync(t => t.OrderId == req.OrderId, ct);
        if (issue is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(new TicketsResponse(issue.OrderId, issue.Status.ToString(), issue.Attempts, issue.TicketCodes, issue.LastError), ct);
    }
}
