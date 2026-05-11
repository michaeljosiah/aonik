// AonikSidebar — production sidebar shell, 1:1 visual port of
// templates/aonik-admin-starterkit/kit/shell-aonik.jsx (AonikSidebar).
//
// Preserves the live admin behaviours that the template doesn't model:
//   - runtime audience filtering (host vs tenant)
//   - workspace flyout (templates + saved layouts) on the Workspace nav item
//   - role hydration from /admin manifest + identity service
//   - theme / logout / profile menu inside the bottom user card
//   - collapse-with-hover-expand (mouse over collapsed sidebar visually expands)

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import {
  Search,
  ChevronRight, ChevronDown, PanelLeftClose, PanelLeft, X, Check,
  Award, UserCog, Info, FileText, Sun, Moon, Monitor, LogOut,
  Layout,
} from 'lucide-react';

import { cn } from '@/lib/utils';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { useTheme } from '@/contexts';
import type { NavItem, NavigationSection } from '@/types';
import { useModules } from '@/modules';
import { useAuth, type AuthUser } from '@/auth/useAuth';
import { isPortalAdmin as resolvePortalAdmin } from '@/lib/roleUtils';
import { identityService } from '@/services/identityService';
import { tenantService } from '@/services/tenantService';
import { getSelectedTenant, setSelectedTenant } from '@/lib/tenantContext';
import type { MyTenantSummary } from '@/types';
import { getWorkspacePanelForRoute, getWorkspaceTemplates } from '@/workspace/registry';
import { loadWorkspaceState } from '@/workspace/storage';
import type { WorkspaceTemplate } from '@/workspace/types';

import { AonikMark, AonikWordmark } from './AonikMark';
import { AonikTemplateIcon } from './AonikTemplateIcon';
import { NavPopover } from './NavPopover';
import { getViewportFlyoutPosition } from './flyoutPosition';
import { STARTERKIT_SIDEBAR_NAV, collectNavItemHrefs } from './starterkitSidebarNav';

interface AonikSidebarProps {
  collapsed?: boolean;
  onToggle?: () => void;
}

function resolveHref(href: string | undefined): string {
  if (!href) return '#';
  const panel = getWorkspacePanelForRoute(href);
  return panel ? `/workspace?panel=${panel.id}` : href;
}

// ─── Regular nav item — opens NavPopover for items with children ─────────
function NavItemRow({
  item,
  collapsed,
}: {
  item: NavItem;
  collapsed: boolean;
}) {
  const location = useLocation();
  const triggerRef = useRef<HTMLDivElement>(null);
  const [openRect, setOpenRect] = useState<DOMRect | null>(null);

  const hasChildren =
    (item.children && item.children.length > 0) || (item.childGroups && item.childGroups.length > 0);

  const childHrefs = useMemo(() => collectNavItemHrefs(item), [item]);

  const isActive = useMemo(() => {
    if (item.href && item.href === location.pathname) return true;
    return childHrefs.some((h) => h === location.pathname);
  }, [item.href, childHrefs, location.pathname]);

  const isOpen = openRect !== null;

  const handleToggleClick = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!hasChildren) return;
    if (isOpen) {
      setOpenRect(null);
    } else {
      setOpenRect(e.currentTarget.getBoundingClientRect());
    }
  };

  const handleClose = () => setOpenRect(null);

  // Visual treatment of the row (template-spec: 7px 10px padding, 8px radius,
  // active = surface bg + light border + small shadow + text-primary).
  const rowClasses = cn(
    'relative flex items-center rounded-lg cursor-pointer transition-colors duration-150 border',
    collapsed ? 'h-9 w-9 justify-center' : 'gap-2.5 px-2.5 py-[7px]',
    isActive || isOpen
      ? 'bg-[var(--color-surface)] border-[var(--color-border-light)] text-[var(--color-text-primary)] font-medium shadow-[0_1px_2px_0_rgb(0_0_0/0.04)]'
      : 'border-transparent text-[var(--color-text-secondary)] font-normal hover:bg-black/[0.03]',
  );

  const content = (
    <>
      <AonikTemplateIcon
        name={item.icon}
        size={16}
        color={isActive ? 'var(--color-brand-primary)' : 'var(--color-text-secondary)'}
        className="h-4 w-4 shrink-0"
      />
      {!collapsed && <span className="flex-1 truncate text-[13px]">{item.label}</span>}
      {!collapsed && item.badge != null && (
        <span className="rounded-full bg-[var(--color-brand-secondary)] px-1.5 py-px font-mono text-[10px] font-semibold text-white">
          {item.badge}
        </span>
      )}
      {!collapsed && hasChildren && (
        <ChevronRight
          className={cn(
            'h-3 w-3 shrink-0 transition-colors',
            isOpen ? 'text-[var(--color-brand-primary)]' : 'text-[var(--color-text-tertiary)]',
          )}
        />
      )}
      {collapsed && hasChildren && (
        <span
          aria-hidden
          className={cn(
            'absolute bottom-1 right-1.5 h-1 w-1 rounded-full',
            isActive ? 'bg-[var(--color-brand-primary)]' : 'bg-[var(--color-text-tertiary)]',
          )}
        />
      )}
    </>
  );

  // Leaf items render as a Link; parent-with-children render as a click target.
  if (!hasChildren) {
    const href = resolveHref(item.href);
    if (collapsed) {
      return (
        <Tooltip>
          <TooltipTrigger asChild>
            <Link to={href} className={rowClasses}>
              {content}
            </Link>
          </TooltipTrigger>
          <TooltipContent side="right" sideOffset={8}>
            <p>{item.label}</p>
          </TooltipContent>
        </Tooltip>
      );
    }
    return (
      <Link to={href} className={rowClasses}>
        {content}
      </Link>
    );
  }

  return (
    <div ref={triggerRef} className="relative">
      {collapsed ? (
        <Tooltip>
          <TooltipTrigger asChild>
            <div className={rowClasses} onClick={handleToggleClick}>
              {content}
            </div>
          </TooltipTrigger>
          {!isOpen && (
            <TooltipContent side="right" sideOffset={8}>
              <p>{item.label}</p>
            </TooltipContent>
          )}
        </Tooltip>
      ) : (
        <div className={rowClasses} onClick={handleToggleClick}>
          {content}
        </div>
      )}
      {openRect && (
        <NavPopover
          parent={item}
          anchorRect={openRect}
          onClose={handleClose}
        />
      )}
    </div>
  );
}

