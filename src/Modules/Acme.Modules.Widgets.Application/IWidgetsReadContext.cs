using Acme.Kernel.Application.Common.Interfaces;

namespace Acme.Modules.Widgets.Application;

/// <summary>
/// Marker for the Widgets module's own no-tracking read context (ADR-0015 + #9/ADR-0016). DI cannot
/// disambiguate one shared <see cref="IReadDataContext"/>, so each module resolves its read context
/// through its own marker; the implementation lives in Widgets.Infrastructure.
/// </summary>
public interface IWidgetsReadContext : IReadDataContext;
