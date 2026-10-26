using FluentValidation;
using Mediator;

namespace TicketTide.SharedKernel.Behaviors;

/// <summary>
/// Runs every FluentValidation validator registered for a message before its handler. A module's Contracts are its
/// public API, and any module can send them, so each module guards its own front door: a message that fails here
/// is a bug in the sender, and it throws.
/// </summary>
/// <remarks>
/// HTTP requests are validated by their FastEndpoints validators first, so a fan's bad request is a <c>400</c>,
/// not an exception.
/// </remarks>
public sealed class ValidationBehavior<TMessage, TResponse>(IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        foreach (var validator in validators)
        {
            await validator.ValidateAndThrowAsync(message, cancellationToken);
        }

        return await next(message, cancellationToken);
    }
}
