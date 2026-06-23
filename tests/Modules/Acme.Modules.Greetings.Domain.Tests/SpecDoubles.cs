using Acme.Modules.Greetings.Domain.Contracts;

namespace Acme.Modules.Greetings.Domain.Tests;

// Minimal spec implementation so Greetings domain-method tests can construct inputs by name.
internal sealed record CreateGreetingSpec(string Message) : ICreateGreetingSpec;
