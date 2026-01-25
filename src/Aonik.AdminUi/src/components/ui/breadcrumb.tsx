import { ChevronRight } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface BreadcrumbItem {
  label: string;
  href?: string;
  icon?: React.ReactNode;
}

interface BreadcrumbProps {
  items: BreadcrumbItem[];
  className?: string;
}

export function Breadcrumb({ items, className }: BreadcrumbProps) {
  return (
    <nav aria-label="Breadcrumb" className={cn('flex items-center', className)}>
      <ol className="flex items-center gap-1 text-sm">
        {items.map((item, index) => {
          const isLast = index === items.length - 1;

          const labelContent = (
            <span className="inline-flex items-center gap-1.5">
              {item.icon && <span className="text-[var(--color-text-tertiary)]">{item.icon}</span>}
              <span>{item.label}</span>
            </span>
          );
          
          return (
            <li key={index} className="flex items-center gap-1">
              {index > 0 && (
                <ChevronRight className="w-4 h-4 text-[var(--color-text-tertiary)]" />
              )}
              {isLast ? (
                <span className="text-[var(--color-text-secondary)]">
                  {labelContent}
                </span>
              ) : item.href ? (
                <a
                  href={item.href}
                  className="text-[var(--color-text-tertiary)] hover:text-[var(--color-text-secondary)] transition-colors"
                >
                  {labelContent}
                </a>
              ) : (
                <span className="text-[var(--color-text-tertiary)]">
                  {labelContent}
                </span>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
