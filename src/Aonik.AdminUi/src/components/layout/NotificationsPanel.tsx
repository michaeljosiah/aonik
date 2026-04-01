import { useMemo, useState } from 'react';
import {
  AlertTriangle,
  Bell,
  CheckCheck,
  CheckCircle2,
  Database,
  Info,
  Loader2,
  Sparkles,
  X,
} from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { AdminNotification } from '@/services/notificationService';

interface NotificationsPanelProps {
  open: boolean;
  onClose: () => void;
  notifications: AdminNotification[];
  unreadCount: number;
  loading: boolean;
  onMarkRead: (notificationId: string) => Promise<unknown>;
  onDismiss: (notificationId: string) => Promise<unknown>;
  onMarkAllRead: () => Promise<unknown>;
}

const severityConfig: Record<string, { icon: React.ElementType; tone: string; bg: string }> = {
  Info: {
    icon: Info,
    tone: 'text-[var(--color-info)]',
    bg: 'bg-[var(--color-info-light)]',
  },
  Success: {
    icon: CheckCircle2,
    tone: 'text-[var(--color-success)]',
    bg: 'bg-[var(--color-success-light)]',
  },
  Warning: {
    icon: AlertTriangle,
    tone: 'text-[var(--color-warning)]',
    bg: 'bg-[var(--color-warning-light)]',
  },
  Error: {
    icon: AlertTriangle,
    tone: 'text-[var(--color-error)]',
    bg: 'bg-[var(--color-error-light)]',
  },
};

const sourceIcons: Record<string, React.ElementType> = {
  Agent: Sparkles,
  Scheduler: Database,
};

