using Acme.CQRS.Abstractions;
using Acme.DomainAbstractions;
using Acme.Kernel.Application.Common.Interfaces;
using Acme.Kernel.Contracts;
using Acme.Modules.Greetings.Application.Contracts;
using Acme.Modules.Greetings.Domain;

namespace Acme.Modules.Greetings.Application.Features.Create;

internal sealed class CreateGreetingHandler(
    IGreetingRepository repository,
    IUnitOfWork unitOfWork,
    INotificationPublisher publisher
) : ICommandHandler<CreateGreetingCommand, CreateGreetingResult>
{
    public async Task<Result<CreateGreetingResult>> HandleAsync(
        CreateGreetingCommand command,
        CancellationToken cancellationToken
    ) =>
        Greeting.Create(command) switch
        {
            Success<Greeting>(var greeting) => await PersistAsync(greeting, cancellationToken),
            Failure failure => failure, // propagate the domain rule failure
        };

    private async Task<CreateGreetingResult> PersistAsync(
        Greeting greeting,
        CancellationToken cancellationToken
    )
    {
        repository.Add(greeting);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await publisher.PublishAsync(
            new NotificationMessage("greetings", $"New greeting: {greeting.Message}"),
            cancellationToken
        );

        return new CreateGreetingResult
        {
            Id = greeting.Id,
            Greeting = new GreetingDto
            {
                Id = greeting.Id,
                Message = greeting.Message,
                CreatedAt = greeting.CreatedAt,
            },
        };
    }
}
