namespace Acme.Kernel.Application.Common.Realtime;

/// <summary>
/// Shared names for the realtime notifications hub: the client method invoked on push, and the
/// per-channel group convention used to scope a broadcast to the subscribers of one channel.
/// </summary>
public static class NotificationHubTopics
{
    /// <summary>Client-side hub method invoked when a notification is pushed.</summary>
    public const string NotificationReceived = "NotificationReceived";

    /// <summary>SignalR group name for a channel's subscribers.</summary>
    public static string GroupName(string channel) =>
        $"channel:{channel.Trim().ToLowerInvariant()}";
}