export function NotificationsPanel({
  open,
  onClose,
  notifications,
  unreadCount,
  loading,
  onMarkRead,
  onDismiss,
  onMarkAllRead,
}: NotificationsPanelProps) {
  const navigate = useNavigate();
  const [pendingIds, setPendingIds] = useState<string[]>([]);
  const [markAllPending, setMarkAllPending] = useState(false);

  const grouped = useMemo(() => {
    const byGroup = new Map<string, AdminNotification[]>();

    for (const item of notifications) {
      const group = resolveGroupLabel(item.createdAt);
      const list = byGroup.get(group) ?? [];
      list.push(item);
      byGroup.set(group, list);
    }

    return byGroup;
  }, [notifications]);

  if (!open) return null;

  const handleMarkRead = async (notificationId: string) => {
    setPendingIds((current) => [...current, notificationId]);
    try {
      await onMarkRead(notificationId);
    } finally {
      setPendingIds((current) => current.filter((id) => id !== notificationId));
    }
  };

  const handleDismiss = async (notificationId: string) => {
    setPendingIds((current) => [...current, notificationId]);
    try {
      await onDismiss(notificationId);
    } finally {
      setPendingIds((current) => current.filter((id) => id !== notificationId));
    }
  };

  const handleMarkAllRead = async () => {
    setMarkAllPending(true);
    try {
      await onMarkAllRead();
    } finally {
      setMarkAllPending(false);
    }
  };

  const handleAction = async (notification: AdminNotification) => {
    if (notification.status === 'Unread') {
      await handleMarkRead(notification.id);
    }

    if (notification.actionUrl) {
      navigate(notification.actionUrl);
      onClose();
    }
  };

  return (
    <div className="fixed inset-0 z-[80]">
      <button
        type="button"
        aria-label="Close notifications"
        className="absolute inset-0 bg-black/10"
        onClick={onClose}
      />

      <aside
        role="dialog"
        aria-label="Notifications"
        className="fixed right-0 top-0 h-full w-[420px] max-w-[94vw] bg-[var(--color-surface)] border-l border-[var(--color-border-light)] shadow-xl flex flex-col"
      >
        <div className="h-14 px-4 bg-[var(--color-brand-primary)] text-white flex items-center justify-between">
          <div className="flex items-center gap-2 font-semibold">
            <Bell className="w-4 h-4" />
            Notifications
          </div>
          <Button
            variant="ghost"
            size="icon-sm"
            className="text-white hover:bg-white/15"
            onClick={onClose}
            aria-label="Close notifications panel"
          >
            <X className="w-4 h-4" />
          </Button>
        </div>

        <div className="p-4 border-b border-[var(--color-border-light)] flex items-center justify-between gap-3">
          <div>
            <p className="text-sm font-semibold text-[var(--color-text-primary)]">
              {unreadCount} unread {unreadCount === 1 ? 'notification' : 'notifications'}
            </p>
            <p className="text-xs text-[var(--color-text-secondary)]">
              Realtime updates from agents, jobs, and system workflows.
            </p>
          </div>

          <Button
            variant="outline"
            size="sm"
            className="rounded-sm"
            disabled={markAllPending || unreadCount === 0}
            onClick={() => void handleMarkAllRead()}
          >
            {markAllPending ? <Loader2 className="w-4 h-4 mr-2 animate-spin" /> : <CheckCheck className="w-4 h-4 mr-2" />}
            Mark all read
          </Button>
        </div>

        <div className="flex-1 overflow-auto p-4">
          {loading && notifications.length === 0 ? (
            <div className="h-full flex items-center justify-center gap-3 text-[var(--color-text-secondary)]">
              <Loader2 className="w-5 h-5 animate-spin" />
              Loading notifications...
            </div>
          ) : notifications.length === 0 ? (
            <div className="h-full flex flex-col items-center justify-center text-center px-8">
              <Bell className="w-10 h-10 text-[var(--color-text-tertiary)] mb-3" />
              <h3 className="text-sm font-semibold text-[var(--color-text-primary)]">No notifications yet</h3>
              <p className="mt-2 text-sm text-[var(--color-text-secondary)]">
                New agent messages and system events will appear here in real time.
              </p>
            </div>
          ) : (
            ['Today', 'Yesterday', 'Earlier'].map((group) => {
              const items = grouped.get(group) ?? [];
              if (items.length === 0) return null;

              return (
                <div key={group} className="mb-6">
                  <div className="flex items-center justify-between mb-3">
                    <h3 className="text-sm font-semibold text-[var(--color-text-primary)]">{group}</h3>
                    <span className="text-xs text-[var(--color-text-tertiary)]">{items.length}</span>
                  </div>

                  <div className="space-y-3">
                    {items.map((item) => {
                      const pending = pendingIds.includes(item.id);
                      const severity = severityConfig[item.severity] ?? severityConfig.Info;
                      const SourceIcon = sourceIcons[item.source] ?? severity.icon;

                      return (
                        <div
                          key={item.id}
                          className={cn(
                            'rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4 shadow-sm transition-colors',
                            item.status === 'Unread' && 'bg-[var(--color-surface-inset)]'
                          )}
                        >
                          <div className="flex items-start justify-between gap-3">
                            <div className="flex items-center gap-2 min-w-0">
                              <div className={cn('w-8 h-8 rounded-md flex items-center justify-center', severity.bg)}>
                                <SourceIcon className={cn('w-4 h-4', severity.tone)} />
                              </div>
                              <div className="min-w-0">
                                <div className="flex items-center gap-2">
                                  <span className="text-xs font-medium text-[var(--color-text-secondary)] truncate">
                                    {formatSourceLabel(item.source)}
                                  </span>
                                  {item.status === 'Unread' && <span className="w-2 h-2 rounded-full bg-[var(--color-brand-primary)]" />}
                                </div>
                                <span className="text-xs text-[var(--color-text-tertiary)]">{formatRelativeTime(item.createdAt)}</span>
                              </div>
                            </div>
                            {pending && <Loader2 className="w-4 h-4 animate-spin text-[var(--color-text-tertiary)] flex-shrink-0" />}
                          </div>

                          <div className="mt-3">
                            <h4 className="text-sm font-semibold text-[var(--color-text-primary)]">{item.title}</h4>
                            <p className="mt-1 text-xs text-[var(--color-text-secondary)] leading-5 whitespace-pre-wrap">
                              {item.body}
                            </p>
                          </div>

                          <div className="mt-4 flex items-center justify-between gap-3">
                            <div className="flex items-center gap-3">
                              {item.status === 'Unread' && (
                                <button
                                  type="button"
                                  className="text-xs text-[var(--color-text-tertiary)] hover:text-[var(--color-text-secondary)] disabled:opacity-50"
                                  disabled={pending}
                                  onClick={() => void handleMarkRead(item.id)}
                                >
                                  Mark read
                                </button>
                              )}
                              <button
                                type="button"
                                className="text-xs text-[var(--color-text-tertiary)] hover:text-[var(--color-text-secondary)] disabled:opacity-50"
                                disabled={pending}
                                onClick={() => void handleDismiss(item.id)}
                              >
                                Dismiss
                              </button>
                            </div>

                            {item.actionUrl && (
                              <Button
                                size="sm"
                                className="rounded-sm h-7 px-3 text-xs"
                                disabled={pending}
                                onClick={() => void handleAction(item)}
                              >
                                Open
                              </Button>
                            )}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              );
            })
          )}
        </div>
      </aside>
    </div>
  );
}

function resolveGroupLabel(value: string): string {
  const date = new Date(value);
  const today = new Date();
  const startOfToday = new Date(today.getFullYear(), today.getMonth(), today.getDate());
  const startOfYesterday = new Date(startOfToday);
  startOfYesterday.setDate(startOfYesterday.getDate() - 1);

  if (date >= startOfToday) {
    return 'Today';
  }

  if (date >= startOfYesterday) {
    return 'Yesterday';
  }

  return 'Earlier';
}

function formatRelativeTime(value: string): string {
  const date = new Date(value);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMinutes = Math.floor(Math.abs(diffMs) / 60000);

  if (diffMinutes < 1) {
    return 'just now';
  }

  if (diffMinutes < 60) {
    return `${diffMinutes}m ago`;
  }

  const diffHours = Math.floor(diffMinutes / 60);
  if (diffHours < 24) {
    return `${diffHours}h ago`;
  }

  const diffDays = Math.floor(diffHours / 24);
  return `${diffDays}d ago`;
}

function formatSourceLabel(source: string): string {
  if (source === 'AzureMonitor') {
    return 'Azure Monitor';
  }

  return source.replace(/([a-z])([A-Z])/g, '$1 $2');
}
