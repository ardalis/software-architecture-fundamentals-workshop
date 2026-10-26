using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: AssemblyFixture(typeof(TicketTide.EndToEndTests.TicketTideApp))]

namespace TicketTide.EndToEndTests;

/// <summary>
/// Starts the whole Aspire app (API, SQL Server, Papercut) once for every test in the assembly.
/// The tests talk to it over HTTP only, the same way a fan's browser would.
/// </summary>
public sealed class TicketTideApp : IAsyncLifetime
{
    // Longer than the PostgreSQL samples: the SQL Server image is large, and SQL Server is slow to start.
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(10);

    private DistributedApplication? _app;

    public HttpClient Api { get; private set; } = null!;

    public PapercutClient Papercut { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        using var cts = new CancellationTokenSource(StartupTimeout);

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.TicketTide_AppHost>(cts.Token);
        builder.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Warning);
            logging.AddFilter("Aspire.", LogLevel.Warning);
        });

        _app = await builder.BuildAsync(cts.Token);
        await _app.StartAsync(cts.Token);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("api", cts.Token);

        Api = _app.CreateHttpClient("api");
        Api.Timeout = TimeSpan.FromMinutes(1);
        Papercut = new PapercutClient(_app.CreateHttpClient("papercut", "http"));
    }

    public async ValueTask DisposeAsync()
    {
        Api?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
