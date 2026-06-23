using Acme.DomainAbstractions;
using Acme.Modules.Widgets.Domain.Contracts;

namespace Acme.Modules.Widgets.Application;

/// <summary>Expected, recoverable failures for the Widgets module.</summary>
public static class WidgetErrors
{
    public static Error NotFound(WidgetId id) =>
        Error.NotFound("widget_not_found", $"No widget found with id '{id}'.");
}
