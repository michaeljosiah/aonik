import { Home, Bell, Copy, Maximize2, Minimize2, Sun, Moon, Monitor, Building2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useTheme } from '@/contexts';
import { useState, useRef, useEffect } from 'react';
import { getSelectedTenant } from '@/lib/tenantContext';
import { NotificationsPanel } from '@/components/layout/NotificationsPanel';

interface HeaderProps {
  title?: string;
  breadcrumb?: string[];
  leftSlot?: React.ReactNode;
}

export function Header({ breadcrumb = ['My Space'], leftSlot }: HeaderProps) {
  const { theme, resolvedTheme, setTheme } = useTheme();
  const [showThemeMenu, setShowThemeMenu] = useState(false);
  const [showNotifications, setShowNotifications] = useState(false);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const [tenantLabel, setTenantLabel] = useState<string | null>(null);
  const [tenantEnv, setTenantEnv] = useState<string | null>(null);

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

  useEffect(() => {
    const selected = getSelectedTenant();
    if (!selected) return;

    const name = selected.name?.trim();
    setTenantLabel(name && name.length > 0 ? name : null);
    setTenantEnv(selected.environment?.trim() || null);
  }, []);

  // Track fullscreen state changes
  useEffect(() => {
    const handleFullscreenChange = () => {
      setIsFullscreen(!!document.fullscreenElement);
    };

    document.addEventListener('fullscreenchange', handleFullscreenChange);
    return () => document.removeEventListener('fullscreenchange', handleFullscreenChange);
  }, []);

  const toggleFullscreen = async () => {
    try {
      if (!document.fullscreenElement) {
        await document.documentElement.requestFullscreen();
      } else {
        await document.exitFullscreen();
      }
    } catch (error) {
      console.error('Fullscreen toggle failed:', error);
    }
  };

  const themeOptions = [
    { value: 'light' as const, label: 'Light', icon: Sun },
    { value: 'dark' as const, label: 'Dark', icon: Moon },
    { value: 'system' as const, label: 'System', icon: Monitor },
  ];

  const CurrentIcon = resolvedTheme === 'dark' ? Moon : Sun;

  return (
    <>
    <header className="flex items-center justify-between h-14 px-6 bg-[var(--color-surface)] border-b border-[var(--color-border-light)]">
      {/* Breadcrumb / Left Slot */}
      <nav className="flex items-center gap-2 text-sm min-w-0">
        {leftSlot ? (
          leftSlot
        ) : (
          <>
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
          </>
        )}

        {tenantLabel && (
          <span className="ml-3 flex items-center gap-2 px-2 py-1 rounded-md bg-[var(--color-surface-elevated)] border border-[var(--color-border-light)]">
            <Building2 className="w-4 h-4 text-[var(--color-text-tertiary)]" />
            <span className="text-xs text-[var(--color-text-tertiary)]">Tenant</span>
            <span className="text-xs font-medium text-[var(--color-text-primary)] truncate max-w-[14rem]">{tenantLabel}</span>
            {tenantEnv && tenantEnv !== 'Prod' && (
              <span className="text-[10px] px-1.5 py-0.5 rounded-md bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)]">
                {tenantEnv}
              </span>
            )}
          </span>
        )}
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
            <div className="absolute right-0 top-full mt-1 w-36 py-1 bg-[var(--color-surface)] border border-[var(--color-border)] rounded-md shadow-lg z-50">
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

        <Button
          variant="ghost"
          size="icon-sm"
          className="text-[var(--color-text-secondary)]"
          onClick={() => setShowNotifications(true)}
          aria-label="Open notifications"
        >
          <Bell className="w-4 h-4" />
        </Button>
        <Button variant="ghost" size="icon-sm" className="text-[var(--color-text-secondary)]">
          <Copy className="w-4 h-4" />
        </Button>
        <Button
          variant="ghost"
          size="icon-sm"
          className="text-[var(--color-text-secondary)]"
          onClick={toggleFullscreen}
          title={isFullscreen ? 'Exit fullscreen' : 'Enter fullscreen'}
        >
          {isFullscreen ? <Minimize2 className="w-4 h-4" /> : <Maximize2 className="w-4 h-4" />}
        </Button>
      </div>
    </header>
    <NotificationsPanel open={showNotifications} onClose={() => setShowNotifications(false)} />
    </>
  );
}
