import { LayoutGridIcon, ListIcon, SearchIcon } from 'lucide-react';

import { cn } from '@/lib/utils';
import { InputGroup, InputGroupAddon, InputGroupInput } from '@/components/ui/input-group';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group';

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
  searchPlaceholder = 'Search…',
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
    <div className={cn('flex flex-wrap items-center justify-between gap-3 border-b px-4 py-3', className)}>
      <div className="flex flex-1 flex-wrap items-center gap-2">
        {showSearch && (
          <InputGroup className="w-full sm:w-64">
            <InputGroupAddon>
              <SearchIcon />
            </InputGroupAddon>
            <InputGroupInput
              type="search"
              aria-label={searchPlaceholder}
              value={searchValue}
              onChange={(e) => onSearchChange(e.target.value)}
              placeholder={searchPlaceholder}
            />
          </InputGroup>
        )}

        {filterOptions.length > 0 && onFilterChange && (
          <Select
            value={filterValue || undefined}
            onValueChange={(value) => onFilterChange(value === '__all__' ? '' : value)}
          >
            <SelectTrigger aria-label={filterPlaceholder} className="w-auto min-w-36">
              <SelectValue placeholder={filterPlaceholder} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="__all__">{filterPlaceholder}</SelectItem>
              {filterOptions.map((option) => (
                <SelectItem key={option.value} value={option.value}>
                  {option.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
      </div>

      <div className="flex items-center gap-2">
        {actions}
        {showViewToggle && onViewModeChange && (
          <ToggleGroup
            type="single"
            variant="outline"
            size="sm"
            value={viewMode}
            onValueChange={(value) => value && onViewModeChange(value as ViewMode)}
            aria-label="View"
          >
            <ToggleGroupItem value="list" aria-label="List view">
              <ListIcon />
            </ToggleGroupItem>
            <ToggleGroupItem value="grid" aria-label="Grid view">
              <LayoutGridIcon />
            </ToggleGroupItem>
          </ToggleGroup>
        )}
      </div>
    </div>
  );
}
