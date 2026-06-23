using Acme.CQRS.Abstractions;
using Acme.Modules.Greetings.Application.Contracts;

namespace Acme.Modules.Greetings.Application.Features.Create;

internal sealed class CreateGreetingValidator : RequestValidator<CreateGreetingCommand>
{
    protected override void Validate(CreateGreetingCommand request, List<ValidationError> errors)
    {
        Rule(
            !string.IsNullOrWhiteSpace(request.Message),
            errors,
            nameof(request.Message),
            "Message is required."
        );
    }
}
