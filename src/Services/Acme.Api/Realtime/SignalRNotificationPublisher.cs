using Acme.Kernel.Application.Common.Interfaces;
using Acme.Kernel.Application.Common.Realtime;
using Acme.Kernel.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace Acme.Api.Realtime;

/// <summary>
/// SignalR-backed <see cref="INotificationPublisher"/>: broadcasts a <see cref="NotificationMessage"/>
/// to the subscribers of its channel. Bound in the API composition root so the Application layer stays
/// transport-agnostic.
/// </summary>
public sealed class SignalRNotificationPublisher(IHubContext<NotificationsHub> hub)
    : INotificationPublisher
{
    public Task PublishAsync(
        NotificationMessage notification,
        CancellationToken cancellationToken = default
    ) =>
        hub
            .Clients.Group(NotificationHubTopics.GroupName(notification.Channel))
            .SendAsync(NotificationHubTopics.NotificationReceived, notification, cancellationToken);
}