// ─── Workspace nav item — special: shows templates + saved layouts ───────
interface WorkspaceLayoutSummary {
  id: string;
  name: string;
  isDefault: boolean;
  updatedAt: string;
}

function useWorkspaceLayouts() {
  const [layouts, setLayouts] = useState<WorkspaceLayoutSummary[]>(() => {
    const stored = loadWorkspaceState();
    return stored.layouts.map((l) => ({
      id: l.id,
      name: l.name,
      isDefault: l.isDefault,
      updatedAt: l.updatedAt,
    }));
  });
  const [activeLayoutId, setActiveLayoutId] = useState<string>(
    () => loadWorkspaceState().activeLayoutId ?? '',
  );

  useEffect(() => {
    const handler = (e: Event) => {
      const detail = (e as CustomEvent).detail as
        | { layouts?: WorkspaceLayoutSummary[]; activeLayoutId?: string }
        | undefined;
      if (detail?.layouts) setLayouts(detail.layouts);
      if (detail?.activeLayoutId !== undefined) setActiveLayoutId(detail.activeLayoutId);
    };
    window.addEventListener('aonik:workspace:state', handler);
    return () => window.removeEventListener('aonik:workspace:state', handler);
  }, []);

  return { layouts, activeLayoutId };
}

function WorkspaceNavItemRow({ item, collapsed }: { item: NavItem; collapsed: boolean }) {
  const location = useLocation();
  const navigate = useNavigate();
  const { layouts, activeLayoutId } = useWorkspaceLayouts();
  const [templates] = useState<WorkspaceTemplate[]>(() => getWorkspaceTemplates());
  const triggerRef = useRef<HTMLDivElement>(null);
  const [showFlyout, setShowFlyout] = useState(false);
  const [position, setPosition] = useState({ top: 0, left: 0, maxHeight: 160, pointerTop: 14 });
  const isActive = location.pathname === '/workspace';
  const hasContent = layouts.length > 0 || templates.length > 0;

  useEffect(() => {
    if (showFlyout && triggerRef.current) {
      const rect = triggerRef.current.getBoundingClientRect();
      setPosition(getViewportFlyoutPosition(rect));
    }
  }, [showFlyout]);

  useEffect(() => {
    if (!showFlyout) return;
    const handleClickOutside = (e: MouseEvent) => {
      if (triggerRef.current && !triggerRef.current.contains(e.target as Node)) {
        const isInsideFlyout = (e.target as Element)?.closest?.('.flyout-menu');
        if (!isInsideFlyout) setShowFlyout(false);
      }
    };
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setShowFlyout(false);
    };
    const raf = requestAnimationFrame(() => {
      document.addEventListener('mousedown', handleClickOutside);
      document.addEventListener('keydown', handleKey);
    });
    return () => {
      cancelAnimationFrame(raf);
      document.removeEventListener('mousedown', handleClickOutside);
      document.removeEventListener('keydown', handleKey);
    };
  }, [showFlyout]);

  const handleToggleClick = () => {
    if (hasContent) setShowFlyout((v) => !v);
    else navigate('/workspace');
  };

  const handleSelectLayout = useCallback(
    (layoutId: string) => {
      setShowFlyout(false);
      navigate(`/workspace?layout=${layoutId}`);
    },
    [navigate],
  );

  const handleSelectTemplate = useCallback(
    (templateId: string) => {
      setShowFlyout(false);
      navigate(`/workspace?template=${templateId}`);
    },
    [navigate],
  );

  const rowClasses = cn(
    'relative flex items-center rounded-lg cursor-pointer transition-colors duration-150 border',
    collapsed ? 'h-9 w-9 justify-center' : 'gap-2.5 px-2.5 py-[7px]',
    isActive || showFlyout
      ? 'bg-[var(--color-surface)] border-[var(--color-border-light)] text-[var(--color-text-primary)] font-medium shadow-[0_1px_2px_0_rgb(0_0_0/0.04)]'
      : 'border-transparent text-[var(--color-text-secondary)] font-normal hover:bg-black/[0.03]',
  );

  const content = (
    <>
      <AonikTemplateIcon
        name={item.icon}
        size={16}
        color={isActive ? 'var(--color-brand-primary)' : 'var(--color-text-secondary)'}
        className="h-4 w-4 shrink-0"
      />
      {!collapsed && <span className="flex-1 truncate text-[13px]">{item.label}</span>}
      {!collapsed && hasContent && (
        <ChevronRight
          className={cn(
            'h-3 w-3 shrink-0 transition-colors',
            showFlyout ? 'text-[var(--color-brand-primary)]' : 'text-[var(--color-text-tertiary)]',
          )}
        />
      )}
      {collapsed && hasContent && (
        <span
          aria-hidden
          className={cn(
            'absolute bottom-1 right-1.5 h-1 w-1 rounded-full',
            isActive ? 'bg-[var(--color-brand-primary)]' : 'bg-[var(--color-text-tertiary)]',
          )}
        />
      )}
    </>
  );

  const flyout = showFlyout && hasContent && (
    <div
      className="flyout-menu fixed z-[1000] min-w-[232px] rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)] p-1.5"
      style={{
        left: position.left,
        top: position.top,
        maxHeight: position.maxHeight,
        boxShadow: '0 18px 40px -10px rgb(0 0 0 / 0.22), 0 0 0 1px rgb(0 0 0 / 0.02)',
      }}
    >
      <span
        aria-hidden
        className="absolute -left-[5px] h-[9px] w-[9px] rotate-45 border-b border-l border-[var(--color-border-light)] bg-[var(--color-surface)]"
        style={{ top: position.pointerTop }}
      />
      <div className="overflow-y-auto" style={{ maxHeight: position.maxHeight - 12 }}>
        <div className="mb-1 flex items-center justify-between gap-2 border-b border-[var(--color-border-light)] px-2.5 pb-2.5 pt-2">
          <span className="flex items-center gap-2">
            <AonikTemplateIcon name={item.icon} size={13} color="var(--color-brand-primary)" />
            <span className="text-[12.5px] font-semibold text-[var(--color-text-primary)]">
              {item.label}
            </span>
          </span>
        </div>

        {templates.length > 0 && (
          <>
            <div className="px-2.5 pb-0.5 pt-1.5">
              <span className="text-[10px] font-semibold uppercase tracking-wider text-[var(--color-text-tertiary)]">
                Templates
              </span>
            </div>
            {templates.map((template) => {
              return (
                <button
                  key={template.id}
                  type="button"
                  onClick={() => handleSelectTemplate(template.id)}
                  title={template.description}
                  className="flex w-full items-center gap-2.5 rounded-md px-2.5 py-2 text-left text-[12.5px] text-[var(--color-text-primary)] transition-colors hover:bg-black/[0.04]"
                >
                  <AonikTemplateIcon
                    name={template.icon ?? 'sparkles'}
                    size={14}
                    color="var(--color-text-secondary)"
                    className="h-3.5 w-3.5 shrink-0"
                  />
                  <span className="flex-1 truncate">{template.name}</span>
                </button>
              );
            })}
          </>
        )}

        {layouts.length > 0 && (
          <>
            {templates.length > 0 && (
              <div className="my-1 border-t border-[var(--color-border-light)]" />
            )}
            <div className="px-2.5 pb-0.5 pt-1.5">
              <span className="text-[10px] font-semibold uppercase tracking-wider text-[var(--color-text-tertiary)]">
                Layouts
              </span>
            </div>
            {layouts.map((layout) => {
              const isLayoutActive = layout.id === activeLayoutId;
              return (
                <button
                  key={layout.id}
                  type="button"
                  onClick={() => handleSelectLayout(layout.id)}
                  className={cn(
                    'flex w-full items-center gap-2.5 rounded-md px-2.5 py-2 text-left text-[12.5px] transition-colors',
                    isLayoutActive
                      ? 'bg-[var(--color-brand-primary-10)] font-semibold text-[var(--color-brand-primary)]'
                      : 'text-[var(--color-text-primary)] hover:bg-black/[0.04]',
                  )}
                >
                  <Layout
                    className={cn(
                      'h-3.5 w-3.5 shrink-0',
                      isLayoutActive
                        ? 'text-[var(--color-brand-primary)]'
                        : 'text-[var(--color-text-secondary)]',
                    )}
                  />
                  <span className="flex-1 truncate">{layout.name}</span>
                  {layout.isDefault && (
                    <Badge variant="outline" className="px-1 py-0 text-[9px]">
                      Default
                    </Badge>
                  )}
                </button>
              );
            })}
          </>
        )}

        {item.viewAllHref && (
          <div className="mt-1 border-t border-[var(--color-border-light)] pt-1">
            <Link
              to={item.viewAllHref}
              onClick={() => setShowFlyout(false)}
              className="flex items-center justify-center rounded-md px-2 py-1 text-sm text-[var(--color-brand-primary)] transition-colors hover:bg-[var(--color-sidebar-hover)]"
            >
              {item.viewAllLabel ?? 'View all'}
            </Link>
          </div>
        )}
      </div>
    </div>
  );

  return (
    <div ref={triggerRef} className="relative">
      {collapsed ? (
        <Tooltip>
          <TooltipTrigger asChild>
            <div className={rowClasses} onClick={handleToggleClick}>
              {content}
            </div>
          </TooltipTrigger>
          {!showFlyout && (
            <TooltipContent side="right" sideOffset={8}>
              <p>{item.label}</p>
            </TooltipContent>
          )}
        </Tooltip>
      ) : (
        <div className={rowClasses} onClick={handleToggleClick}>
          {content}
        </div>
      )}
      {flyout}
    </div>
  );
}

