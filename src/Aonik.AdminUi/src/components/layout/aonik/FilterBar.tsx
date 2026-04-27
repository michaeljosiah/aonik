import type { ReactNode } from 'react';
import { Search } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface FilterBarTab {
  value: string;
  label: ReactNode;
  count?: number;
}

export interface FilterBarProps {
  tabs?: FilterBarTab[];
  active?: string;
  onTabChange?: (value: string) => void;
  search?: string;
  searchPlaceholder?: string;
  onSearchChange?: (value: string) => void;
  /** Extra controls rendered between the search input and the trailing Filters button. */
  extra?: ReactNode;
  /** Hide the trailing "Filters" button (defaults to visible). */
  hideFilterButton?: boolean;
}

/**
 * Filter bar — segmented tabs + search input + optional extras. Matches the
 * template's filter row that sits above every list table.
 */
export function FilterBar({
  tabs,
  active,
  onTabChange,
  search = '',
  searchPlaceholder = 'Filter…',
  onSearchChange,
  extra,
  hideFilterButton,
}: FilterBarProps) {
  const activeKey = active ?? tabs?.[0]?.value;

  return (
    <div className="flex items-center gap-2.5 rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)] px-3.5 py-2.5">
      {tabs && tabs.length > 0 && (
        <>
          <div className="flex items-center gap-0.5">
            {tabs.map((t) => {
              const isActive = activeKey === t.value;
              return (
                <button
                  key={t.value}
                  type="button"
                  onClick={() => onTabChange?.(t.value)}
                  className={cn(
                    'inline-flex h-[30px] items-center gap-1.5 rounded-md px-3 text-xs transition-colors',
                    isActive
                      ? 'bg-[var(--color-brand-primary-10)] font-semibold text-[var(--color-brand-primary)]'
                      : 'text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]',
                  )}
                >
                  {t.label}
                  {t.count != null && (
                    <span
                      className={cn(
                        'font-[family-name:var(--font-mono)] text-[10px] font-semibold',
                        isActive ? 'text-[var(--color-brand-secondary)]' : 'text-[var(--color-text-tertiary)]',
                      )}
                    >
                      {t.count}
                    </span>
                  )}
                </button>
              );
            })}
          </div>
          <div className="mx-1 h-5 w-px bg-[var(--color-border-light)]" />
        </>
      )}

      <div className="relative min-w-[180px] flex-1">
        <Search
          className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-[var(--color-text-tertiary)]"
          aria-hidden
        />
        <input
          type="text"
          value={search}
          onChange={(e) => onSearchChange?.(e.target.value)}
          placeholder={searchPlaceholder}
          className="h-[30px] w-full rounded-md border-0 bg-[var(--color-surface-inset)] pl-8 pr-3 text-xs text-[var(--color-text-primary)] placeholder:text-[var(--color-text-tertiary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-brand-primary)]/40"
        />
      </div>

      {extra}

      {!hideFilterButton && (
        <button
          type="button"
          className="inline-flex h-[30px] items-center gap-1.5 rounded-md px-2.5 text-xs text-[var(--color-text-secondary)] transition-colors hover:bg-[var(--color-surface-inset)] hover:text-[var(--color-text-primary)]"
        >
          Filters
        </button>
      )}
    </div>
  );
}
