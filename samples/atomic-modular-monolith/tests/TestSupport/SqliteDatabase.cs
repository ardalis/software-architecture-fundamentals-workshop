using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TicketTide.SharedKernel.Data;

namespace TicketTide.TestSupport;

/// <summary>
/// An in-memory SQLite database standing in for SQL Server, so the tests need no Docker. Like the real app, every
/// module's DbContext uses the same single connection, so a <see cref="UnitOfWork"/> transaction covers all of them.
/// Linked into every test project that needs it from tests/TestSupport: edit it there.
/// </summary>
public sealed class SqliteDatabase : IDisposable
{
    public SqliteDatabase() => Connection.Open();

    /// <summary>The one connection. The in-memory database lives as long as it's open.</summary>
    public SqliteConnection Connection { get; } = new("Data Source=:memory:");

    /// <summary>A module's DbContext on the shared connection, enlisted in <paramref name="unitOfWork"/>.</summary>
    public TContext CreateContext<TContext>(UnitOfWork unitOfWork)
        where TContext : ModuleDbContext
    {
        var options = new DbContextOptionsBuilder<TContext>().UseSqlite(Connection).Options;
        return (TContext)Activator.CreateInstance(typeof(TContext), options, unitOfWork)!;
    }

    public void Dispose() => Connection.Dispose();
}

public static class ModuleBoundaries
{
    /// <summary>
    /// A module may use other modules' Contracts, never their Internal projects. Fails if
    /// <paramref name="module"/> uses a type from another module's Internal assembly.
    /// </summary>
    public static void AssertUsesOnlyContracts(Assembly module)
    {
        var internals = module.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => name.StartsWith("TicketTide.", StringComparison.Ordinal) && name.EndsWith(".Internal", StringComparison.Ordinal))
            .ToList();

        Xunit.Assert.True(internals.Count == 0, $"{module.GetName().Name} uses another module's Internal project: {string.Join(", ", internals)}. Use its Contracts instead.");
    }
}
