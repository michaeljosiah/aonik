import * as DropdownMenu from '@radix-ui/react-dropdown-menu';
import { MoreVertical } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface DataTableAction {
  label: string;
  icon?: React.ReactNode;
  onClick: () => void;
  variant?: 'default' | 'danger';
  disabled?: boolean;
}

export interface DataTableRowActionsProps {
  actions: DataTableAction[];
  className?: string;
}

export function DataTableRowActions({ actions, className }: DataTableRowActionsProps) {
  if (actions.length === 0) {
    return null;
  }

  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger asChild>
        <button
          className={cn(
            "w-8 h-8 flex items-center justify-center rounded-md text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)] hover:bg-[var(--color-surface-inset)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] transition-colors",
            className
          )}
          aria-label="Row actions"
        >
          <MoreVertical className="w-4 h-4" />
        </button>
      </DropdownMenu.Trigger>

      <DropdownMenu.Portal>
        <DropdownMenu.Content
          className="min-w-[160px] bg-[var(--color-surface)] rounded-lg border border-[var(--color-border-light)] shadow-lg py-1 z-50"
          sideOffset={5}
          align="end"
        >
          {actions.map((action, index) => (
            <DropdownMenu.Item
              key={index}
              disabled={action.disabled}
              onClick={action.onClick}
              className={cn(
                "flex items-center gap-2 px-3 py-2 text-sm cursor-pointer outline-none transition-colors",
                action.variant === 'danger'
                  ? "text-[var(--color-error)] hover:bg-[var(--color-error-light)] focus:bg-[var(--color-error-light)]"
                  : "text-[var(--color-text-primary)] hover:bg-[var(--color-surface-inset)] focus:bg-[var(--color-surface-inset)]",
                action.disabled && "opacity-50 cursor-not-allowed"
              )}
            >
              {action.icon && <span className="w-4 h-4">{action.icon}</span>}
              {action.label}
            </DropdownMenu.Item>
          ))}
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  );
}
