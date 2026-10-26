using FluentValidation;
using Mediator;
using TicketTide.EmailSending.Contracts;

namespace TicketTide.EmailSending.Internal.Integrations;

public sealed class SendEmailHandler(IEmailSender sender) : ICommandHandler<SendEmail>
{
    public async ValueTask<Unit> Handle(SendEmail command, CancellationToken cancellationToken)
    {
        await sender.SendAsync(command.To, command.Subject, command.Body, cancellationToken);
        return Unit.Value;
    }
}

internal sealed class SendEmailValidator : AbstractValidator<SendEmail>
{
    public SendEmailValidator()
    {
        RuleFor(x => x.To).NotEmpty().EmailAddress();
        RuleFor(x => x.Subject).NotEmpty();
    }
}
