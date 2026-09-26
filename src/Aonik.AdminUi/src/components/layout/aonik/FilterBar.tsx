import type { ReactNode } from 'react';
import { SearchIcon } from 'lucide-react';
import { InputGroup, InputGroupAddon, InputGroupInput } from '@/components/ui/input-group';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';

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
  /** Extra controls rendered after the search input. */
  extra?: ReactNode;
}

/**
 * Toolbar above a list: status tabs with counts, search, and extra controls.
 * No card chrome; it sits directly on the page (Spec 098 §7.5).
 */
export function FilterBar({
  tabs,
  active,
  onTabChange,
  search = '',
  searchPlaceholder = 'Filter…',
  onSearchChange,
  extra,
}: FilterBarProps) {
  const activeKey = active ?? tabs?.[0]?.value;

  return (
    <div className="flex flex-wrap items-center gap-2">
      {tabs && tabs.length > 0 && (
        <Tabs value={activeKey} onValueChange={(value) => onTabChange?.(value)}>
          <TabsList>
            {tabs.map((t) => (
              <TabsTrigger key={t.value} value={t.value} className="px-2.5">
                {t.label}
                {t.count != null && (
                  <span className="rounded-sm bg-muted px-1 font-mono text-xs tabular-nums text-muted-foreground">
                    {t.count}
                  </span>
                )}
              </TabsTrigger>
            ))}
          </TabsList>
        </Tabs>
      )}

      <InputGroup className="min-w-[180px] flex-1 sm:max-w-xs">
        <InputGroupAddon>
          <SearchIcon />
        </InputGroupAddon>
        <InputGroupInput
          type="search"
          aria-label={searchPlaceholder}
          value={search}
          onChange={(e) => onSearchChange?.(e.target.value)}
          placeholder={searchPlaceholder}
        />
      </InputGroup>

      {extra}
    </div>
  );
}
