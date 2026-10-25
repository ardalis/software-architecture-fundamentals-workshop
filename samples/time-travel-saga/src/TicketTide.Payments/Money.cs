using System.Globalization;

namespace TicketTide.Payments;

internal static class Money
{
    // TicketTide sells in US dollars. Format the same way whatever the server's culture is.
    public static string Format(decimal amount) => amount.ToString("$#,##0.00", CultureInfo.InvariantCulture);
}
