using Acme.Modules.Widgets.Domain.Contracts;

namespace Acme.Modules.Widgets.Domain.Tests;

// Minimal spec implementation so Widgets domain-method tests can construct inputs by name.
internal sealed record CreateWidgetSpec(string Name, int Quantity) : ICreateWidgetSpec;
