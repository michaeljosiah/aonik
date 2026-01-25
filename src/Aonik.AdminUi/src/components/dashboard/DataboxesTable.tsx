import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Eye, ArrowUpDown } from 'lucide-react';
import type { Databox } from '@/types';

interface DataboxesTableProps {
  databoxes: Databox[];
}

export function DataboxesTable({ databoxes }: DataboxesTableProps) {
  return (
    <Card className="h-full flex flex-col">
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-4">
        <div className="flex items-center gap-3">
          <div className="p-2 rounded-md bg-[var(--color-brand-primary)]">
            <svg viewBox="0 0 24 24" className="w-5 h-5 text-white" fill="none" stroke="currentColor" strokeWidth="2">
              <rect x="3" y="3" width="7" height="7" rx="1" />
              <rect x="14" y="3" width="7" height="7" rx="1" />
              <rect x="3" y="14" width="7" height="7" rx="1" />
              <rect x="14" y="14" width="7" height="7" rx="1" />
            </svg>
          </div>
          <div>
            <CardTitle className="text-base font-semibold">My databoxes</CardTitle>
            <p className="text-sm text-[var(--color-text-secondary)]">Your personal and team databoxes.</p>
          </div>
        </div>
        <Button variant="default" size="sm" className="gap-1.5">
          <Eye className="w-4 h-4" />
          View all databoxes
        </Button>
      </CardHeader>
      <CardContent className="flex-1 overflow-hidden">
        {/* Table Header */}
        <div className="flex items-center justify-between py-2 border-b border-[var(--color-border-light)] text-xs text-[var(--color-text-tertiary)] uppercase tracking-wide">
          <div className="flex items-center gap-1 cursor-pointer hover:text-[var(--color-text-secondary)]">
            Databox
            <ArrowUpDown className="w-3 h-3" />
          </div>
          <div className="flex items-center gap-1 cursor-pointer hover:text-[var(--color-text-secondary)]">
            Last Modified
            <ArrowUpDown className="w-3 h-3" />
          </div>
        </div>

        {/* Table Body */}
        <div className="divide-y divide-[var(--color-border-light)] overflow-y-auto max-h-[400px]">
          {databoxes.map((databox) => (
            <div
              key={databox.id}
              className="flex items-center justify-between py-3 hover:bg-[var(--color-background)] -mx-2 px-2 rounded-md cursor-pointer transition-colors"
            >
              <div className="flex items-center gap-3 min-w-0 flex-1">
                <div
                  className="w-1.5 h-10 rounded-full flex-shrink-0"
                  style={{ backgroundColor: databox.color }}
                />
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium text-[var(--color-text-primary)] truncate">
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
