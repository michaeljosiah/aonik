import { ChevronLeft, ChevronRight } from 'lucide-react';
import { cn } from '@/lib/utils';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

export interface DataTablePaginationProps {
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  pageSizeOptions?: number[];
  className?: string;
}

export function DataTablePagination({
  pageNumber,
  pageSize,
  totalCount,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions = [10, 25, 50, 100],
  className,
}: DataTablePaginationProps) {
  const totalPages = Math.ceil(totalCount / pageSize);
  const startItem = totalCount === 0 ? 0 : (pageNumber - 1) * pageSize + 1;
  const endItem = Math.min(pageNumber * pageSize, totalCount);

  // Generate page numbers to display
  const getPageNumbers = (): (number | 'ellipsis')[] => {
    const pages: (number | 'ellipsis')[] = [];
    
    if (totalPages <= 7) {
      // Show all pages if 7 or fewer
      for (let i = 1; i <= totalPages; i++) {
        pages.push(i);
      }
    } else {
      // Always show first page
      pages.push(1);
      
      if (pageNumber <= 3) {
        // Near the start: 1, 2, 3, ..., last
        pages.push(2, 3, 'ellipsis', totalPages);
      } else if (pageNumber >= totalPages - 2) {
        // Near the end: 1, ..., last-2, last-1, last
        pages.push('ellipsis', totalPages - 2, totalPages - 1, totalPages);
      } else {
        // In the middle: 1, ..., current-1, current, current+1, ..., last
        pages.push('ellipsis', pageNumber - 1, pageNumber, pageNumber + 1, 'ellipsis', totalPages);
      }
    }
    
    return pages;
  };

  const pageNumbers = getPageNumbers();
  const canGoPrevious = pageNumber > 1;
  const canGoNext = pageNumber < totalPages;

  if (totalCount === 0) {
    return null;
  }

  return (
    <div className={cn(
      "flex items-center justify-between px-4 py-3 border-t border-[var(--color-border-light)]",
      className
    )}>
      {/* Left side: Showing X-Y of Z + Items per page */}
      <div className="flex items-center gap-4">
        <span className="text-sm text-[var(--color-text-secondary)]">
          Showing{' '}
          <span className="font-medium text-[var(--color-text-primary)]">{startItem}-{endItem}</span>
          {' '}of{' '}
          <span className="font-medium text-[var(--color-text-primary)]">{totalCount}</span>
        </span>
        
        <div className="flex items-center gap-2">
          <span className="text-sm text-[var(--color-text-secondary)]">Items per page</span>
          <Select value={String(pageSize)} onValueChange={(value) => onPageSizeChange(Number(value))}>
            <SelectTrigger aria-label="Items per page" className="h-8 w-[96px] rounded-md">
              <SelectValue placeholder={String(pageSize)} />
            </SelectTrigger>
            <SelectContent>
              {pageSizeOptions.map((size) => (
                <SelectItem key={size} value={String(size)}>
                  {size}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      {/* Right side: Pagination controls */}
      <div className="flex items-center gap-1">
        {/* Previous button */}
        <button
          onClick={() => onPageChange(pageNumber - 1)}
          disabled={!canGoPrevious}
          className={cn(
            "flex items-center gap-1 px-2 py-1.5 text-sm transition-colors",
            canGoPrevious
              ? "text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]"
              : "text-[var(--color-text-tertiary)] cursor-not-allowed"
          )}
        >
          {canGoPrevious && <ChevronLeft className="w-4 h-4" />}
          <span>Previous</span>
        </button>

        {/* Page numbers */}
        <div className="flex items-center gap-0.5 mx-1">
          {pageNumbers.map((page, index) => {
            if (page === 'ellipsis') {
              return (
                <span
                  key={`ellipsis-${index}`}
                  className="w-8 h-8 flex items-center justify-center text-sm text-[var(--color-text-tertiary)]"
                >
                  ...
                </span>
              );
            }

            const isActive = page === pageNumber;
            return (
              <button
                key={page}
                onClick={() => onPageChange(page)}
                className={cn(
                  "min-w-[32px] h-8 px-2 flex items-center justify-center text-sm rounded transition-colors",
                  isActive
                    ? "bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] font-medium"
                    : "text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)] hover:bg-[var(--color-surface-inset)]"
                )}
              >
                {page}
              </button>
            );
          })}
        </div>

        {/* Next button */}
        <button
          onClick={() => onPageChange(pageNumber + 1)}
          disabled={!canGoNext}
          className={cn(
            "flex items-center gap-1 px-2 py-1.5 text-sm transition-colors",
            canGoNext
              ? "text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]"
              : "text-[var(--color-text-tertiary)] cursor-not-allowed"
          )}
        >
          <span>Next</span>
          {canGoNext && <ChevronRight className="w-4 h-4" />}
        </button>
      </div>
    </div>
  );
}
