using Ardalis.Result;

namespace TicketTide.Core.Interfaces;

/// <summary>Saves every change the repositories are tracking, in one transaction.</summary>
public interface IUnitOfWork
{
    /// <returns>
    /// A <see cref="ResultStatus.Conflict"/> result if someone else changed the same data first, or an
    /// <see cref="ResultStatus.Error"/> result if the database rejected the changes for another reason.
    /// </returns>
    Task<Result> SaveChangesAsync(CancellationToken cancellationToken);
}
