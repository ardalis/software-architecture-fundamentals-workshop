namespace TicketTide.EndToEndTests;

/// <summary>Reads the emails Papercut caught, through its HTTP API.</summary>
public sealed class PapercutClient(HttpClient http)
{
    /// <param name="to">The recipient.</param>
    /// <param name="subjectContains">Only match a message whose subject contains this, ignoring case.</param>
    /// <param name="timeout">How long to wait. The default is 30 seconds.</param>
    public async Task<EmailMessage> WaitForMessageAsync(string to, string? subjectContains = null, TimeSpan? timeout = null)
    {
        var ct = TestContext.Current.CancellationToken;
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        var checkedIds = new HashSet<string>();

        while (DateTimeOffset.UtcNow < deadline)
        {
            var list = await http.GetFromJsonAsync<MessageList>("/api/messages?limit=500", ct);
            var candidates = list!.Messages
                .Where(m => subjectContains is null || m.Subject.Contains(subjectContains, StringComparison.OrdinalIgnoreCase))
                .Where(m => checkedIds.Add(m.Id));
            foreach (var summary in candidates)
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
