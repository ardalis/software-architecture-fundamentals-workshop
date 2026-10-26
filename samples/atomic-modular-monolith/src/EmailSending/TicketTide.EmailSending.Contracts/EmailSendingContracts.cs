using Mediator;

namespace TicketTide.EmailSending.Contracts;

/// <summary>
/// Sends a plain-text email. Best effort: a failure is logged, not thrown. Send it after your transaction commits,
/// because an email can't be rolled back.
/// </summary>
public sealed record SendEmail(string To, string Subject, string Body) : ICommand;
