import { cn } from '@/lib/utils';
import { Checkbox } from '@/components/ui/checkbox';
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty';
import { Skeleton } from '@/components/ui/skeleton';

export interface DataTableGridViewProps<T> {
  data: T[];
  getRowId: (row: T) => string;
  renderCard: (row: T) => React.ReactNode;
  selectedIds?: Set<string>;
  onSelectionChange?: (selectedIds: Set<string>) => void;
  showCheckboxes?: boolean;
  loading?: boolean;
  loadingMessage?: string;
  emptyIcon?: React.ReactNode;
  emptyTitle?: string;
  emptyDescription?: string;
  columns?: number;
  className?: string;
}

const gridColumns: Record<number, string> = {
  2: 'grid-cols-1 sm:grid-cols-2',
  3: 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3',
  4: 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4',
};

export function DataTableGridView<T>({
  data,
  getRowId,
  renderCard,
  selectedIds = new Set(),
  onSelectionChange,
  showCheckboxes = true,
  loading = false,
  loadingMessage = 'Loading…',
  emptyIcon,
  emptyTitle = 'Nothing here yet',
  emptyDescription,
  columns = 3,
  className,
}: DataTableGridViewProps<T>) {
  const handleSelectRow = (rowId: string, checked: boolean) => {
    if (!onSelectionChange) return;
    const next = new Set(selectedIds);
    if (checked) next.add(rowId);
    else next.delete(rowId);
    onSelectionChange(next);
  };

  if (loading) {
    return (
      <div className={cn('grid gap-4 p-4', gridColumns[columns], className)}>
        <span className="sr-only">{loadingMessage}</span>
        {Array.from({ length: columns * 2 }, (_, i) => (
          <Skeleton key={i} className="h-28 rounded-xl" />
        ))}
      </div>
    );
  }

  if (data.length === 0) {
    return (
      <Empty className={cn('py-10', className)}>
        <EmptyHeader>
          {emptyIcon && <EmptyMedia variant="icon">{emptyIcon}</EmptyMedia>}
          <EmptyTitle className="text-base">{emptyTitle}</EmptyTitle>
          {emptyDescription && <EmptyDescription>{emptyDescription}</EmptyDescription>}
        </EmptyHeader>
      </Empty>
    );
  }

  return (
    <div className={cn('grid gap-4 p-4', gridColumns[columns], className)}>
      {data.map((row) => {
        const rowId = getRowId(row);
        const isSelected = selectedIds.has(rowId);
        return (
          <div
            key={rowId}
            data-state={isSelected ? 'selected' : undefined}
            className="relative rounded-xl border bg-card p-4 text-card-foreground shadow-xs transition-colors data-[state=selected]:border-primary data-[state=selected]:bg-primary/5"
          >
            {showCheckboxes && onSelectionChange && (
              <Checkbox
                aria-label="Select item"
                className="absolute top-4 left-4"
                checked={isSelected}
                onCheckedChange={(checked) => handleSelectRow(rowId, checked === true)}
              />
            )}
            <div className={showCheckboxes && onSelectionChange ? 'pl-7' : undefined}>{renderCard(row)}</div>
          </div>
        );
      })}
    </div>
  );
}
