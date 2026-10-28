# Contracts and Mediator Detail

## Mediator example

```csharp
// In UserManagement.Contracts (public)
public record UserRegisteredEvent(Guid UserId, string Email) : INotification;

// In CertificationTracking (internal handler)
internal class UserRegisteredHandler : INotificationHandler<UserRegisteredEvent>
{
    public Task Handle(UserRegisteredEvent evt, CancellationToken ct)
        => _provisioner.CreateComplianceRecordAsync(evt.UserId, ct);
}
```

The mediator keeps coupling loose but hides the call graph; compensate with naming conventions and architecture tests. Use it at boundaries only, not for a module's internal calls.

## Service-style contract example

```csharp
// In CourseManagement.Contracts
public interface ICourseQueryService
{
    Task<CourseSummaryDto?> GetSummaryAsync(Guid courseId, CancellationToken ct);
}
```

The consumer injects `ICourseQueryService`; the owner registers an implementation, often a facade over internal services. Discoverable, strongly typed, easy to debug; costs a compile-time dependency and runtime temporal coupling. Message style (mediator) has the lowest coupling for events but harder-to-trace flow.

## Contract design notes
- DTOs model the consumer-facing translation, not the internal domain model; translate at the boundary.
- Treat contracts as versioned APIs: additive changes are cheap, breaking changes touch every consumer.
- When a consumer needs a simplified view, put a proxy/facade in the owner's contracts that translates between models.
- A fat contract is a boundary leak in slow motion.
