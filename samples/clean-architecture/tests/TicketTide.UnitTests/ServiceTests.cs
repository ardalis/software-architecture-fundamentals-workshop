using Ardalis.Result;
using TicketTide.Core.EventAggregate;
using TicketTide.Core.Interfaces;
using TicketTide.Core.OrderAggregate;
using TicketTide.Core.Services;

namespace TicketTide.UnitTests;

/// <summary>
/// The use cases, with in-memory stand-ins for Infrastructure. Core depends only on its own interfaces,
/// so it can be tested without a database, an SMTP server, or a payment provider.
/// </summary>
public sealed class ServiceTests
{
    private readonly Event _backRoom = new("back-room-sessions", "Back Room Sessions", "The Back Room", DateTimeOffset.UtcNow, 30m, capacity: 4);
    private readonly InMemoryStore _store = new();
    private readonly FakePayments _payments = new();
    private readonly RecordingNotifier _notifier = new();

    public ServiceTests() => _store.Events.Add(_backRoom);

    private PlaceOrderService PlaceOrder => new(_store, _store, _store, _payments, _notifier);

    private RefundOrderService RefundOrder => new(_store, _store, _store, _payments, _notifier);

    [Fact]
    public async Task PlaceOrder_ChargesTheCard_SellsTheSeats_AndSendsTheTickets()
    {
        var result = await PlaceOrder.PlaceOrderAsync(new PlaceOrderCommand(_backRoom.Id, 2, "fan@example.com", "tok_visa"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Paid, result.Value.Status);
        Assert.Equal(2, _backRoom.SeatsAvailable);
        Assert.Equal(60m, Assert.Single(_payments.Charges));
        Assert.Same(result.Value, Assert.Single(_notifier.TicketsSent));
        Assert.Same(result.Value, Assert.Single(_store.Orders));
    }

    [Fact]
    public async Task PlaceOrder_UnknownEvent_IsNotFound()
    {
        var result = await PlaceOrder.PlaceOrderAsync(new PlaceOrderCommand("no-such-event", 1, "fan@example.com", "tok_visa"), TestContext.Current.CancellationToken);

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Empty(_payments.Charges);
    }

    [Fact]
    public async Task PlaceOrder_NotEnoughSeats_IsInvalid_AndNeverChargesTheCard()
    {
        var result = await PlaceOrder.PlaceOrderAsync(new PlaceOrderCommand(_backRoom.Id, 5, "fan@example.com", "tok_visa"), TestContext.Current.CancellationToken);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Empty(_payments.Charges);
        Assert.Empty(_store.Orders);
    }

    [Fact]
    public async Task PlaceOrder_Declined_SavesTheDeclinedOrder_AndKeepsTheSeatsOnSale()
    {
        var result = await PlaceOrder.PlaceOrderAsync(new PlaceOrderCommand(_backRoom.Id, 2, "fan@example.com", "tok_declined"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Declined, result.Value.Status);
        Assert.Equal(4, _backRoom.SeatsAvailable);
        Assert.Empty(_notifier.TicketsSent);
    }

    [Fact]
    public async Task PlaceOrder_SomeoneElseSavedFirst_RefundsTheCard_AndIsAConflict()
    {
        _store.NextSaveFails = Result.Conflict();

        var result = await PlaceOrder.PlaceOrderAsync(new PlaceOrderCommand(_backRoom.Id, 2, "fan@example.com", "tok_visa"), TestContext.Current.CancellationToken);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Single(_payments.Refunds);
        Assert.Empty(_notifier.TicketsSent);
    }

    [Fact]
    public async Task PlaceOrder_SaveFailsForAnotherReason_RefundsTheCard_AndIsAnError()
    {
        _store.NextSaveFails = Result.Error("The database is down.");

        var result = await PlaceOrder.PlaceOrderAsync(new PlaceOrderCommand(_backRoom.Id, 2, "fan@example.com", "tok_visa"), TestContext.Current.CancellationToken);

        Assert.Equal(ResultStatus.Error, result.Status);
        Assert.Single(_payments.Refunds);
        Assert.Empty(_notifier.TicketsSent);
    }

    [Fact]
    public async Task RefundOrder_RefundsTheCard_ReturnsTheSeats_AndTellsTheFan()
    {
        var placed = await PlaceOrder.PlaceOrderAsync(new PlaceOrderCommand(_backRoom.Id, 3, "fan@example.com", "tok_visa"), TestContext.Current.CancellationToken);

        var result = await RefundOrder.RefundAsync(placed.Value.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Refunded, result.Value.Status);
        Assert.Equal(4, _backRoom.SeatsAvailable);
        Assert.Single(_payments.Refunds);
        Assert.Same(result.Value, Assert.Single(_notifier.RefundsSent));
    }

    [Fact]
    public async Task RefundOrder_UnknownOrder_IsNotFound()
    {
        var result = await RefundOrder.RefundAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task RefundOrder_DeclinedOrder_IsInvalid_AndNeverCallsThePaymentProvider()
    {
        var placed = await PlaceOrder.PlaceOrderAsync(new PlaceOrderCommand(_backRoom.Id, 1, "fan@example.com", "tok_declined"), TestContext.Current.CancellationToken);

        var result = await RefundOrder.RefundAsync(placed.Value.Id, TestContext.Current.CancellationToken);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Empty(_payments.Refunds);
    }

    [Fact]
    public async Task RefundOrder_SomeoneElseRefundedItFirst_IsAConflict_AndReturnsNoSeats()
    {
        var placed = await PlaceOrder.PlaceOrderAsync(new PlaceOrderCommand(_backRoom.Id, 3, "fan@example.com", "tok_visa"), TestContext.Current.CancellationToken);
        _store.NextSaveFails = Result.Conflict();

        var result = await RefundOrder.RefundAsync(placed.Value.Id, TestContext.Current.CancellationToken);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(1, _backRoom.SeatsAvailable); // The request that won returns the seats, not this one.
        Assert.Empty(_notifier.RefundsSent);
    }

    [Fact]
    public async Task RefundOrder_ProviderFails_IsAnError_AndChangesNothing()
    {
        var placed = await PlaceOrder.PlaceOrderAsync(new PlaceOrderCommand(_backRoom.Id, 3, "fan@example.com", "tok_visa"), TestContext.Current.CancellationToken);
        _payments.RefundsFail = true;

        var result = await RefundOrder.RefundAsync(placed.Value.Id, TestContext.Current.CancellationToken);

        Assert.Equal(ResultStatus.Error, result.Status);
        Assert.Equal(1, _backRoom.SeatsAvailable);
        Assert.Empty(_notifier.RefundsSent);
    }

    private sealed class InMemoryStore : IEventRepository, IOrderRepository, IUnitOfWork
    {
        public List<Event> Events { get; } = [];

        public List<Order> Orders { get; } = [];

        /// <summary>When set, the next save fails with this result and saves nothing.</summary>
        public Result? NextSaveFails { get; set; }

        private readonly List<Order> _pending = [];

        public Task<Event?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Events.FirstOrDefault(e => e.Id == id));

        public Task<IReadOnlyList<Event>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Event>>(Events);

        public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Orders.FirstOrDefault(o => o.Id == id));

        public void Add(Order order) => _pending.Add(order);

        public Task ReturnSeatsAsync(string eventId, int quantity, CancellationToken cancellationToken)
        {
            Events.Single(e => e.Id == eventId).ReturnSeats(quantity);
            return Task.CompletedTask;
        }

        public Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (NextSaveFails is { } failure)
            {
                NextSaveFails = null;
                _pending.Clear();
                return Task.FromResult(failure);
            }

            Orders.AddRange(_pending);
            _pending.Clear();
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FakePayments : IPaymentGateway
    {
        public List<decimal> Charges { get; } = [];

        public List<string> Refunds { get; } = [];

        public bool RefundsFail { get; set; }

        public Task<PaymentResult> ChargeAsync(string idempotencyKey, string paymentToken, decimal amount, CancellationToken cancellationToken)
        {
            if (paymentToken == "tok_declined")
            {
                return Task.FromResult(new PaymentResult(false, null, "The card was declined."));
            }

            Charges.Add(amount);
            return Task.FromResult(new PaymentResult(true, $"ch_{Charges.Count}", null));
        }

        public Task<PaymentResult> RefundAsync(string idempotencyKey, string paymentReference, CancellationToken cancellationToken)
        {
            if (RefundsFail)
            {
                return Task.FromResult(new PaymentResult(false, null, "The provider is down."));
            }

            Refunds.Add(paymentReference);
            return Task.FromResult(new PaymentResult(true, $"re_{Refunds.Count}", null));
        }
    }

    private sealed class RecordingNotifier : IFanNotifier
    {
        public List<Order> TicketsSent { get; } = [];

        public List<Order> RefundsSent { get; } = [];

        public Task SendTicketsAsync(Order order, CancellationToken cancellationToken)
        {
            TicketsSent.Add(order);
            return Task.CompletedTask;
        }

        public Task SendRefundAsync(Order order, CancellationToken cancellationToken)
        {
            RefundsSent.Add(order);
            return Task.CompletedTask;
        }
    }
}
