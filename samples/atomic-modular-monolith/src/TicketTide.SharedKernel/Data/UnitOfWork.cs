using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace TicketTide.SharedKernel.Data;

/// <summary>
/// One database transaction that every module's work joins, so the work of several modules commits or rolls back
/// as a whole. The orchestrating use case starts it; the modules it calls don't know or care that it exists.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Starts a transaction on the request's one database connection. Every module's DbContext in this request
    /// joins it, including those created afterwards. Dispose it without committing to roll everything back.
    /// </summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);

    Task RollbackAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The unit of work for one request (it's scoped). It owns the request's single <see cref="DbConnection"/>, which
/// every <see cref="ModuleDbContext"/> uses, and puts each of them in the same transaction.
/// </summary>
/// <remarks>
/// This only works because every module's tables are in the same database. Give a module its own database and a
/// transaction can't span them any more: that's when you need a saga.
/// </remarks>
public sealed class UnitOfWork(DbConnection connection) : IUnitOfWork
{
    private readonly List<DbContext> _contexts = [];
    private DbTransaction? _transaction;

    /// <summary>The request's one connection. Every module's DbContext uses it.</summary>
    public DbConnection Connection => connection;

    /// <summary>True while a transaction is open, so the work happening now commits or rolls back with it.</summary>
    public bool InTransaction => _transaction is not null;

    /// <summary>Called by each <see cref="ModuleDbContext"/> when it's created.</summary>
    internal void Enlist(DbContext context)
    {
        _contexts.Add(context);
        if (_transaction is not null)
        {
            context.Database.UseTransaction(_transaction);
        }
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A transaction is already open. The use case that started it decides when it ends.");
        }

        if (connection.State != System.Data.ConnectionState.Open)
        {
            // Opened here, the connection stays open for the rest of the request, so EF Core doesn't close it
            // between the modules' commands. The request's scope disposes it.
            await connection.OpenAsync(cancellationToken);
        }

        _transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var context in _contexts)
        {
            await context.Database.UseTransactionAsync(_transaction, cancellationToken);
        }

        return new Transaction(this);
    }

    private async Task EndAsync(bool commit, CancellationToken cancellationToken)
    {
        if (_transaction is null)
        {
            return;
        }

        try
        {
            if (commit)
            {
                await _transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await _transaction.RollbackAsync(cancellationToken);
            }
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
            foreach (var context in _contexts)
            {
                await context.Database.UseTransactionAsync(null, CancellationToken.None);
                if (!commit)
                {
                    // Whatever the modules saved was rolled back, so what they're tracking is no longer true.
                    context.ChangeTracker.Clear();
                }
            }
        }
    }

    private sealed class Transaction(UnitOfWork unitOfWork) : IUnitOfWorkTransaction
    {
        private bool _ended;

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            _ended = true;
            await unitOfWork.EndAsync(commit: true, cancellationToken);
        }

        public async Task RollbackAsync(CancellationToken cancellationToken)
        {
            _ended = true;
            await unitOfWork.EndAsync(commit: false, cancellationToken);
        }

        // Leaving the using block without committing (an early return, or an exception) rolls everything back.
        public async ValueTask DisposeAsync()
        {
            if (!_ended)
            {
                _ended = true;
                await unitOfWork.EndAsync(commit: false, CancellationToken.None);
            }
        }
    }
}
