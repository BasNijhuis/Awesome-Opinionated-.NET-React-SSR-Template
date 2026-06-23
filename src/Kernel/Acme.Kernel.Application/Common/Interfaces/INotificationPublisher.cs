using Acme.Kernel.Contracts;

namespace Acme.Kernel.Application.Common.Interfaces;

/// <summary>
/// Publishes a realtime <see cref="NotificationMessage"/> to subscribers of its channel. A shared
/// kernel port (like <c>IUnitOfWork</c>): the Application layer depends only on this abstraction, while
/// the host binds it to a transport (SignalR in the API; a no-op or test double elsewhere). Keeps the
/// Application layer free of any SignalR/ASP.NET dependency.
/// </summary>
public interface INotificationPublisher
{
    Task PublishAsync(
        NotificationMessage notification,
        CancellationToken cancellationToken = default
    );
}
