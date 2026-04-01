import { useCallback, useEffect, useState } from 'react';
import { toast } from 'sonner';
import { useAuth } from '@/auth';
import {
  notificationService,
  type AdminNotification,
  type NotificationStreamEvent,
} from '@/services/notificationService';

interface NotificationState {
  notifications: AdminNotification[];
  unreadCount: number;
  loading: boolean;
}

const InitialState: NotificationState = {
  notifications: [],
  unreadCount: 0,
  loading: true,
};

export function useNotifications() {
  const { isAuthenticated, getAccessToken } = useAuth();
  const [state, setState] = useState<NotificationState>(InitialState);

  const loadNotifications = useCallback(async () => {
    setState((current) => ({ ...current, loading: true }));

    try {
      const [notifications, summary] = await Promise.all([
        notificationService.list({ take: 50 }),
        notificationService.getSummary(),
      ]);

      setState({
        notifications,
        unreadCount: summary.unreadCount,
        loading: false,
      });
    } catch (error) {
      console.error('Failed to load notifications:', error);
      toast.error('Failed to load notifications');
      setState((current) => ({ ...current, loading: false }));
    }
  }, []);

  useEffect(() => {
    if (!isAuthenticated) {
      setState(InitialState);
      return;
    }

    void loadNotifications();

    const abortController = new AbortController();
    let active = true;

    const connect = async () => {
      while (active && !abortController.signal.aborted) {
        try {
          await notificationService.subscribeToStream({
            getAccessToken,
            signal: abortController.signal,
            onEvent: (event) => {
              setState((current) => applyStreamEvent(current, event));
            },
          });

          await waitForReconnect(1500, abortController.signal);
        } catch (error) {
          if (abortController.signal.aborted || !active) {
            return;
          }

          console.warn('Notification stream disconnected; retrying.', error);
          await waitForReconnect(3000, abortController.signal);
        }
      }
    };

    void connect();

    return () => {
      active = false;
      abortController.abort();
    };
  }, [getAccessToken, isAuthenticated, loadNotifications]);

  const markRead = useCallback(async (notificationId: string) => {
    const notification = await notificationService.markRead(notificationId);
    setState((current) => applyNotificationChange(current, notification, 0));
    return notification;
  }, []);

  const dismiss = useCallback(async (notificationId: string) => {
    const notification = await notificationService.dismiss(notificationId);
    setState((current) => applyNotificationChange(current, notification, 0));
    return notification;
  }, []);

  const markAllRead = useCallback(async () => {
    const result = await notificationService.markAllRead();
    if (result.affectedCount <= 0) {
      return result;
    }

    const readAt = new Date().toISOString();
    setState((current) => ({
      ...current,
      unreadCount: Math.max(0, current.unreadCount - result.affectedCount),
      notifications: current.notifications.map((notification) =>
        notification.status === 'Unread'
          ? { ...notification, status: 'Read', readAt: notification.readAt ?? readAt }
          : notification
      ),
    }));

    return result;
  }, []);

  return {
    notifications: state.notifications,
    unreadCount: state.unreadCount,
    loading: state.loading,
    refresh: loadNotifications,
    markRead,
    dismiss,
    markAllRead,
  };
}

function applyStreamEvent(current: NotificationState, event: NotificationStreamEvent): NotificationState {
  switch (event.type) {
    case 'HELLO':
      return {
        ...current,
        unreadCount: typeof event.unreadCount === 'number' ? event.unreadCount : current.unreadCount,
      };
    case 'NOTIFICATION_CREATED':
    case 'NOTIFICATION_UPDATED':
      if (!event.notification) {
        return current;
      }

      return applyNotificationChange(current, event.notification, event.unreadCountDelta ?? 0);
    default:
      return current;
  }
}

function applyNotificationChange(
  current: NotificationState,
  notification: AdminNotification,
  unreadCountDelta: number
): NotificationState {
  const existingIndex = current.notifications.findIndex((item) => item.id === notification.id);
  const existing = existingIndex >= 0 ? current.notifications[existingIndex] : null;

  let unreadCount = current.unreadCount;
  if (existing) {
    const wasUnread = existing.status === 'Unread';
    const isUnread = notification.status === 'Unread';

    if (wasUnread && !isUnread) {
      unreadCount = Math.max(0, unreadCount - 1);
    } else if (!wasUnread && isUnread) {
      unreadCount += 1;
    }
  } else if (unreadCountDelta !== 0) {
    unreadCount = Math.max(0, unreadCount + unreadCountDelta);
  }

  let notifications = current.notifications;
  if (notification.status === 'Dismissed') {
    notifications = existingIndex >= 0
      ? current.notifications.filter((item) => item.id !== notification.id)
      : current.notifications;
  } else {
    notifications = upsertNotifications(current.notifications, notification);
  }

  return {
    ...current,
    notifications,
    unreadCount,
  };
}

function upsertNotifications(notifications: AdminNotification[], notification: AdminNotification): AdminNotification[] {
  const next = notifications.filter((item) => item.id !== notification.id);
  next.unshift(notification);

  next.sort((left, right) => {
    const createdAtComparison = right.createdAt.localeCompare(left.createdAt);
    if (createdAtComparison !== 0) {
      return createdAtComparison;
    }

    return right.id.localeCompare(left.id);
  });

  return next.slice(0, 50);
}

function waitForReconnect(delayMs: number, signal: AbortSignal): Promise<void> {
  return new Promise((resolve) => {
    const timeoutId = window.setTimeout(() => {
      signal.removeEventListener('abort', handleAbort);
      resolve();
    }, delayMs);

    const handleAbort = () => {
      window.clearTimeout(timeoutId);
      signal.removeEventListener('abort', handleAbort);
      resolve();
    };

    signal.addEventListener('abort', handleAbort, { once: true });
  });
}
