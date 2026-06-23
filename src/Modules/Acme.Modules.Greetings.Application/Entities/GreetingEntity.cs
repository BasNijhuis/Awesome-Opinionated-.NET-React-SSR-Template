using Acme.Modules.Greetings.Domain;
using Acme.Modules.Greetings.Domain.Contracts;

namespace Acme.Modules.Greetings.Application.Entities;

// Persistence entity for the Greetings module (own schema). The strongly-typed id (an EF value
// converter maps it to a uuid column) and it implements the domain hydration spec (IGreetingState)
// so Greeting.Rehydrate reconstructs the aggregate straight from it. EF mapping lives in
// Greetings.Infrastructure (GreetingsDbContext).
public sealed class GreetingEntity : IGreetingState
{
    public GreetingId Id { get; set; }
    public required string Message { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
