using Acme.Modules.Greetings.Domain.Contracts;

namespace Acme.Modules.Greetings.Domain;

/// <summary>
/// Read-only hydration spec for the <see cref="Greeting"/> aggregate. Implemented by the module's
/// persistence entity so the aggregate rehydrates straight from it.
/// </summary>
public interface IGreetingState
{
    GreetingId Id { get; }
    string Message { get; }
    DateTimeOffset CreatedAt { get; }
}
