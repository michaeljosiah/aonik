import { Home, Bell, Copy, Maximize2, Sun, Moon, Monitor } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useTheme } from '@/contexts';
import { useState, useRef, useEffect } from 'react';

interface HeaderProps {
  title?: string;
  breadcrumb?: string[];
}

export function Header({ breadcrumb = ['My Space'] }: HeaderProps) {
  const { theme, resolvedTheme, setTheme } = useTheme();
  const [showThemeMenu, setShowThemeMenu] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  // Close menu when clicking outside
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) {
        setShowThemeMenu(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const themeOptions = [
    { value: 'light' as const, label: 'Light', icon: Sun },
    { value: 'dark' as const, label: 'Dark', icon: Moon },
    { value: 'system' as const, label: 'System', icon: Monitor },
  ];

  const CurrentIcon = resolvedTheme === 'dark' ? Moon : Sun;

  return (
    <header className="flex items-center justify-between h-14 px-6 bg-[var(--color-surface)] border-b border-[var(--color-border-light)]">
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
        {/* Theme Toggle */}
        <div className="relative" ref={menuRef}>
          <Button
            variant="ghost"
            size="icon-sm"
            className="text-[var(--color-text-secondary)]"
            onClick={() => setShowThemeMenu(!showThemeMenu)}
            title={`Current theme: ${theme}`}
          >
            <CurrentIcon className="w-4 h-4" />
          </Button>
          
          {showThemeMenu && (
            <div className="absolute right-0 top-full mt-1 w-36 py-1 bg-[var(--color-surface)] border border-[var(--color-border)] rounded-lg shadow-lg z-50">
              {themeOptions.map((option) => {
                const Icon = option.icon;
                const isActive = theme === option.value;
                return (
                  <button
                    key={option.value}
                    onClick={() => {
                      setTheme(option.value);
                      setShowThemeMenu(false);
                    }}
                    className={`w-full flex items-center gap-2 px-3 py-2 text-sm transition-colors ${
                      isActive
                        ? 'bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)]'
                        : 'text-[var(--color-text-secondary)] hover:bg-[var(--color-sidebar-hover)]'
                    }`}
                  >
                    <Icon className="w-4 h-4" />
                    <span>{option.label}</span>
                    {isActive && (
                      <span className="ml-auto text-[var(--color-brand-primary)]">✓</span>
                    )}
                  </button>
                );
              })}
            </div>
          )}
        </div>

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
