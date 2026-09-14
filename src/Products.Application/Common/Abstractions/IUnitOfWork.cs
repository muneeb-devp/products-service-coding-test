namespace Products.Application.Common.Abstractions;

/// <summary>
/// Commits the changes tracked in the current business transaction.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Persists all pending changes and dispatches any domain events raised.</summary>
    /// <returns>The number of state entries written.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
