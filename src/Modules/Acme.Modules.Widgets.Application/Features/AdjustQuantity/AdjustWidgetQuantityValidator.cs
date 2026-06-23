using Acme.CQRS.Abstractions;
using Acme.Modules.Widgets.Application.Contracts;

namespace Acme.Modules.Widgets.Application.Features.AdjustQuantity;

internal sealed class AdjustWidgetQuantityValidator : RequestValidator<AdjustWidgetQuantityCommand>
{
    protected override void Validate(
        AdjustWidgetQuantityCommand request,
        List<ValidationError> errors
    )
    {
        Rule(
            request.Delta != 0,
            errors,
            nameof(request.Delta),
            "Delta must be non-zero (use a positive value to add stock, negative to remove)."
        );
    }
}
