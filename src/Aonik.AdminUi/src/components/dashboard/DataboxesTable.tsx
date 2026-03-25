import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Eye, ArrowUpDown, UserRound } from 'lucide-react';
import type { Databox } from '@/types';

interface DataboxesTableProps {
  databoxes: Databox[];
}

export function DataboxesTable({ databoxes }: DataboxesTableProps) {
  return (
    <Card className="h-full rounded-[4px] flex flex-col px-4 py-3">
      <div className="mb-4 flex flex-row items-center justify-between space-y-0">
        <div className="flex items-center gap-3">
          <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-[var(--color-brand-primary)]">
            <svg viewBox="0 0 24 24" className="w-5 h-5 text-white" fill="none" stroke="currentColor" strokeWidth="2">
              <rect x="3" y="3" width="7" height="7" rx="1" />
              <rect x="14" y="3" width="7" height="7" rx="1" />
              <rect x="3" y="14" width="7" height="7" rx="1" />
              <rect x="14" y="14" width="7" height="7" rx="1" />
            </svg>
          </div>
          <div>
            <h2 className="text-[18px] font-bold text-[var(--color-text-primary)]">My databoxes</h2>
            <p className="text-sm text-[var(--color-text-secondary)]">Your personal and team databoxes.</p>
          </div>
        </div>
        <Button variant="default" size="sm" className="gap-1.5">
          <Eye className="w-4 h-4" />
          View all databoxes
        </Button>
      </div>
      <CardContent className="flex-1 overflow-hidden px-0 pb-0">
        <div className="flex items-center justify-between rounded-[2px] bg-[var(--color-surface-inset)] px-4 py-2 text-[10px] font-semibold uppercase tracking-[0.08em] text-[var(--color-text-secondary)]">
          <div className="flex items-center gap-1 cursor-pointer hover:text-[var(--color-text-primary)]">
            Databox
            <ArrowUpDown className="w-3 h-3" />
          </div>
          <div className="flex items-center gap-1 cursor-pointer hover:text-[var(--color-text-primary)]">
            Last Modified
            <ArrowUpDown className="w-3 h-3" />
          </div>
        </div>

        <div className="visible-scrollbar divide-y divide-[var(--color-border-light)] overflow-y-auto max-h-[400px]">
          {databoxes.map((databox) => (
            <div
              key={databox.id}
              className="flex items-center justify-between px-3 py-3 hover:bg-[var(--color-background)] cursor-pointer transition-colors"
            >
              <div className="flex items-center gap-3 min-w-0 flex-1">
                <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-[color-mix(in_srgb,var(--color-background)_65%,transparent)]">
                  <UserRound className="h-4 w-4" style={{ color: databox.color }} />
                </div>
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-semibold text-[var(--color-text-primary)] truncate">
                    {databox.name}
                  </p>
                  <p className="text-xs text-[var(--color-text-secondary)] truncate">
                    {databox.description}
                  </p>
                </div>
              </div>
              <div className="text-right flex-shrink-0 ml-4">
                <p className="text-sm text-[var(--color-text-primary)]">{databox.lastModified}</p>
                <p className="text-xs text-[var(--color-text-tertiary)]">by {databox.modifiedBy}</p>
              </div>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