// ─── Workspace switcher (tenant picker) ──────────────────────────────────
// Click the resting card to open a popover listing the tenants the
// public login endpoint exposes; selecting one persists the choice and
// reloads to "/" so module data, breadcrumbs, and routes refresh.

function tenantInitials(name: string | undefined): string {
  if (!name) return '?';
  return name
    .split(' ')
    .map((n) => n[0])
    .join('')
    .slice(0, 2)
    .toUpperCase();
}

function WorkspaceSwitcher() {
  const containerRef = useRef<HTMLDivElement>(null);
  const tenant = getSelectedTenant();
  const [isOpen, setIsOpen] = useState(false);
  const [tenants, setTenants] = useState<MyTenantSummary[] | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Lazy-fetch tenants the first time the popover opens.
  useEffect(() => {
    if (!isOpen || tenants !== null || loading) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    tenantService
      .listMyTenants()
      .then((res) => {
        if (cancelled) return;
        setTenants(res.tenants);
      })
      .catch((err) => {
        if (cancelled) return;
        setError(
          (err && typeof err === 'object' && 'userMessage' in err
            ? String((err as { userMessage?: string }).userMessage ?? '')
            : '') || 'Could not load workspaces.',
        );
        setTenants([]);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [isOpen, tenants, loading]);

  // Outside click + Esc close.
  useEffect(() => {
    if (!isOpen) return;
    const handleClick = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    };
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setIsOpen(false);
    };
    const raf = requestAnimationFrame(() => {
      document.addEventListener('mousedown', handleClick);
      document.addEventListener('keydown', handleKey);
    });
    return () => {
      cancelAnimationFrame(raf);
      document.removeEventListener('mousedown', handleClick);
      document.removeEventListener('keydown', handleKey);
    };
  }, [isOpen]);

  if (!tenant?.tenantId) return null;

  const handleSwitch = (next: MyTenantSummary) => {
    if (next.tenantId === tenant.tenantId) {
      setIsOpen(false);
      return;
    }
    setSelectedTenant({
      tenantId: next.tenantId,
      name: next.name,
      subdomain: next.subdomain,
      environment: next.environment,
    });
    // Hard reload onto the home route — modules, routes, breadcrumbs,
    // and tenant-scoped API caches all rebind cleanly that way.
    window.location.assign('/');
  };

  return (
    <div ref={containerRef} className="relative mb-3">
      <button
        type="button"
        onClick={() => setIsOpen((v) => !v)}
        className="flex w-full items-center gap-2.5 rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface)] px-2.5 py-[7px] text-left transition-colors hover:bg-black/[0.02]"
        aria-haspopup="listbox"
        aria-expanded={isOpen}
      >
        <span
          className="flex h-6 w-6 shrink-0 items-center justify-center rounded-md text-[10px] font-bold text-white"
          style={{ background: 'var(--color-brand-primary)', fontFamily: 'var(--font-brand)' }}
        >
          {tenantInitials(tenant.name)}
        </span>
        <span className="min-w-0 flex-1">
          <span className="block truncate text-[12px] font-semibold text-[var(--color-text-primary)]">
            {tenant.name ?? 'Workspace'}
          </span>
          <span className="block truncate text-[10px] text-[var(--color-text-secondary)]">
            {tenant.environment ?? 'Workspace'}
            {tenant.subdomain ? ` · ${tenant.subdomain}` : ''}
          </span>
        </span>
        <ChevronDown className="h-3 w-3 shrink-0 text-[var(--color-text-tertiary)]" />
      </button>

      {isOpen && (
        <div
          className="flyout-menu absolute left-0 right-0 top-full z-[1000] mt-1.5 overflow-hidden rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)] p-1.5"
          style={{
            boxShadow: '0 18px 40px -10px rgb(0 0 0 / 0.22), 0 0 0 1px rgb(0 0 0 / 0.02)',
          }}
          role="listbox"
        >
          <div className="px-2.5 pb-1 pt-1.5">
            <span className="text-[10px] font-semibold uppercase tracking-wider text-[var(--color-text-tertiary)]">
              Switch workspace
            </span>
          </div>

          {loading && (
            <div className="px-2.5 py-2 text-[12px] text-[var(--color-text-secondary)]">
              Loading workspaces…
            </div>
          )}

          {error && (
            <div className="px-2.5 py-2 text-[12px] text-[var(--color-error)]">{error}</div>
          )}

          {tenants?.map((t) => {
            const isCurrent = t.tenantId === tenant.tenantId;
            return (
              <button
                key={t.tenantId}
                type="button"
                role="option"
                aria-selected={isCurrent}
                onClick={() => handleSwitch(t)}
                className={cn(
                  'flex w-full items-center gap-2.5 rounded-md px-2.5 py-2 text-left transition-colors',
                  isCurrent
                    ? 'bg-[var(--color-brand-primary-10)] text-[var(--color-brand-primary)]'
                    : 'text-[var(--color-text-primary)] hover:bg-black/[0.04]',
                )}
              >
                <span
                  className="flex h-6 w-6 shrink-0 items-center justify-center rounded-md text-[10px] font-bold text-white"
                  style={{ background: 'var(--color-brand-primary)', fontFamily: 'var(--font-brand)' }}
                >
                  {tenantInitials(t.name)}
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-[12.5px] font-semibold">{t.name}</span>
                  <span className="block truncate text-[10px] text-[var(--color-text-tertiary)]">
                    {t.environment}
                    {t.subdomain ? ` · ${t.subdomain}` : ''}
                  </span>
                </span>
                {isCurrent && (
                  <Check className="h-3.5 w-3.5 shrink-0 text-[var(--color-brand-primary)]" />
                )}
              </button>
            );
          })}

          {tenants && tenants.length === 0 && !loading && !error && (
            <div className="px-2.5 py-2 text-[12px] text-[var(--color-text-secondary)]">
              No other workspaces available.
            </div>
          )}
        </div>
      )}
    </div>
  );
}

// ─── Compact bottom user profile (template-style) ────────────────────────
// Resting state: avatar + online dot + name + email + chevdown (single row).
// Click to expand a popover above with menu, theme switcher, and logout —
// porting the existing UserProfile menu structure.

const formatRoleLabel = (role: string) =>
  role
    .replace(/[_-]+/g, ' ')
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/\b\w/g, (char) => char.toUpperCase());

