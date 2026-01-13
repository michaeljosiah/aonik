import { Home, Bell, Copy, Maximize2 } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface HeaderProps {
  title?: string;
  breadcrumb?: string[];
}

export function Header({ breadcrumb = ['My Space'] }: HeaderProps) {
  return (
    <header className="flex items-center justify-between h-14 px-6 bg-white border-b border-[var(--color-border-light)]">
      {/* Breadcrumb */}
      <nav className="flex items-center gap-2 text-sm">
        <Home className="w-4 h-4 text-[var(--color-text-secondary)]" />
        {breadcrumb.map((item, index) => (
          <span key={item} className="flex items-center gap-2">
            {index > 0 && <span className="text-[var(--color-text-tertiary)]">/</span>}
            <span
              className={
                index === breadcrumb.length - 1
                  ? 'text-[var(--color-text-primary)] font-medium'
                  : 'text-[var(--color-text-secondary)]'
              }
            >
              {item}
            </span>
          </span>
        ))}
      </nav>

      {/* Actions */}
      <div className="flex items-center gap-2">
        <Button variant="ghost" size="icon-sm" className="text-[var(--color-text-secondary)]">
          <Bell className="w-4 h-4" />
        </Button>
        <Button variant="ghost" size="icon-sm" className="text-[var(--color-text-secondary)]">
          <Copy className="w-4 h-4" />
        </Button>
        <Button variant="ghost" size="icon-sm" className="text-[var(--color-text-secondary)]">
          <Maximize2 className="w-4 h-4" />
        </Button>
      </div>
    </header>
  );
}
