using Microsoft.Extensions.Logging.Abstractions;
using TicketTide.EmailSending.Contracts;
using TicketTide.EmailSending.Internal;
using TicketTide.EmailSending.Internal.Integrations;

namespace TicketTide.EmailSending.Tests;

public sealed class EmailSendingModuleTests
{
    [Fact]
    public void UsesOnlyOtherModulesContracts() => ModuleBoundaries.AssertUsesOnlyContracts(typeof(EmailSendingModule).Assembly);

    [Fact]
    public void SmtpEndpoint_ReadsAspiresConnectionString()
    {
        var endpoint = SmtpEndpoint.FromConnectionString("Endpoint=smtp://localhost:2525");

        Assert.Equal(new SmtpEndpoint("localhost", 2525), endpoint);
    }

    [Fact]
    public void SmtpEndpoint_WithoutAnEndpoint_Throws() =>
        Assert.Throws<InvalidOperationException>(() => SmtpEndpoint.FromConnectionString("Host=localhost"));

    [Fact]
    public async Task SendEmail_IsBestEffort_AnUnreachableServerDoesNotThrow()
    {
        // Nothing listens on port 1, so the send fails. The order is already saved, so the failure is only logged.
        var sender = new SmtpEmailSender(new SmtpEndpoint("127.0.0.1", 1), NullLogger<SmtpEmailSender>.Instance);

        await new SendEmailHandler(sender).Handle(new SendEmail("fan@example.com", "Hello", "Body"), TestContext.Current.CancellationToken);
    }

    [Fact]
    public void SendEmailValidator_RejectsABadAddress() =>
        Assert.False(new SendEmailValidator().Validate(new SendEmail("not-an-email", "Hello", "Body")).IsValid);
}
