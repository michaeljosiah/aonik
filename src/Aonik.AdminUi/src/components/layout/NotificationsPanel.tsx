import { useMemo, useState } from 'react';
import {
  AlertTriangle,
  AtSign,
  Bell,
  CheckCheck,
  CheckCircle2,
  Database,
  Info,
  Loader2,
  MessageSquare,
  Sparkles,
} from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty';
import { Sheet, SheetBody, SheetContent, SheetHeader } from '@/components/ui/sheet';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { cn } from '@/lib/utils';
import { AgentAvatar } from '@/components/ai/AgentAvatar';
import { agentDisplayName, notificationAgentName } from '@/lib/agentIdentity';
import { chatNotificationContext, isChatNotification } from '@/lib/chatNotifications';
import { HumanAvatar } from '@/components/ai/HumanAvatar';
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

const severityConfig: Record<string, { icon: React.ElementType; tone: string }> = {
  Info: { icon: Info, tone: 'text-info' },
  Success: { icon: CheckCircle2, tone: 'text-success' },
  Warning: { icon: AlertTriangle, tone: 'text-warning' },
  Error: { icon: AlertTriangle, tone: 'text-destructive' },
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
  const [actionError, setActionError] = useState('');
  const [filter, setFilter] = useState<'all' | 'unread' | 'mentions' | 'chat'>('all');

  const visible = useMemo(
    () => notifications.filter(item => filter === 'all' || (filter === 'unread' && item.status === 'Unread')
      || (filter === 'mentions' && item.type === 'chat.mention') || (filter === 'chat' && isChatNotification(item))),
    [notifications, filter],
  );

  const grouped = useMemo(() => {
    const byGroup = new Map<string, AdminNotification[]>();

    for (const item of visible) {
      const group = resolveGroupLabel(item.createdAt);
      const list = byGroup.get(group) ?? [];
      list.push(item);
      byGroup.set(group, list);
    }

    return byGroup;
  }, [visible]);

  const handleMarkRead = async (notificationId: string) => {
    setActionError('');
    setPendingIds((current) => [...current, notificationId]);
    try {
      await onMarkRead(notificationId);
      return true;
    } catch {
      setActionError('Could not mark this notification read. Please try again.');
      return false;
    } finally {
      setPendingIds((current) => current.filter((id) => id !== notificationId));
    }
  };

  const handleDismiss = async (notificationId: string) => {
    setActionError('');
    setPendingIds((current) => [...current, notificationId]);
    try {
      await onDismiss(notificationId);
    } catch {
      setActionError('Could not dismiss this notification. Please try again.');
    } finally {
      setPendingIds((current) => current.filter((id) => id !== notificationId));
    }
  };

  const handleMarkAllRead = async () => {
    setActionError('');
    setMarkAllPending(true);
    try {
      await onMarkAllRead();
    } catch {
      setActionError('Could not mark notifications read. Please try again.');
    } finally {
      setMarkAllPending(false);
    }
  };

  const handleAction = async (notification: AdminNotification) => {
    if (notification.status === 'Unread') {
      if (!await handleMarkRead(notification.id)) return;
    }

    if (notification.actionUrl) {
      navigate(notification.actionUrl);
      onClose();
    }
  };

  return (
    <Sheet open={open} onOpenChange={(next) => !next && onClose()}>
      <SheetContent size="sm">
        <SheetHeader
          icon={<Bell />}
          title="Notifications"
          subtitle={`${unreadCount} unread from people, agents and workflows`}
        />

        <div className="flex flex-wrap items-center justify-between gap-3 border-b px-4 py-3">
          <Tabs value={filter} onValueChange={(v) => setFilter(v as 'all' | 'unread')}>
            <TabsList>
              <TabsTrigger value="all">All</TabsTrigger>
              <TabsTrigger value="unread">
                Unread
                {unreadCount > 0 && (
                  <span className="font-mono text-xs tabular-nums text-muted-foreground">{unreadCount}</span>
                )}
              </TabsTrigger>
              <TabsTrigger value="mentions">Mentions</TabsTrigger>
              <TabsTrigger value="chat">Chat</TabsTrigger>
            </TabsList>
          </Tabs>
          <Button
            variant="ghost"
            size="sm"
            disabled={markAllPending || unreadCount === 0}
            onClick={() => void handleMarkAllRead()}
          >
            {markAllPending ? <Loader2 className="animate-spin" /> : <CheckCheck />}
            Mark all read
          </Button>
        </div>

        {actionError && <p role="alert" className="border-b bg-destructive/10 px-4 py-3 text-xs text-destructive">{actionError}</p>}
        <SheetBody className="gap-6 p-2">
          {loading && notifications.length === 0 ? (
            <div className="flex flex-col gap-2 p-2" aria-label="Loading notifications">
              {Array.from({ length: 4 }, (_, i) => (
                <Skeleton key={i} className="h-16 w-full" />
              ))}
            </div>
          ) : visible.length === 0 ? (
            <Empty className="flex-1">
              <EmptyHeader>
                <EmptyMedia variant="icon">
                  <Bell />
                </EmptyMedia>
                <EmptyTitle className="text-base">
                  {filter === 'unread' ? 'You are all caught up' : 'No notifications yet'}
                </EmptyTitle>
                <EmptyDescription>
                  {filter === 'mentions' ? 'Mentions from shared rooms appear here.' : filter === 'chat' ? 'New shared-room messages appear here.' : 'Messages, mentions and system events appear here as they happen.'}
                </EmptyDescription>
              </EmptyHeader>
            </Empty>
          ) : (
            ['Today', 'Yesterday', 'Earlier'].map((group) => {
              const items = grouped.get(group) ?? [];
              if (items.length === 0) return null;

              return (
                <section key={group} className="flex flex-col gap-1">
                  <h3 className="px-2 pt-1 text-xs font-medium text-muted-foreground">{group}</h3>
                  {items.map((item) => {
                    const pending = pendingIds.includes(item.id);
                    const severity = severityConfig[item.severity] ?? severityConfig.Info;
                    const SourceIcon = sourceIcons[item.source] ?? severity.icon;
                    const unread = item.status === 'Unread';
                    const agentName = notificationAgentName(item);
                    const chat = chatNotificationContext(item);
                    const ChatIcon = item.type === 'chat.mention' ? AtSign : MessageSquare;

                    return (
                      <article
                        key={item.id}
                        data-notification-id={item.id}
                        className={cn(
                          'relative flex gap-3 rounded-lg p-3 transition-colors hover:bg-accent/60',
                          unread && 'bg-muted/60',
                        )}
                      >
                        {agentName ? <AgentAvatar name={agentName} /> : chat?.human ? <HumanAvatar name={chat.authorName} /> : <div className="flex size-8 shrink-0 items-center justify-center rounded-md border bg-background">
                          <SourceIcon className={cn('size-4', severity.tone)} />
                        </div>}
                        <div className="flex min-w-0 flex-1 flex-col gap-1">
                          <div className="flex items-center gap-2 text-xs text-muted-foreground">
                            <span className="truncate font-medium">{agentName ? `${agentDisplayName(agentName)} · AI agent` : chat?.human ? `${chat.authorName} · Human` : formatSourceLabel(item.source)}</span>
                            <span className="shrink-0">{formatRelativeTime(item.createdAt)}</span>
                            {pending ? (
                              <Loader2 className="ml-auto size-3.5 shrink-0 animate-spin" />
                            ) : unread ? (
                              <span className="ml-auto size-2 shrink-0 rounded-full bg-primary" aria-label="Unread" />
                            ) : null}
                          </div>
                          <h4 className="text-sm font-medium">{item.title}</h4>
                          {isChatNotification(item) ? <span className="inline-flex items-center gap-1.5 text-xs text-primary"><ChatIcon className="size-3.5 shrink-0" />{item.type === 'chat.mention' ? 'Mention' : 'New message'}{chat && <span className="truncate text-muted-foreground">· {chat.roomTitle}</span>}</span> : agentName && <span className={cn('inline-flex items-center gap-1 text-xs', severity.tone)}><severity.icon className="size-3" />{item.severity}</span>}
                          <p className="whitespace-pre-wrap text-xs leading-5 text-muted-foreground">{item.body}</p>
                          <div className="mt-1 flex items-center gap-1">
                            {item.actionUrl && (
                              <Button size="sm" variant="outline" disabled={pending} onClick={() => void handleAction(item)}>
                                {isChatNotification(item) ? 'Open message' : 'Open'}
                              </Button>
                            )}
                            {unread && (
                              <Button size="sm" variant="ghost" disabled={pending} onClick={() => void handleMarkRead(item.id)}>
                                Mark read
                              </Button>
                            )}
                            <Button size="sm" variant="ghost" disabled={pending} onClick={() => void handleDismiss(item.id)}>
                              Dismiss
                            </Button>
                          </div>
                        </div>
                      </article>
                    );
                  })}
                </section>
              );
            })
          )}
        </SheetBody>
      </SheetContent>
    </Sheet>
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
