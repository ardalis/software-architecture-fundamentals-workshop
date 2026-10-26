using Mediator;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketTide.EmailSending.Contracts;
using TicketTide.Events.Contracts;
using TicketTide.Events.Internal;
using TicketTide.Events.Internal.Data;
using TicketTide.Orders.Contracts;
using TicketTide.Orders.Internal;
using TicketTide.Orders.Internal.Data;
using TicketTide.Orders.Internal.Payments;
using TicketTide.SharedKernel.Behaviors;
using TicketTide.SharedKernel.Data;
using TicketTide.Tickets.Contracts;
using TicketTide.Tickets.Internal;
using TicketTide.Tickets.Internal.Data;

namespace TicketTide.IntegrationTests;

/// <summary>
/// The modules wired together the way TicketTide.Web wires them, but on SQLite, with a fake payment provider and
/// email that's recorded instead of sent. Each <see cref="SendAsync{TResponse}"/> and <see cref="QueryAsync{TResponse}"/> runs in its own scope, like an
/// HTTP request.
/// </summary>
public sealed class TestModules : IAsyncDisposable
{
    private readonly SqliteDatabase _database = new();
    private readonly ServiceProvider _services;

    public TestModules()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_database.Connection);
        services.AddSharedDatabase<SqliteConnection>((options, connection) =>
            options.UseSqlite(connection).AddInterceptors(SaveFailures));

        services.AddEventsModule();
        services.AddOrdersModule();
        services.AddTicketsModule();
        services.AddSingleton<IPaymentGateway>(Payments); // Replaces the module's fake with one the tests control.
        services.AddSingleton(Emails);

        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.Assemblies =
            [
                typeof(GetEventDetails), typeof(EventsModule),
                typeof(OrderPlaced), typeof(OrdersModule),
                typeof(IssueTickets), typeof(TicketsModule),
                typeof(SendEmail), typeof(RecordingEmailHandler),
            ];
            options.PipelineBehaviors = [typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>)];
        });

        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public TestPayments Payments { get; } = new();

    public SentEmails Emails { get; } = new();

    /// <summary>Makes the next save of a new order fail, as if the database rejected it.</summary>
    public FailingOrderSave SaveFailures { get; } = new();

    /// <summary>Creates every module's tables and seeds the events, like the app does at startup.</summary>
    public async Task StartAsync()
    {
        foreach (var initializer in _services.GetServices<IHostedService>())
        {
            await initializer.StartAsync(CancellationToken.None);
        }
    }

    public async Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, CancellationToken.None);
    }

    public async Task<TResponse> QueryAsync<TResponse>(IQuery<TResponse> query)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(query, CancellationToken.None);
    }

    public async Task<int> SeatsSoldAsync(string eventId)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        return await db.Events.Where(e => e.Id == eventId).Select(e => e.SeatsSold).SingleAsync();
    }

    public async Task<List<Ticket>> TicketsAsync(Guid orderId)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        return await db.Tickets.AsNoTracking().Where(t => t.OrderId == orderId).ToListAsync();
    }

    public async Task<Order?> OrderAsync(Guid orderId)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        return await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);
    }

    public async Task<int> OrderCountAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Orders.CountAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        _database.Dispose();
    }
}

public sealed class TestPayments : IPaymentGateway
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

public sealed class SentEmails : List<SendEmail>;

/// <summary>Stands in for the EmailSending module: records each email instead of sending it.</summary>
public sealed class RecordingEmailHandler(SentEmails emails) : ICommandHandler<SendEmail>
{
    public ValueTask<Unit> Handle(SendEmail command, CancellationToken cancellationToken)
    {
        emails.Add(command);
        return ValueTask.FromResult(Unit.Value);
    }
}

public sealed class FailingOrderSave : SaveChangesInterceptor
{
    public bool FailNextOrderInsert { get; set; }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (FailNextOrderInsert && eventData.Context!.ChangeTracker.Entries<Order>().Any(e => e.State == EntityState.Added))
        {
            FailNextOrderInsert = false;
            throw new DbUpdateException("The database rejected the order (simulated).");
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
