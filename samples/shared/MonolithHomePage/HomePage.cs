using System.Net;

namespace TicketTide.Web;

/// <summary>
/// Serves the sample's home page at <c>/</c>: one static HTML page with buttons for the happy path, a refund, and
/// the failure paths, so you can try the API from a browser. The monolith samples' (Clean Architecture, Vertical
/// Slice, and Atomic Modular Monolith) <c>TicketTide.Web</c> link this file and <c>index.html</c> from
/// <c>samples/shared/MonolithHomePage</c>, so edit them there, not in a sample.
/// </summary>
internal static class HomePage
{
    private const string ResourceName = "TicketTide.Web.HomePage.index.html";

    /// <param name="sampleName">The sample's name, such as "Clean Architecture".</param>
    /// <param name="summary">One line: how the sample's code is organized.</param>
    /// <param name="ticketingFailureOutcome">
    /// For a sample that supports <c>"simulateTicketingFailure": true</c> in the order request: what happens when
    /// Ticketing fails. The page shows that scenario only when this is set.
    /// </param>
    public static IEndpointRouteBuilder MapHomePage(
        this IEndpointRouteBuilder app, string sampleName, string summary, string? ticketingFailureOutcome = null)
    {
        using var stream = typeof(HomePage).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource {ResourceName} is missing.");
        using var reader = new StreamReader(stream);

        var html = reader.ReadToEnd()
            .Replace("{{SampleName}}", WebUtility.HtmlEncode(sampleName))
            .Replace("{{Summary}}", WebUtility.HtmlEncode(summary))
            .Replace("{{TicketingFailureHidden}}", ticketingFailureOutcome is null ? "hidden" : string.Empty)
            .Replace("{{TicketingFailureOutcome}}", WebUtility.HtmlEncode(ticketingFailureOutcome ?? string.Empty));

        app.MapGet("/", () => Results.Content(html, "text/html; charset=utf-8"))
            .ExcludeFromDescription();

        return app;
    }
}
