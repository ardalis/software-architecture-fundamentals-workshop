using System.Collections.Concurrent;

namespace TicketTide.Checkout.Saga;

/// <summary>
/// Lets <c>POST /orders</c> wait for its saga. The fan's request is held open while the replies arrive on the
/// checkout queue and are handled elsewhere in the process. The orchestrator registers the order before it sends
/// anything, and completes it when the order is <c>Confirmed</c> or <c>Failed</c>.
/// </summary>
/// <remarks>
/// This only works because the API is a single process: the reply is always handled where the fan is waiting.
/// With several API instances, a different instance could handle the reply. You would need a reply queue
/// per instance, or the waiting request would have to poll the database.
/// </remarks>
internal sealed class OrderCompletionSignal
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _waiting = new();

    public void Register(Guid orderId) =>
        // Run the waiting request's continuation on its own thread, not on the message consumer's.
        _waiting[orderId] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Complete(Guid orderId)
    {
        if (_waiting.TryGetValue(orderId, out var completion))
        {
            completion.TrySetResult();
        }
    }

    /// <summary>Waits for <see cref="Complete"/>. Returns false if the order did not finish in time.</summary>
    public async Task<bool> WaitAsync(Guid orderId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!_waiting.TryGetValue(orderId, out var completion))
        {
            return false;
        }

        try
        {
            await completion.Task.WaitAsync(timeout, cancellationToken);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        finally
        {
            _waiting.TryRemove(orderId, out _);
        }
    }

    /// <summary>Stops waiting for an order, for example because starting its saga failed.</summary>
    public void Forget(Guid orderId) => _waiting.TryRemove(orderId, out _);
}