function UserProfileCard({
  user,
  collapsed,
  onLogout,
}: {
  user: AuthUser;
  collapsed: boolean;
  onLogout: () => void;
}) {
  const [isExpanded, setIsExpanded] = useState(false);
  const [apiRoles, setApiRoles] = useState<string[]>([]);
  const [profilePhotoUrl, setProfilePhotoUrl] = useState<string | null>(null);
  const [imageError, setImageError] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const { theme, setTheme } = useTheme();

  const initials = user.name
    .split(' ')
    .map((n) => n[0])
    .join('')
    .toUpperCase();

  // Load roles + photo from identity service if we don't already have them
  useEffect(() => {
    let cancelled = false;
    const fetchInfo = async () => {
      if (user.roleSource === 'api' || !user.roles || user.roles.length === 0) {
        try {
          const info = await identityService.getUserInfo();
          if (cancelled) return;
          setApiRoles(info.roles);
          const photoUrl = info.photoUrlSmall || info.photoUrlTiny || info.photoUrl;
          if (photoUrl) {
            const fullUrl = photoUrl.startsWith('http')
              ? photoUrl
              : `${import.meta.env.VITE_API_URL || 'https://localhost:5001'}${photoUrl}`;
            setProfilePhotoUrl(fullUrl);
          }
        } catch {
          if (!cancelled) setApiRoles([]);
        }
      }
    };
    fetchInfo();
    return () => {
      cancelled = true;
    };
  }, [user.id, user.roles, user.roleSource]);

  // Close popover on outside click / Esc
  useEffect(() => {
    if (!isExpanded) return;
    const handleClick = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsExpanded(false);
      }
    };
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setIsExpanded(false);
    };
    const raf = requestAnimationFrame(() => {
      document.addEventListener('mousedown', handleClick);
      document.addEventListener('keydown', handleKey);
    });
    return () => {
      cancelAnimationFrame(raf);
      document.removeEventListener('mousedown', handleClick);
      document.removeEventListener('keydown', handleKey);
    };
  }, [isExpanded]);

  const effectiveRoles = apiRoles.length > 0 ? apiRoles : user.roles && user.roles.length > 0 ? user.roles : ['User'];
  const roleLabel = effectiveRoles.map(formatRoleLabel).join(', ');
  const isAdmin = effectiveRoles.some(
    (r) => r.toLowerCase().includes('admin') || r.toLowerCase().includes('administrator'),
  );

  const displayPhoto = !imageError ? profilePhotoUrl || user.picture || null : null;

  if (collapsed) {
    return (
      <div className="flex justify-center pt-2.5">
        <Avatar
          className="relative h-8 w-8 cursor-pointer"
          onClick={() => setIsExpanded(true)}
          ref={containerRef as React.RefObject<HTMLDivElement>}
        >
          {displayPhoto && (
            <AvatarImage src={displayPhoto} alt={user.name} onError={() => setImageError(true)} />
          )}
          <AvatarFallback className="bg-[var(--color-violet,#7b76b6)] text-white text-xs">
            {initials}
          </AvatarFallback>
          <span className="absolute -bottom-0.5 -right-0.5 h-2.5 w-2.5 rounded-full border-2 border-[var(--color-surface-inset)] bg-[var(--color-success)]" />
        </Avatar>
      </div>
    );
  }

  return (
    <div ref={containerRef} className="relative pt-2.5">
      {/* Resting card — template-style: avatar + name + email + chevdown */}
      <button
        type="button"
        onClick={() => setIsExpanded((v) => !v)}
        className="flex w-full items-center gap-2.5 rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface)] px-2 py-2 text-left transition-colors hover:bg-black/[0.02]"
      >
        <span className="relative shrink-0">
          <Avatar className="h-8 w-8">
            {displayPhoto && (
              <AvatarImage src={displayPhoto} alt={user.name} onError={() => setImageError(true)} />
            )}
            <AvatarFallback className="bg-[var(--color-violet,#7b76b6)] text-white text-xs">
              {initials}
            </AvatarFallback>
          </Avatar>
          <span className="absolute -bottom-0.5 -right-0.5 h-2.5 w-2.5 rounded-full border-2 border-[var(--color-surface)] bg-[var(--color-success)]" />
        </span>
        <span className="min-w-0 flex-1">
          <span className="block truncate text-[12.5px] font-semibold text-[var(--color-text-primary)]">
            {user.name}
          </span>
          <span className="block truncate text-[10px] text-[var(--color-text-tertiary)]">
            {user.email ?? roleLabel}
          </span>
        </span>
        <ChevronDown className="h-3 w-3 shrink-0 text-[var(--color-text-tertiary)]" />
      </button>

      {/* Expanded popover (menu + theme + logout) — opens above the card */}
      {isExpanded && (
        <div className="absolute bottom-full left-0 right-0 mb-2 rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-elevated)] p-3 shadow-[0_18px_40px_-10px_rgb(0_0_0/0.22)]">
          <div className="mb-3 flex items-center justify-between">
            {isAdmin ? (
              <Badge variant="team" className="px-2 py-0.5 text-[11px]">
                Admin
              </Badge>
            ) : (
              <span className="text-[11px] text-[var(--color-text-tertiary)]">{roleLabel}</span>
            )}
            <button
              type="button"
              onClick={() => setIsExpanded(false)}
              className="rounded-md p-1 text-[var(--color-text-tertiary)] hover:bg-[var(--color-surface-inset)]"
              aria-label="Close"
            >
              <X className="h-4 w-4" />
            </button>
          </div>

          <div className="border-t border-[var(--color-border-light)] py-2">
            {[
              { icon: FileText, label: 'Guides', href: '/setup-guides' },
              { icon: Award, label: 'API Documentation' },
              { icon: UserCog, label: 'Manage profile' },
              { icon: Info, label: 'About Aonik' },
              { icon: FileText, label: 'Release notes' },
            ].map((mi) => (
              <button
                key={mi.label}
                type="button"
                className="flex w-full items-center gap-3 rounded-md px-2 py-2 text-sm text-[var(--color-text-primary)] transition-colors hover:bg-[var(--color-background)]"
                onClick={() => {
                  if (mi.href) window.location.href = mi.href;
                }}
              >
                <mi.icon className="h-4 w-4 text-[var(--color-text-secondary)]" />
                {mi.label}
              </button>
            ))}
          </div>

          <div className="border-t border-[var(--color-border-light)] py-2">
            <p className="mb-1.5 text-xs font-medium text-[var(--color-text-primary)]">Theme</p>
            <div className="flex rounded-md bg-[var(--color-background)] p-1">
              {[
                { value: 'light' as const, icon: Sun, label: 'Light' },
                { value: 'dark' as const, icon: Moon, label: 'Dark' },
                { value: 'system' as const, icon: Monitor, label: 'System' },
              ].map(({ value, icon: TIcon, label }) => (
                <button
                  key={value}
                  type="button"
                  onClick={() => setTheme(value)}
                  className={cn(
                    'flex flex-1 items-center justify-center gap-1.5 rounded-md px-2 py-1.5 text-[11px] font-medium transition-colors',
                    theme === value
                      ? 'bg-[var(--color-surface)] text-[var(--color-text-primary)] shadow-sm'
                      : 'text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]',
                  )}
                >
                  <TIcon className="h-3.5 w-3.5" />
                  {label}
                </button>
              ))}
            </div>
          </div>

          <div className="border-t border-[var(--color-border-light)] pt-2">
            <button
              type="button"
              onClick={onLogout}
              className="flex w-full items-center gap-3 rounded-md px-2 py-2 text-sm text-[var(--color-text-primary)] transition-colors hover:bg-[var(--color-background)]"
            >
              <LogOut className="h-4 w-4 text-[var(--color-text-secondary)]" />
              Log out
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

// ─── Sidebar ─────────────────────────────────────────────────────────────
export function AonikSidebar({ collapsed = false, onToggle }: AonikSidebarProps) {
  const { user, logout } = useAuth();
  const { manifest } = useModules();
  const [navRoles, setNavRoles] = useState<string[]>([]);
  const [isLoadingNavRoles, setIsLoadingNavRoles] = useState(false);
  const [menuHover, setMenuHover] = useState(false);

  // Hover-to-expand: when collapsed, hovering temporarily shows the full sidebar.
  const isVisuallyCollapsed = collapsed && !menuHover;

  const handleLogout = useCallback(async () => {
    try {
      await logout();
    } catch (err) {
      console.error('Logout failed', err);
    }
  }, [logout]);

      // Hydrate roles for nav audience filtering.
  useEffect(() => {
    let cancelled = false;
    const hydrate = async () => {
      if (!user) {
        setNavRoles([]);
        return;
      }
      if (user.roleSource !== 'api' && user.roles && user.roles.length > 0) {
        setNavRoles(user.roles);
        return;
      }
      setIsLoadingNavRoles(true);
      try {
        const info = await identityService.getUserInfo();
        if (!cancelled) setNavRoles(info.roles);
      } catch {
        if (!cancelled) setNavRoles([]);
      } finally {
        if (!cancelled) setIsLoadingNavRoles(false);
      }
    };
    hydrate();
    return () => {
      cancelled = true;
    };
  }, [user]);

  const isPortalAdmin = resolvePortalAdmin(navRoles);
  const disabledNavIds = useMemo(() => new Set(manifest?.disabledNavItems ?? []), [manifest]);
  const disabledRoutes = useMemo(() => new Set(manifest?.disabledRoutes ?? []), [manifest]);

  const isItemVisible = useCallback(
    (it: NavItem) => {
      if (disabledNavIds.has(it.id)) return false;
      if (it.href && disabledRoutes.has(it.href)) return false;
      if (it.audience === 'host') return isPortalAdmin;
      if (it.audience === 'tenant') return !isPortalAdmin && !isLoadingNavRoles;
      return true;
    },
    [disabledNavIds, disabledRoutes, isPortalAdmin, isLoadingNavRoles],
  );

  const filterItems = useCallback(
    (items: NavItem[]): NavItem[] =>
      items.reduce<NavItem[]>((acc, item) => {
        if (!isItemVisible(item)) return acc;
        const filteredChildren = item.children?.filter(isItemVisible);
        const filteredChildGroups = item.childGroups
          ?.map((g) => ({ ...g, items: g.items.filter(isItemVisible) }))
          .filter((g) => g.items.length > 0);
        const hasVisibleChildren = (filteredChildren?.length ?? 0) > 0 || (filteredChildGroups?.length ?? 0) > 0;
        const hasVisibleHref = Boolean(item.href && !disabledRoutes.has(item.href));
        if (!hasVisibleChildren && !hasVisibleHref) return acc;
        acc.push({ ...item, children: filteredChildren, childGroups: filteredChildGroups });
        return acc;
      }, []),
    [disabledRoutes, isItemVisible],
  );

  const visibleSections = STARTERKIT_SIDEBAR_NAV.filter((s: NavigationSection) => {
    if (s.audience === 'host') return isPortalAdmin;
    if (s.audience === 'tenant') return !isPortalAdmin && !isLoadingNavRoles;
    return true;
  });

  return (
    <TooltipProvider delayDuration={300}>
      <aside
        className={cn(
          'sticky top-0 z-40 flex h-screen flex-col border-r border-[var(--color-border-light)] bg-[var(--color-sidebar-bg)] transition-[width] duration-200',
          isVisuallyCollapsed ? 'w-[62px] px-2 py-3.5' : 'w-[240px] px-3 py-3.5',
        )}
        onMouseEnter={() => collapsed && setMenuHover(true)}
        onMouseLeave={(e) => {
          // Don't collapse if mouse moved into a flyout
          const target = e.relatedTarget;
          if (target instanceof Element && target.closest('.flyout-menu')) return;
          setMenuHover(false);
        }}
      >
        {/* Brand row */}
        <div
          className={cn(
            'flex items-center pb-3.5',
            isVisuallyCollapsed ? 'justify-center pt-1' : 'justify-between px-2 pt-1',
          )}
        >
          {isVisuallyCollapsed ? (
            <AonikMark size={22} />
          ) : (
            <Link to="/" className="flex items-center">
              <AonikWordmark size={19} />
            </Link>
          )}
          {!isVisuallyCollapsed && (
            <button
              type="button"
              onClick={onToggle}
              className="hover-halo"
              aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
              title={collapsed ? 'Expand' : 'Collapse'}
            >
              {collapsed ? <PanelLeft className="h-3.5 w-3.5" /> : <PanelLeftClose className="h-3.5 w-3.5" />}
            </button>
          )}
        </div>

        {/* Workspace switcher + search (expanded only) */}
        {!isVisuallyCollapsed && (
          <>
            <WorkspaceSwitcher />
            <div className="relative mb-2">
              <Search className="absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-[var(--color-text-tertiary)]" />
              <input
                type="text"
                placeholder="Search or ask…"
                className="h-8 w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] pl-8 pr-12 text-[13px] text-[var(--color-text-primary)] placeholder:text-[var(--color-text-tertiary)] outline-none transition-colors focus:border-[var(--color-brand-primary)]"
              />
              <span
                className="absolute right-2 top-1/2 -translate-y-1/2 rounded border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-1.5 py-px font-mono text-[10px] text-[var(--color-text-tertiary)]"
                aria-hidden
              >
                ⌘K
              </span>
            </div>
          </>
        )}

        {/* Nav groups */}
        <nav className="-mx-1 mt-1 flex-1 overflow-y-auto overflow-x-visible px-1">
          {visibleSections.map((section) => {
            const items = filterItems(section.items);
            if (items.length === 0) return null;
            return (
              <div key={section.id} className="mb-2.5">
                {!isVisuallyCollapsed && section.label && (
                  <div className="px-2.5 pb-1 pt-1.5">
                    <span className="text-[10px] font-semibold uppercase tracking-wider text-[var(--color-text-tertiary)]">
                      {section.label}
                    </span>
                  </div>
                )}
                <div className={cn('flex flex-col gap-0.5', isVisuallyCollapsed && 'items-center')}>
                  {items.map((item) =>
                    item.id === 'workspace' ? (
                      <WorkspaceNavItemRow key={item.id} item={item} collapsed={isVisuallyCollapsed} />
                    ) : (
                      <NavItemRow key={item.id} item={item} collapsed={isVisuallyCollapsed} />
                    ),
                  )}
                </div>
              </div>
            );
          })}
        </nav>

        {/* Bottom user profile */}
        <div className="mt-auto border-t border-[var(--color-border-light)]">
          {user && (
            <UserProfileCard user={user} collapsed={isVisuallyCollapsed} onLogout={handleLogout} />
          )}
        </div>
      </aside>
    </TooltipProvider>
  );
}
