using Acme.Modules.Greetings.Domain.Contracts;
using Acme.Modules.Widgets.Domain.Contracts;

namespace Acme.Kernel.Infrastructure.Tests;

// Minimal spec implementations so the coordinating-UoW tests can stage writes in two modules by name
// (cross-module by design — this project exercises AcmeUnitOfWork across modules).
internal sealed record CreateGreetingSpec(string Message) : ICreateGreetingSpec;

internal sealed record CreateWidgetSpec(string Name, int Quantity) : ICreateWidgetSpec;
