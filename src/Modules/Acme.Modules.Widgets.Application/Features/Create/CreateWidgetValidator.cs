using Acme.CQRS.Abstractions;
using Acme.Modules.Widgets.Application.Contracts;

namespace Acme.Modules.Widgets.Application.Features.Create;

internal sealed class CreateWidgetValidator : RequestValidator<CreateWidgetCommand>
{
    protected override void Validate(CreateWidgetCommand request, List<ValidationError> errors)
    {
        Rule(
            !string.IsNullOrWhiteSpace(request.Name),
            errors,
            nameof(request.Name),
            "Name is required."
        );
    }
}
