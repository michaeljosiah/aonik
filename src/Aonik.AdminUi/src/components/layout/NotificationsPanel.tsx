import { useMemo } from 'react';
import {
  Bell,
  Search,
  Sparkles,
  Database,
  FileText,
  X,
  Trash2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

type NotificationGroup = 'Today' | 'Yesterday';
type NotificationKind = 'insights' | 'data' | 'docs';

interface NotificationItem {
  id: string;
  group: NotificationGroup;
  kind: NotificationKind;
  sourceLabel: string;
  timeLabel: string;
  title: string;
  description: string;
  showAction?: boolean;
}

interface NotificationsPanelProps {
  open: boolean;
  onClose: () => void;
}

const kindConfig: Record<NotificationKind, { icon: React.ElementType; tone: string; bg: string }> = {
  insights: {
    icon: Sparkles,
    tone: 'text-[var(--color-brand-primary)]',
    bg: 'bg-[var(--color-brand-primary-light)]',
  },
  data: {
    icon: Database,
    tone: 'text-[var(--color-info)]',
    bg: 'bg-[var(--color-info-light)]',
  },
  docs: {
    icon: FileText,
    tone: 'text-[var(--color-pending)]',
    bg: 'bg-[var(--color-pending-light)]',
  },
};

export function NotificationsPanel({ open, onClose }: NotificationsPanelProps) {
  const notifications = useMemo<NotificationItem[]>(
    () => [
      {
        id: 'n1',
        group: 'Today',
        kind: 'insights',
        sourceLabel: 'Smart Insights',
        timeLabel: 'now',
        title: 'Notification title',
        description:
          'Evaluates fund performance across time periods, highlighting key contributors and deviations from benchmarks.',
        showAction: true,
      },
      {
        id: 'n2',
        group: 'Today',
        kind: 'data',
        sourceLabel: 'Smart Data',
        timeLabel: '2h ago',
        title: 'Notification title',
        description:
          'Evaluates fund performance across time periods, highlighting key contributors and deviations from benchmarks.',
      },
      {
        id: 'n3',
        group: 'Today',
        kind: 'docs',
        sourceLabel: 'Smart Docs',
        timeLabel: '3h ago',
        title: 'Notification title',
        description:
          'Evaluates fund performance across time periods, highlighting key contributors and deviations from benchmarks.',
        showAction: true,
      },
      {
        id: 'n4',
        group: 'Yesterday',
        kind: 'insights',
        sourceLabel: 'Smart Insights',
        timeLabel: 'Yesterday',
        title: 'Notification title',
        description:
          'Evaluates fund performance across time periods, highlighting key contributors and deviations from benchmarks.',
        showAction: true,
      },
    ],
    []
  );

  const grouped = useMemo(() => {
    const byGroup = new Map<NotificationGroup, NotificationItem[]>();
    for (const item of notifications) {
      const list = byGroup.get(item.group) ?? [];
      list.push(item);
      byGroup.set(item.group, list);
    }
    return byGroup;
  }, [notifications]);

  if (!open) return null;

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
        className="fixed right-0 top-0 h-full w-[380px] max-w-[92vw] bg-[var(--color-surface)] border-l border-[var(--color-border-light)] shadow-xl flex flex-col"
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

        <div className="p-4 border-b border-[var(--color-border-light)] flex items-center justify-between">
          <div className="flex items-center gap-3">
            <span className="text-xs text-[var(--color-text-tertiary)]">Sort by</span>
            <div className="inline-flex rounded-md border border-[var(--color-border-light)] overflow-hidden">
              <button
                type="button"
                className="px-3 h-8 text-xs bg-[var(--color-brand-primary)] text-white"
              >
                Date
              </button>
              <button
                type="button"
                className="px-3 h-8 text-xs text-[var(--color-text-secondary)] bg-[var(--color-surface)] hover:bg-[var(--color-surface-inset)]"
              >
                Sender
              </button>
            </div>
          </div>

          <Button variant="ghost" size="icon-sm" className="text-[var(--color-text-secondary)]">
            <Search className="w-4 h-4" />
          </Button>
        </div>

        <div className="flex-1 overflow-auto p-4">
          {(['Today', 'Yesterday'] as NotificationGroup[]).map((group) => {
            const items = grouped.get(group) ?? [];
            if (items.length === 0) return null;

            return (
              <div key={group} className="mb-6">
                <div className="flex items-center justify-between mb-3">
                  <h3 className="text-sm font-semibold text-[var(--color-text-primary)]">{group}</h3>
                  <button
                    type="button"
                    className="text-xs text-[var(--color-text-tertiary)] hover:text-[var(--color-text-secondary)]"
                  >
                    Clear
                  </button>
                </div>

                <div className="space-y-3">
                  {items.map((item) => {
                    const config = kindConfig[item.kind];
                    const Icon = config.icon;
                    return (
                      <div
                        key={item.id}
                        className={cn(
                          'rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4 shadow-sm',
                          'transition-colors'
                        )}
                      >
                        <div className="flex items-start justify-between">
                          <div className="flex items-center gap-2">
                            <div className={cn('w-7 h-7 rounded-md flex items-center justify-center', config.bg)}>
                              <Icon className={cn('w-4 h-4', config.tone)} />
                            </div>
                            <span className="text-xs text-[var(--color-text-secondary)]">{item.sourceLabel}</span>
                          </div>
                          <span className="text-xs text-[var(--color-text-tertiary)]">{item.timeLabel}</span>
                        </div>

                        <div className="mt-2">
                          <h4 className="text-sm font-semibold text-[var(--color-text-primary)]">{item.title}</h4>
                          <p className="mt-1 text-xs text-[var(--color-text-secondary)] leading-5">
                            {item.description}
                          </p>
                        </div>

                        <div className="mt-3 flex items-center justify-between">
                          <button
                            type="button"
                            className="text-xs text-[var(--color-text-tertiary)] hover:text-[var(--color-text-secondary)]"
                          >
                            Dismiss
                          </button>
                          {item.showAction && (
                            <Button size="sm" className="rounded-sm h-7 px-3 text-xs">
                              Action
                            </Button>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            );
          })}
        </div>

        <div className="p-4 border-t border-[var(--color-border-light)] flex justify-end">
          <Button variant="outline" size="sm" className="rounded-sm">
            <Trash2 className="w-4 h-4 mr-2" />
            Clear all
          </Button>
        </div>
      </aside>
    </div>
  );
}
