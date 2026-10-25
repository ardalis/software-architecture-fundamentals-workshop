using System.Collections.Concurrent;

namespace TicketTide.Checkout.Saga;

/// <summary>
/// Lets <c>POST /orders</c> wait for an asynchronous saga to finish. The endpoint registers the order before
/// it publishes Order Placed; the event handler that moves the order to a final state completes it.
/// <para>
/// It lives in memory, so it only works because every module runs in this one process. With several API
/// instances, the event that finishes the order could arrive at a different instance than the waiting fan.
/// </para>
/// </summary>
internal sealed class OrderCompletionSignal
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _waiting = new();

    public void Register(Guid orderId) =>
        _waiting.TryAdd(orderId, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

    /// <summary>The order reached Confirmed or Failed. Does nothing if nobody is waiting.</summary>
    public void Complete(Guid orderId)
    {
        if (_waiting.TryGetValue(orderId, out var completion))
        {
            completion.TrySetResult();
        }
    }

    /// <summary>Returns <see langword="true"/> if the order finished within <paramref name="timeout"/>.</summary>
    public async Task<bool> WaitAsync(Guid orderId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!_waiting.TryGetValue(orderId, out var completion))
        {
            throw new InvalidOperationException($"Order {orderId} was not registered.");
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
