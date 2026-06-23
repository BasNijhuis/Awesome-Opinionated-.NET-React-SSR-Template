using Acme.DomainAbstractions;
using Acme.Modules.Greetings.Domain.Contracts;

namespace Acme.Modules.Greetings.Application;

/// <summary>Expected, recoverable failures for the Greetings module.</summary>
public static class GreetingErrors
{
    public static Error NotFound(GreetingId id) =>
        Error.NotFound("greeting_not_found", $"No greeting found with id '{id}'.");
}
