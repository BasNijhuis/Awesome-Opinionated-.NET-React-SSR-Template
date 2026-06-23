using Acme.DomainAbstractions;

namespace Acme.CQRS.Abstractions;

/// <summary>
/// Routes commands/queries to their handlers, running validators first. A validation failure
/// returns a failed <see cref="Result{T}"/> (it is not thrown).
/// </summary>
public interface IRequestDispatcher
{
    Task<Result<TResult>> SendAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken cancellationToken = default
    );

    Task<Result<TResult>> QueryAsync<TResult>(
        IQuery<TResult> query,
        CancellationToken cancellationToken = default
    );
}
