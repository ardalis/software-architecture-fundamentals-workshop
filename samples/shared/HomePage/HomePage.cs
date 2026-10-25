using System.Net;

namespace TicketTide.Api;

/// <summary>
/// Serves the sample's home page at <c>/</c>: one static HTML page with buttons for the happy path and the
/// failure paths, so you can run the saga from a browser. Every sample's <c>TicketTide.Api</c> links this file and
/// <c>index.html</c> from <c>samples/shared/HomePage</c>, so edit them there, not in a sample.
/// </summary>
/// <remarks>
/// The page's "See the diagram" dialog shows the saga's animated diagram. Its files are embedded from
/// <c>diagrams/sagas</c> and served at <c>/diagrams/sagas/...</c>, keeping their folder layout so their relative
/// imports work. The diagram engine itself comes from the CDN.
/// </remarks>
internal static class HomePage
{
    private const string ResourceName = "TicketTide.Api.HomePage.index.html";
    private const string DiagramResourcePrefix = "TicketTide.Api.Diagrams/";

    /// <param name="sampleName">The sample's name, such as "Epic Saga".</param>
    /// <param name="summary">One line: its communication, consistency, and coordination.</param>
    /// <param name="diagram">The saga's folder in <c>diagrams/sagas</c>, such as "epic-saga".</param>
    /// <param name="ticketingFailures">How many times Ticketing fails in the "Ticketing fails" scenario.</param>
    /// <param name="ticketingFailureOutcome">What this saga does about it.</param>
    public static IEndpointRouteBuilder MapHomePage(
        this IEndpointRouteBuilder app, string sampleName, string summary, string diagram, int ticketingFailures,
        string ticketingFailureOutcome)
    {
        var diagrams = LoadDiagramFiles();
        var diagramPath = $"sagas/{diagram}/{diagram}.js";
        if (!diagrams.ContainsKey(diagramPath))
        {
            throw new InvalidOperationException($"The diagram {diagramPath} is not embedded. Is {diagram} a folder in diagrams/sagas?");
        }

        using var stream = typeof(HomePage).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource {ResourceName} is missing.");
        using var reader = new StreamReader(stream);

        var html = reader.ReadToEnd()
            .Replace("{{SampleName}}", WebUtility.HtmlEncode(sampleName))
            .Replace("{{Summary}}", WebUtility.HtmlEncode(summary))
            .Replace("{{DiagramUrl}}", $"/diagrams/{diagramPath}")
            .Replace("{{TicketingFailures}}", ticketingFailures.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{{TicketingFailureOutcome}}", WebUtility.HtmlEncode(ticketingFailureOutcome));

        app.MapGet("/", () => Results.Content(html, "text/html; charset=utf-8"))
            .ExcludeFromDescription();

        // Only the embedded files are served, so the path can't reach anything else.
        app.MapGet("/diagrams/{**path}", (string? path) => path is not null && diagrams.TryGetValue(path, out var file)
                ? Results.Bytes(file.Content, file.ContentType)
                : Results.NotFound())
            .ExcludeFromDescription();

        return app;
    }

    private static Dictionary<string, (byte[] Content, string ContentType)> LoadDiagramFiles()
    {
        var assembly = typeof(HomePage).Assembly;
        var files = new Dictionary<string, (byte[] Content, string ContentType)>(StringComparer.Ordinal);
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(DiagramResourcePrefix, StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var content = new MemoryStream();
            stream.CopyTo(content);

            // %(RecursiveDir) uses the build machine's path separator.
            var path = name[DiagramResourcePrefix.Length..].Replace('\\', '/');
            var contentType = path.EndsWith(".css", StringComparison.Ordinal) ? "text/css; charset=utf-8" : "text/javascript; charset=utf-8";
            files[path] = (content.ToArray(), contentType);
        }

        return files;
    }
}
