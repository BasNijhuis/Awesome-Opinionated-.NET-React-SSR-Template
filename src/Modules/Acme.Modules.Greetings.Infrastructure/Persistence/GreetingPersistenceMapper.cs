using Acme.Modules.Greetings.Application.Entities;
using Acme.Modules.Greetings.Domain;

namespace Acme.Modules.Greetings.Infrastructure.Persistence;

/// <summary>
/// Maps between the <see cref="Greeting"/> aggregate and its persistence entity. Hydration reads
/// straight from the entity (it implements <c>IGreetingState</c>); persistence reads the aggregate's
/// public surface.
/// </summary>
internal static class GreetingPersistenceMapper
{
    public static Greeting ToDomain(GreetingEntity entity) => Greeting.Rehydrate(entity);

    public static GreetingEntity ToEntity(Greeting greeting) =>
        new()
        {
            Id = greeting.Id,
            Message = greeting.Message,
            CreatedAt = greeting.CreatedAt,
        };

    public static void ApplyToEntity(Greeting greeting, GreetingEntity entity)
    {
        entity.Message = greeting.Message;
        entity.CreatedAt = greeting.CreatedAt;
    }
}
