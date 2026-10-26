namespace TicketTide.EndToEndTests;

/// <summary>
/// The home page at <c>/</c> lets you try the API from a browser. The monolith samples' (Clean Architecture, Vertical
/// Slice, and Atomic Modular Monolith) tests link this file from <c>samples/shared/MonolithHomePage</c>, so edit it
/// there.
/// </summary>
public sealed class HomePageTests(TicketTideApp app)
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
        Assert.Matches("<h1>(Clean Architecture|Vertical Slice|Atomic Modular Monolith)</h1>", html); // The sample's own name.
        Assert.Contains("data-scenario=\"happy\"", html);
        Assert.Contains("data-scenario=\"refund\"", html);
        Assert.Contains("data-scenario=\"declined\"", html);
        Assert.Contains("data-scenario=\"soldOut\"", html);
        Assert.Contains("data-scenario=\"unknownEvent\"", html);
        Assert.Contains("data-scenario=\"invalid\"", html);

        // The endpoints the page calls.
        Assert.Contains("fetch(\"/events\"", html);
        Assert.Contains("send(\"POST\", \"/orders\"", html);
        Assert.Contains("send(\"GET\", `/orders/", html);
        Assert.Contains("/refund`", html);
    }
}
