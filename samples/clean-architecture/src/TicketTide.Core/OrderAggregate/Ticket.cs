using System.Security.Cryptography;

namespace TicketTide.Core.OrderAggregate;

/// <summary>One seat's ticket. Part of the <see cref="Order"/> aggregate: change it only through its order.</summary>
public sealed class Ticket
{
    // For EF Core.
    private Ticket()
    {
    }

    internal Ticket(string holderEmail)
    {
        HolderEmail = holderEmail;
        Code = NewCode();
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>The code in the ticket's QR code. Door staff scan it at the entrance.</summary>
    public string Code { get; private set; } = string.Empty;

    public string HolderEmail { get; private set; } = string.Empty;

    public TicketStatus Status { get; private set; } = TicketStatus.Paid;

    internal void Refund() => Status = TicketStatus.Refunded;

    private static string NewCode() => $"TT-{Convert.ToHexString(RandomNumberGenerator.GetBytes(5))}";
}
