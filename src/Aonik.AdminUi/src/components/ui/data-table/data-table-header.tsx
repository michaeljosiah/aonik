import { Search, List, LayoutGrid, ChevronDown } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';

export type ViewMode = 'list' | 'grid';

export interface FilterOption {
  value: string;
  label: string;
}

export interface DataTableHeaderProps {
  searchValue: string;
  onSearchChange: (value: string) => void;
  searchPlaceholder?: string;
  filterValue?: string;
  onFilterChange?: (value: string) => void;
  filterOptions?: FilterOption[];
  filterPlaceholder?: string;
  viewMode?: ViewMode;
  onViewModeChange?: (mode: ViewMode) => void;
  showViewToggle?: boolean;
  showSearch?: boolean;
  actions?: React.ReactNode;
  className?: string;
}

export function DataTableHeader({
  searchValue,
  onSearchChange,
  searchPlaceholder = 'Search...',
  filterValue = '',
  onFilterChange,
  filterOptions = [],
  filterPlaceholder = 'Filter',
  viewMode = 'list',
  onViewModeChange,
  showViewToggle = true,
  showSearch = true,
  actions,
  className,
}: DataTableHeaderProps) {
  return (
    <div className={cn(
      "flex items-center justify-between gap-4 px-4 py-3 border-b border-[var(--color-border-light)]",
      className
    )}>
      {/* Left side: Search and Filter */}
      <div className="flex items-center gap-6 flex-1">
        {/* Search input */}
        {showSearch && (
          <div className="relative w-64">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--color-text-tertiary)]" />
            <input
              type="text"
              value={searchValue}
              onChange={(e) => onSearchChange(e.target.value)}
              placeholder={searchPlaceholder}
              className="w-full pl-10 pr-4 py-2 text-sm rounded-sm border border-[var(--color-border)] bg-transparent text-[var(--color-text-primary)] placeholder:text-[var(--color-text-tertiary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-brand-primary)] focus:border-[var(--color-brand-primary)]"
            />
          </div>
        )}

        {/* Filter dropdown */}
        {filterOptions.length > 0 && onFilterChange && (
          <div className="relative inline-flex items-center">
            <select
              value={filterValue}
              onChange={(e) => onFilterChange(e.target.value)}
              className="appearance-none h-9 pl-3 pr-9 text-sm rounded-sm border border-[var(--color-border-light)] bg-[var(--color-surface)] text-[var(--color-text-primary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-brand-primary)] focus:border-[var(--color-brand-primary)] cursor-pointer"
              aria-label={filterPlaceholder}
            >
              <option value="" className="bg-[var(--color-surface)] text-[var(--color-text-primary)]">
                {filterPlaceholder}
              </option>
              {filterOptions.map((option) => (
                <option
                  key={option.value}
                  value={option.value}
                  className="bg-[var(--color-surface)] text-[var(--color-text-primary)]"
                >
                  {option.label}
                </option>
              ))}
            </select>
            <ChevronDown className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--color-text-tertiary)] pointer-events-none" />
          </div>
        )}
      </div>

      {/* Right side: Actions and View Toggle */}
      <div className="flex items-center gap-3">
        {actions}

        {showViewToggle && onViewModeChange && (
          <div className="flex items-center gap-1">
            <Button
              variant="ghost"
              size="icon-sm"
              onClick={() => onViewModeChange('list')}
              className={cn(
                "rounded-md",
                viewMode === 'list'
                  ? "text-[var(--color-text-primary)]"
                  : "text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)]"
              )}
              title="List view"
            >
              <List className="w-5 h-5" />
            </Button>
            <Button
              variant="ghost"
              size="icon-sm"
              onClick={() => onViewModeChange('grid')}
              className={cn(
                "rounded-md",
                viewMode === 'grid'
                  ? "text-[var(--color-text-primary)]"
                  : "text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)]"
              )}
              title="Grid view"
            >
              <LayoutGrid className="w-5 h-5" />
            </Button>
          </div>
        )}
      </div>
    </div>
  );
}
