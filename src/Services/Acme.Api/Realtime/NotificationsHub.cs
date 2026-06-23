using Acme.Kernel.Application.Common.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Acme.Api.Realtime;

/// <summary>
/// Generic realtime notifications hub. A client joins the channels it cares about (e.g. "greetings",
/// "widgets"); a module pushes a <c>NotificationMessage</c> via <c>INotificationPublisher</c>, which
/// fans out to that channel's group. The browser reaches this only via the SSR origin's <c>/hubs</c>
/// proxy (ADR-0003).
/// </summary>
public sealed class NotificationsHub : Hub
{
    public Task JoinChannel(string channel) =>
        Groups.AddToGroupAsync(Context.ConnectionId, NotificationHubTopics.GroupName(channel));

    public Task LeaveChannel(string channel) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, NotificationHubTopics.GroupName(channel));
}
