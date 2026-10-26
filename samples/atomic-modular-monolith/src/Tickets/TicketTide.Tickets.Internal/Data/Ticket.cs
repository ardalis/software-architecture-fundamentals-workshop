using System.Security.Cryptography;

namespace TicketTide.Tickets.Internal.Data;

public enum TicketStatus
{
    Paid,
    Refunded,
}

/// <summary>One seat's ticket.</summary>
public sealed class Ticket
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>The order it was bought in. An id only: the order belongs to the Orders module.</summary>
    public Guid OrderId { get; init; }

    /// <summary>1, 2, 3, ... within the order, so its tickets always list in the order they were issued.</summary>
    public int Number { get; init; }

    /// <summary>The code in the ticket's QR code. Door staff scan it at the entrance.</summary>
    public string Code { get; init; } = NewCode();

    public required string HolderEmail { get; init; }

    public TicketStatus Status { get; set; } = TicketStatus.Paid;

    private static string NewCode() => $"TT-{Convert.ToHexString(RandomNumberGenerator.GetBytes(5))}";
}
