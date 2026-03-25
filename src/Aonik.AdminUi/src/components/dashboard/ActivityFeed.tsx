import { ArrowUpRight, MoreVertical } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import type { ActivityItem } from '@/types';

interface ActivityFeedProps {
  items: ActivityItem[];
}

export function ActivityFeed({ items }: ActivityFeedProps) {
  return (
    <Card className="h-full rounded-[4px] px-4 py-3 flex flex-col overflow-hidden">
      <div className="mb-2 flex items-center justify-between shrink-0">
        <h2 className="text-[18px] font-bold text-[var(--color-text-primary)]">Activity feed</h2>
        <Button variant="ghost" size="icon-sm" className="text-[var(--color-text-tertiary)]">
          <MoreVertical className="w-4 h-4" />
        </Button>
      </div>

      <div className="flex flex-col overflow-y-auto flex-1 visible-scrollbar">
        {items.map((item) => (
          <div
            key={item.id}
            className="flex items-center gap-3 border-b border-[var(--color-border-light)] py-3 last:border-b-0"
          >
            <div className="min-w-0 flex-1">
              <p className="truncate text-[14px] font-semibold text-[var(--color-text-primary)]">
                {item.title}
              </p>
              {item.description ? (
                <p className="truncate text-[12px] text-[var(--color-text-secondary)]">{item.description}</p>
              ) : null}
              <p className="text-[11px] text-[var(--color-text-tertiary)]">{item.timestamp}</p>
            </div>

            <div className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full border border-[var(--color-brand-primary)] text-[var(--color-brand-primary)]">
              <ArrowUpRight className="h-3 w-3" />
            </div>
          </div>
        ))}
      </div>

      <div className="mt-2 flex items-center justify-center gap-2 text-[var(--color-gray-300)] shrink-0">
        <span className="h-[3px] w-[10px] rounded-full bg-[var(--color-gray-300)]" />
        <span className="h-[3px] w-[10px] rounded-full bg-[var(--color-gray-200)]" />
      </div>
    </Card>
  );
}
