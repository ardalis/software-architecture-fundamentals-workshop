namespace TicketTide.EndToEndTests;

/// <summary>Reads the emails Papercut caught, through its HTTP API.</summary>
public sealed class PapercutClient(HttpClient http)
{
    public async Task<EmailMessage> WaitForMessageAsync(string to, TimeSpan? timeout = null)
    {
        var ct = TestContext.Current.CancellationToken;
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        var checkedIds = new HashSet<string>();

        while (DateTimeOffset.UtcNow < deadline)
        {
            var list = await http.GetFromJsonAsync<MessageList>("/api/messages?limit=500", ct);
            foreach (var summary in list!.Messages.Where(m => checkedIds.Add(m.Id)))
            {
                var message = await http.GetFromJsonAsync<MessageDetail>($"/api/messages/{Uri.EscapeDataString(summary.Id)}", ct);
                if (message!.To.Any(a => string.Equals(a.Address, to, StringComparison.OrdinalIgnoreCase)))
                {
                    return new EmailMessage(message.Subject, message.TextBody ?? string.Empty);
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
        }

        throw new TimeoutException($"No email to {to} arrived in Papercut.");
    }

    private sealed record MessageList(List<MessageSummary> Messages);

    private sealed record MessageSummary(string Id, string Subject);

    private sealed record MessageDetail(string Subject, List<Mailbox> To, string? TextBody);

    private sealed record Mailbox(string Address);
}

public sealed record EmailMessage(string Subject, string Body);
