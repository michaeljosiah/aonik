import type { ReactNode } from 'react';
import { AlertTriangle, CheckCircle2, Info } from 'lucide-react';

import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';

export type PanelCalloutLevel = 'good' | 'warning' | 'critical' | 'info';

export interface PanelCallout {
  level: PanelCalloutLevel;
  message: ReactNode;
}

export interface PanelInfoPopoverProps {
  title: string;
  description: ReactNode;
  callouts?: PanelCallout[];
}

export function PanelInfoPopover({ title, description, callouts }: PanelInfoPopoverProps) {
  return (
    <Popover>
      <PopoverTrigger asChild>
        <button
          type="button"
          aria-label={`About ${title}`}
          className="inline-flex h-5 w-5 items-center justify-center rounded-full text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)] transition-colors"
        >
          <Info className="w-3.5 h-3.5" />
        </button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-96 max-h-[32rem] overflow-y-auto">
        <div className="space-y-3">
          <h3 className="text-sm font-semibold text-[var(--color-text-primary)]">{title}</h3>
          <div className="text-xs leading-relaxed text-[var(--color-text-secondary)] space-y-2 [&_strong]:text-[var(--color-text-primary)] [&_strong]:font-semibold [&_ul]:list-disc [&_ul]:pl-4 [&_ul]:space-y-1">
            {description}
          </div>
          {callouts && callouts.length > 0 && (
            <div className="pt-2 border-t border-[var(--color-border-light)] space-y-1.5">
              <p className="text-[10px] uppercase tracking-wide font-medium text-[var(--color-text-tertiary)]">
                What your data shows
              </p>
              {callouts.map((c, i) => (
                <Callout key={i} level={c.level}>
                  {c.message}
                </Callout>
              ))}
            </div>
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}

function Callout({ level, children }: { level: PanelCalloutLevel; children: ReactNode }) {
  const Icon = level === 'good' ? CheckCircle2 : level === 'info' ? Info : AlertTriangle;
  const iconColor =
    level === 'good'
      ? 'text-emerald-500'
      : level === 'critical'
        ? 'text-red-500'
        : level === 'warning'
          ? 'text-amber-500'
          : 'text-[var(--color-brand-primary)]';
  return (
    <div className="flex items-start gap-1.5 text-[11px] text-[var(--color-text-secondary)] [&_strong]:text-[var(--color-text-primary)] [&_strong]:font-semibold">
      <Icon className={cn('w-3 h-3 mt-0.5 shrink-0', iconColor)} />
      <span>{children}</span>
    </div>
  );
}
