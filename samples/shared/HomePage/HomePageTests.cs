using System.Text.RegularExpressions;

namespace TicketTide.EndToEndTests;

/// <summary>
/// The home page at <c>/</c> lets you run the saga from a browser. Every sample's tests link this file from
/// <c>samples/shared/HomePage</c>, so edit it there.
/// </summary>
public sealed partial class HomePageTests(TicketTideApp app)
{
    [Fact]
    public async Task HomePage_IsBrowsable()
    {
        var response = await app.Api.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("<title>", html);
        Assert.DoesNotContain("{{", html); // Every placeholder was filled in.
        Assert.Contains("data-scenario=\"happy\"", html);
        Assert.Contains("data-scenario=\"ticketing\"", html);
        Assert.Contains("fetch(\"/orders\"", html);
    }

    [Fact]
    public async Task HomePage_ServesItsSagaDiagram()
    {
        var html = await app.Api.GetStringAsync("/", TestContext.Current.CancellationToken);

        // The sample's own saga: "Epic Saga" shows diagrams/sagas/epic-saga/epic-saga.js.
        var saga = SampleName().Match(html).Groups[1].Value.ToLowerInvariant().Replace(' ', '-');
        var diagramUrl = DiagramUrl().Match(html).Groups[1].Value;
        Assert.Equal($"/diagrams/sagas/{saga}/{saga}.js", diagramUrl);

        // The diagram module and the files it loads relative to itself.
        await AssertServed(diagramUrl, "text/javascript");
        await AssertServed("/diagrams/sagas/saga-kit.js", "text/javascript");
        await AssertServed("/diagrams/sagas/sagas.css", "text/css");

        // Only the embedded diagram files are served.
        foreach (var path in new[] { "/diagrams/sagas/index.html", "/diagrams/" })
        {
            var missing = await app.Api.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
    }

    private async Task AssertServed(string path, string mediaType)
    {
        var response = await app.Api.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [GeneratedRegex("<h1>([^<]+)</h1>")]
    private static partial Regex SampleName();

    [GeneratedRegex("data-diagram=\"([^\"]+)\"")]
    private static partial Regex DiagramUrl();
}
