namespace Products.Application.Common.Abstractions;

/// <summary>
/// Commits the changes tracked in the current business transaction.
/// </summary>
/// <remarks>
/// Kept separate from the repositories so a handler that touches more than one
/// aggregate still commits once, atomically, rather than leaving a half-applied
/// change behind.
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>Persists all pending changes and dispatches any domain events raised.</summary>
    /// <returns>The number of state entries written.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
