namespace Acme.Kernel.Contracts;

/// <summary>
/// A realtime notification pushed to subscribers of a channel over the SignalR hub. Shared cross-module
/// contract: a module raises one via <c>INotificationPublisher</c>; the web client receives it on the
/// hub and renders it (e.g. a toast). Deliberately generic — <paramref name="Channel"/> groups
/// subscribers, <paramref name="Message"/> is the human-readable payload.
/// </summary>
public sealed record NotificationMessage(string Channel, string Message);
