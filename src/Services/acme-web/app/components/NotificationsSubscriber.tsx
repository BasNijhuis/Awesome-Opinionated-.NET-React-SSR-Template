import { useEffect, useState } from "react";

type Notification = {
  /** Monotonic id so repeated identical payloads still re-show. */
  key: number;
  channel: string;
  message: string;
};

/**
 * Connects to the API's `/hubs/notifications` SignalR hub (proxied at the web origin — ADR-0003) and
 * shows a transient toast for every `NotificationReceived` push. The hub is best-effort: if it is
 * unreachable the rest of the app still works via loaders/actions, so connection failures are
 * swallowed. Mounted once in {@link AppShell}.
 */
export function NotificationsSubscriber() {
  const [toasts, setToasts] = useState<Notification[]>([]);

  useEffect(() => {
    let connection: import("@microsoft/signalr").HubConnection | null = null;
    let disposed = false;
    let nextKey = 0;

    const show = (channel: string, message: string) => {
      nextKey += 1;
      const key = nextKey;
      setToasts((current) => [...current, { key, channel, message }]);
      // Auto-dismiss after a few seconds so toasts stay transient.
      setTimeout(() => {
        setToasts((current) => current.filter((toast) => toast.key !== key));
      }, 5000);
    };

    void (async () => {
      const signalR = await import("@microsoft/signalr");
      if (disposed) {
        return;
      }

      connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/notifications")
        .withAutomaticReconnect([0, 1000, 3000, 5000, 10000])
        .build();

      connection.on("NotificationReceived", (payload: { channel: string; message: string }) => {
        show(payload.channel, payload.message);
      });

      try {
        await connection.start();
      } catch {
        // Hub unavailable — the app still works via form actions.
      }
    })();

    return () => {
      disposed = true;
      const active = connection;
      connection = null;
      void active?.stop();
    };
  }, []);

  if (toasts.length === 0) {
    return null;
  }

  return (
    <div
      className="fixed inset-x-0 bottom-4 z-50 flex flex-col items-center gap-2 px-4"
      aria-live="polite"
    >
      {toasts.map((toast) => (
        <div
          key={toast.key}
          role="status"
          data-testid="notification-toast"
          className="w-full max-w-sm rounded-xl border border-brand/40 bg-surface-raised px-4 py-3 text-surface-fg shadow-xl animate-slide-up"
        >
          <p className="text-brand text-xs uppercase tracking-wide">{toast.channel}</p>
          <p className="mt-1 text-sm">{toast.message}</p>
        </div>
      ))}
    </div>
  );
}
