using Acme.Kernel.Application.Common.Interfaces;

namespace Acme.Modules.Greetings.Application;

/// <summary>
/// Marker for the Greetings module's own no-tracking read context (ADR-0015 + #9/ADR-0016). DI cannot
/// disambiguate one shared <see cref="IReadDataContext"/>, so each module resolves its read context
/// through its own marker; the implementation lives in Greetings.Infrastructure.
/// </summary>
public interface IGreetingsReadContext : IReadDataContext;
