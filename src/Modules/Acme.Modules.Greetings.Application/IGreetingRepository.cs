using Acme.Modules.Greetings.Domain;
using Acme.Modules.Greetings.Domain.Contracts;

namespace Acme.Modules.Greetings.Application;

/// <summary>
/// Loads and persists the <see cref="Greeting"/> aggregate. Implemented over the Greetings module's
/// own context; the application layer stays off EF Core.
/// </summary>
public interface IGreetingRepository
{
    void Add(Greeting greeting);

    Task<Greeting?> GetByIdAsync(GreetingId id, CancellationToken cancellationToken);
}
