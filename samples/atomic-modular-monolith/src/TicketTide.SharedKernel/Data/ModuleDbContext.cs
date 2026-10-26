using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace TicketTide.SharedKernel.Data;

/// <summary>
/// The base class for each module's DbContext. Each module has its own DbContext, its own entities, and its own
/// schema, so no module can query another's tables. But they all share the request's one connection, and they all
/// join the <see cref="UnitOfWork"/>'s transaction.
/// </summary>
public abstract class ModuleDbContext : DbContext
{
    protected ModuleDbContext(DbContextOptions options, UnitOfWork unitOfWork)
        : base(options) => unitOfWork.Enlist(this);

    /// <summary>
    /// Creates this module's tables if they aren't there yet. <c>EnsureCreated</c> can't do that here: it does
    /// nothing once the database has any tables, and every module's tables are in the same database.
    /// </summary>
    public async Task CreateTablesIfMissingAsync(CancellationToken cancellationToken)
    {
        var creator = this.GetService<IRelationalDatabaseCreator>();
        if (!await creator.ExistsAsync(cancellationToken))
        {
            await creator.CreateAsync(cancellationToken);
        }

        try
        {
            await creator.CreateTablesAsync(cancellationToken);
        }
        catch (DbException)
        {
            // The tables are already there: the database outlived the last run of the app.
        }
    }
}

public static class ModuleDatabaseExtensions
{
    /// <summary>
    /// Called once, by the host: every module's DbContext uses the request's one <typeparamref name="TConnection"/>,
    /// through the database provider that <paramref name="useProvider"/> sets up.
    /// </summary>
    public static IServiceCollection AddSharedDatabase<TConnection>(
        this IServiceCollection services, Action<DbContextOptionsBuilder, DbConnection> useProvider)
        where TConnection : DbConnection
    {
        // Whoever registered TConnection owns it. Aspire's SqlConnection is scoped, so each request gets its own.
        services.AddSingleton(new DatabaseProvider(useProvider));
        services.AddScoped(sp => new UnitOfWork(sp.GetRequiredService<TConnection>()));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());
        return services;
    }

    /// <summary>Called by each module for its own DbContext.</summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services)
        where TContext : ModuleDbContext =>
        services.AddDbContext<TContext>((sp, options) =>
            sp.GetRequiredService<DatabaseProvider>().UseProvider(options, sp.GetRequiredService<UnitOfWork>().Connection));

    private sealed record DatabaseProvider(Action<DbContextOptionsBuilder, DbConnection> UseProvider);
}
