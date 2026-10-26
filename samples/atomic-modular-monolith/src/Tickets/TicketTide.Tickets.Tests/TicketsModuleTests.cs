using Microsoft.EntityFrameworkCore;
using TicketTide.SharedKernel.Data;
using TicketTide.Tickets.Contracts;
using TicketTide.Tickets.Internal;
using TicketTide.Tickets.Internal.Data;
using TicketTide.Tickets.Internal.Integrations;

namespace TicketTide.Tickets.Tests;

public sealed class TicketsModuleTests : IAsyncLifetime, IDisposable
{
    private const string Fan = "fan@example.com";

    private readonly SqliteDatabase _database = new();
    private readonly UnitOfWork _unitOfWork;
    private readonly TicketsDbContext _db;

    public TicketsModuleTests()
    {
        _unitOfWork = new UnitOfWork(_database.Connection);
        _db = _database.CreateContext<TicketsDbContext>(_unitOfWork);
    }

    public async ValueTask InitializeAsync() => await _db.CreateTablesIfMissingAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    [Fact]
    public void UsesOnlyOtherModulesContracts() => ModuleBoundaries.AssertUsesOnlyContracts(typeof(TicketsModule).Assembly);

    [Fact]
    public async Task IssueTickets_IssuesOnePerSeat_HeldByTheFan_WithUniqueCodes()
    {
        var orderId = Guid.CreateVersion7();

        var result = await new IssueTicketsHandler(_db).Handle(new IssueTickets(orderId, Fan, 3), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
        Assert.All(result.Value, t =>
        {
            Assert.Equal(Fan, t.HolderEmail);
            Assert.Equal("Paid", t.Status);
            Assert.StartsWith("TT-", t.Code);
        });
        Assert.Equal(3, result.Value.Select(t => t.Code).Distinct().Count());

        // Read back in the order they were issued.
        var stored = await new GetTicketsForOrderHandler(_db).Handle(new GetTicketsForOrder(orderId), TestContext.Current.CancellationToken);
        Assert.Equal(result.Value, stored);
    }

    [Fact]
    public async Task IssueTickets_SimulatedFailure_WritesTheTicketsThenThrows_AndARollbackRemovesThem()
    {
        var orderId = Guid.CreateVersion7();
        var handler = new IssueTicketsHandler(_db);

        await using (await _unitOfWork.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await handler.Handle(new IssueTickets(orderId, Fan, 2, SimulateFailure: true), TestContext.Current.CancellationToken));

            // Written, inside the transaction...
            Assert.Equal(2, await _db.Tickets.CountAsync(t => t.OrderId == orderId, TestContext.Current.CancellationToken));
        }

        // ...and gone, because the transaction was disposed without committing.
        Assert.Equal(0, await _db.Tickets.CountAsync(t => t.OrderId == orderId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RefundTickets_EveryTicketInTheOrderStopsWorking()
    {
        var orderId = Guid.CreateVersion7();
        var otherOrderId = Guid.CreateVersion7();
        await new IssueTicketsHandler(_db).Handle(new IssueTickets(orderId, Fan, 2), TestContext.Current.CancellationToken);
        await new IssueTicketsHandler(_db).Handle(new IssueTickets(otherOrderId, Fan, 1), TestContext.Current.CancellationToken);

        var result = await new RefundTicketsHandler(_db).Handle(new RefundTickets(orderId), TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Value.Count);
        Assert.All(result.Value, t => Assert.Equal("Refunded", t.Status));
        var other = await new GetTicketsForOrderHandler(_db).Handle(new GetTicketsForOrder(otherOrderId), TestContext.Current.CancellationToken);
        Assert.Equal("Paid", Assert.Single(other).Status);
    }

    [Fact]
    public void IssueTicketsValidator_RejectsABadAddress() =>
        Assert.False(new IssueTicketsValidator().Validate(new IssueTickets(Guid.CreateVersion7(), "not-an-email", 1)).IsValid);
}
