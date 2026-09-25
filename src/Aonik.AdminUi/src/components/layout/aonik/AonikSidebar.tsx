// AonikSidebar: the app shell's navigation rail, on the shadcn Sidebar
// anatomy (Spec 098 §7.5).
//
//   - Expanded (16rem): section labels, menu buttons, parents open inline as
//     collapsible sub-menus (auto-open when a child is the current page).
//   - Collapsed (3rem icon rail): tooltips on every item; parents and the
//     Workspace item open a DropdownMenu to the right.
//   - Header: Aonik mark + tenant switcher (DropdownMenu).
//   - Footer: user menu (DropdownMenu) with theme and log out.
//   - Ctrl/Cmd+B toggles collapse.
//
// Visibility (audience, runtime overrides, module enablement) comes from
// useVisibleNav, shared with the command palette.

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import {
  BookOpenIcon,
  CheckIcon,
  ChevronRightIcon,
  ChevronsUpDownIcon,
  LayoutIcon,
  LogOutIcon,
  MonitorIcon,
  MoonIcon,
  SunIcon,
} from 'lucide-react';

import { cn } from '@/lib/utils';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  SidebarGroup,
  SidebarGroupLabel,
  SidebarMenu,
  SidebarMenuBadge,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarMenuSub,
  SidebarMenuSubButton,
  SidebarMenuSubItem,
} from '@/components/ui/sidebar';
import { Skeleton } from '@/components/ui/skeleton';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { useTheme } from '@/contexts';
import type { NavItem, NavItemGroup } from '@/types';
import { invalidateModuleManifest } from '@/modules/manifestCache';
import { useAuth, type AuthUser } from '@/auth/useAuth';
import { identityService } from '@/services/identityService';
import { tenantService } from '@/services/tenantService';
import { getSelectedTenant, setSelectedTenant } from '@/lib/tenantContext';
import type { MyTenantSummary } from '@/types';
import { getWorkspacePanelForRoute, getWorkspaceTemplates } from '@/workspace/registry';
import { loadWorkspaceState } from '@/workspace/storage';

import { AonikMark, AonikWordmark } from './AonikMark';
import { AonikTemplateIcon } from './AonikTemplateIcon';
import { collectNavItemHrefs } from './sidebarNav';
import { useVisibleNav } from './useVisibleNav';

interface AonikSidebarProps {
  collapsed?: boolean;
  onToggle?: () => void;
}

function resolveHref(href: string | undefined): string {
  if (!href) return '#';
  const panel = getWorkspacePanelForRoute(href);
  return panel ? `/workspace?panel=${panel.id}` : href;
}

function childGroupsOf(item: NavItem): NavItemGroup[] {
  if (item.childGroups && item.childGroups.length > 0) return item.childGroups;
  if (item.children && item.children.length > 0) return [{ label: '', items: item.children }];
  return [];
}

function hasChildren(item: NavItem): boolean {
  return childGroupsOf(item).length > 0;
}

function useIsActive(item: NavItem): boolean {
  const location = useLocation();
  const hrefs = useMemo(() => collectNavItemHrefs(item), [item]);
  return (item.href != null && item.href === location.pathname) || hrefs.some((h) => h === location.pathname);
}

function NavIcon({ item, active }: { item: NavItem; active: boolean }) {
  return (
    <AonikTemplateIcon
      name={item.icon}
      size={16}
      className={cn('size-4 shrink-0', active ? 'text-sidebar-primary' : 'text-sidebar-foreground/70')}
    />
  );
}

/** Collapsed-rail item: tooltip on hover, optional dropdown content. */
function RailTooltip({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <Tooltip>
      <TooltipTrigger asChild>{children}</TooltipTrigger>
      <TooltipContent side="right" sideOffset={8}>
        {label}
      </TooltipContent>
    </Tooltip>
  );
}

// ─── Expanded: inline sub-menu tree ──────────────────────────────────────
function SubTree({ item }: { item: NavItem }) {
  const location = useLocation();
  return (
    <SidebarMenuSub>
      {childGroupsOf(item).map((group, groupIndex) => (
        <div key={group.label || groupIndex} className="contents">
          {group.label && (
            <li className="px-2 pt-1.5 text-xs font-medium text-sidebar-foreground/60">{group.label}</li>
          )}
          {group.items.map((child) =>
            hasChildren(child) ? (
              <NestedParent key={child.id} item={child} />
            ) : (
              <SidebarMenuSubItem key={child.id}>
                <SidebarMenuSubButton asChild isActive={child.href === location.pathname}>
                  <Link to={resolveHref(child.href)}>
                    <span>{child.label}</span>
                  </Link>
                </SidebarMenuSubButton>
              </SidebarMenuSubItem>
            ),
          )}
        </div>
      ))}
      {item.viewAllHref && (
        <SidebarMenuSubItem>
          <SidebarMenuSubButton asChild>
            <Link to={resolveHref(item.viewAllHref)} className="text-sidebar-foreground/70">
              <span>{item.viewAllLabel ?? 'View all'}</span>
            </Link>
          </SidebarMenuSubButton>
        </SidebarMenuSubItem>
      )}
    </SidebarMenuSub>
  );
}

function NestedParent({ item }: { item: NavItem }) {
  const isActive = useIsActive(item);
  const [open, setOpen] = useState(isActive);
  return (
    <SidebarMenuSubItem>
      <Collapsible open={open} onOpenChange={setOpen}>
        <CollapsibleTrigger asChild>
          <button
            type="button"
            className="group/nested flex h-7 w-full items-center gap-2 rounded-md px-2 text-left text-sm text-sidebar-foreground outline-hidden ring-sidebar-ring hover:bg-sidebar-accent focus-visible:ring-2"
          >
            <span className="flex-1 truncate">{item.label}</span>
            <ChevronRightIcon className="size-3.5 shrink-0 text-sidebar-foreground/60 transition-transform group-data-[state=open]/nested:rotate-90" />
          </button>
        </CollapsibleTrigger>
        <CollapsibleContent>
          <SubTree item={item} />
        </CollapsibleContent>
      </Collapsible>
    </SidebarMenuSubItem>
  );
}

// ─── Collapsed: dropdown menu tree ───────────────────────────────────────
function DropdownTree({ item }: { item: NavItem }) {
  const location = useLocation();
  const groups = childGroupsOf(item);
  return (
    <>
      {groups.map((group, groupIndex) => (
        <DropdownMenuGroup key={group.label || groupIndex}>
          {groupIndex > 0 && <DropdownMenuSeparator />}
          {group.label && <DropdownMenuLabel className="text-xs text-muted-foreground">{group.label}</DropdownMenuLabel>}
          {group.items.map((child) =>
            hasChildren(child) ? (
              <DropdownMenuSub key={child.id}>
                <DropdownMenuSubTrigger>
                  <AonikTemplateIcon name={child.icon} size={16} />
                  {child.label}
                </DropdownMenuSubTrigger>
                <DropdownMenuSubContent className="min-w-48">
                  <DropdownTree item={child} />
                </DropdownMenuSubContent>
              </DropdownMenuSub>
            ) : (
              <DropdownMenuItem key={child.id} asChild>
                <Link to={resolveHref(child.href)} className={cn(child.href === location.pathname && 'font-medium')}>
                  <AonikTemplateIcon name={child.icon} size={16} />
                  {child.label}
                  {child.href === location.pathname && <CheckIcon className="ml-auto" />}
                </Link>
              </DropdownMenuItem>
            ),
          )}
        </DropdownMenuGroup>
      ))}
      {item.viewAllHref && (
        <>
          <DropdownMenuSeparator />
          <DropdownMenuItem asChild>
            <Link to={resolveHref(item.viewAllHref)}>{item.viewAllLabel ?? 'View all'}</Link>
          </DropdownMenuItem>
        </>
      )}
    </>
  );
}

// ─── One top-level item ──────────────────────────────────────────────────
function NavItemRow({ item, collapsed }: { item: NavItem; collapsed: boolean }) {
  const isActive = useIsActive(item);
  const [open, setOpen] = useState(isActive);
  const parent = hasChildren(item);

  const badge = item.badge != null && !collapsed && <SidebarMenuBadge>{item.badge}</SidebarMenuBadge>;

  if (!parent) {
    const link = (
      <SidebarMenuButton asChild isActive={isActive} collapsed={collapsed}>
        <Link to={resolveHref(item.href)} aria-label={collapsed ? item.label : undefined}>
          <NavIcon item={item} active={isActive} />
          {!collapsed && <span>{item.label}</span>}
          {badge}
        </Link>
      </SidebarMenuButton>
    );
    return <SidebarMenuItem>{collapsed ? <RailTooltip label={item.label}>{link}</RailTooltip> : link}</SidebarMenuItem>;
  }

  if (collapsed) {
    return (
      <SidebarMenuItem>
        <DropdownMenu>
          <RailTooltip label={item.label}>
            <DropdownMenuTrigger asChild>
              <SidebarMenuButton isActive={isActive} collapsed aria-label={item.label}>
                <NavIcon item={item} active={isActive} />
              </SidebarMenuButton>
            </DropdownMenuTrigger>
          </RailTooltip>
          <DropdownMenuContent side="right" align="start" sideOffset={8} className="min-w-52">
            <DropdownMenuLabel>{item.label}</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownTree item={item} />
          </DropdownMenuContent>
        </DropdownMenu>
      </SidebarMenuItem>
    );
  }

  return (
    <SidebarMenuItem>
      <Collapsible open={open} onOpenChange={setOpen}>
        <CollapsibleTrigger asChild>
          <SidebarMenuButton isActive={isActive && !open} className="group/parent">
            <NavIcon item={item} active={isActive} />
            <span className="flex-1 truncate">{item.label}</span>
            {badge}
            <ChevronRightIcon className="ml-auto text-sidebar-foreground/60 transition-transform duration-200 group-data-[state=open]/parent:rotate-90" />
          </SidebarMenuButton>
        </CollapsibleTrigger>
        <CollapsibleContent>
          <SubTree item={item} />
        </CollapsibleContent>
      </Collapsible>
    </SidebarMenuItem>
  );
}

// ─── Workspace item: templates + saved layouts ───────────────────────────
interface WorkspaceLayoutSummary {
  id: string;
  name: string;
  isDefault: boolean;
  updatedAt: string;
}

function useWorkspaceLayouts() {
  const [layouts, setLayouts] = useState<WorkspaceLayoutSummary[]>(() =>
    loadWorkspaceState().layouts.map((l) => ({
      id: l.id,
      name: l.name,
      isDefault: l.isDefault,
      updatedAt: l.updatedAt,
    })),
  );
  const [activeLayoutId, setActiveLayoutId] = useState<string>(() => loadWorkspaceState().activeLayoutId ?? '');

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
  const [templates] = useState(() => getWorkspaceTemplates());
  const isActive = location.pathname === '/workspace';
  const hasContent = layouts.length > 0 || templates.length > 0;

  const button = (
    <SidebarMenuButton
      isActive={isActive}
      collapsed={collapsed}
      aria-label={collapsed ? item.label : undefined}
      onClick={hasContent ? undefined : () => navigate('/workspace')}
      className="group/parent"
    >
      <NavIcon item={item} active={isActive} />
      {!collapsed && <span className="flex-1 truncate">{item.label}</span>}
      {!collapsed && hasContent && (
        <ChevronRightIcon className="ml-auto text-sidebar-foreground/60 transition-transform group-data-[state=open]/parent:rotate-90" />
      )}
    </SidebarMenuButton>
  );

  if (!hasContent) {
    return <SidebarMenuItem>{collapsed ? <RailTooltip label={item.label}>{button}</RailTooltip> : button}</SidebarMenuItem>;
  }

  const trigger = <DropdownMenuTrigger asChild>{button}</DropdownMenuTrigger>;

  return (
    <SidebarMenuItem>
      <DropdownMenu>
        {collapsed ? <RailTooltip label={item.label}>{trigger}</RailTooltip> : trigger}
        <DropdownMenuContent side="right" align="start" sideOffset={8} className="min-w-60">
          <DropdownMenuItem onSelect={() => navigate('/workspace')}>
            <LayoutIcon />
            Open workspace
          </DropdownMenuItem>
          {templates.length > 0 && (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuLabel className="text-xs text-muted-foreground">Templates</DropdownMenuLabel>
              {templates.map((template) => (
                <DropdownMenuItem
                  key={template.id}
                  title={template.description}
                  onSelect={() => navigate(`/workspace?template=${template.id}`)}
                >
                  <AonikTemplateIcon name={template.icon ?? 'sparkles'} size={16} />
                  <span className="truncate">{template.name}</span>
                </DropdownMenuItem>
              ))}
            </>
          )}
          {layouts.length > 0 && (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuLabel className="text-xs text-muted-foreground">Saved layouts</DropdownMenuLabel>
              {layouts.map((layout) => (
                <DropdownMenuItem key={layout.id} onSelect={() => navigate(`/workspace?layout=${layout.id}`)}>
                  <LayoutIcon />
                  <span className="truncate">{layout.name}</span>
                  {layout.isDefault && (
                    <Badge variant="outline" className="ml-auto">
                      Default
                    </Badge>
                  )}
                  {layout.id === activeLayoutId && !layout.isDefault && <CheckIcon className="ml-auto" />}
                </DropdownMenuItem>
              ))}
            </>
          )}
          {item.viewAllHref && (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuItem asChild>
                <Link to={item.viewAllHref}>{item.viewAllLabel ?? 'View all'}</Link>
              </DropdownMenuItem>
            </>
          )}
        </DropdownMenuContent>
      </DropdownMenu>
    </SidebarMenuItem>
  );
}

// ─── Tenant switcher ─────────────────────────────────────────────────────
function tenantInitials(name: string | undefined): string {
  if (!name) return '?';
  return name
    .split(' ')
    .map((n) => n[0])
    .join('')
    .slice(0, 2)
    .toUpperCase();
}

function TenantTile({ name, className }: { name?: string; className?: string }) {
  return (
    <span
      className={cn(
        'flex size-8 shrink-0 items-center justify-center rounded-md bg-sidebar-primary text-xs font-semibold text-sidebar-primary-foreground',
        className,
      )}
    >
      {tenantInitials(name)}
    </span>
  );
}

function WorkspaceSwitcher({ collapsed }: { collapsed: boolean }) {
  const tenant = getSelectedTenant();
  const [open, setOpen] = useState(false);
  const [tenants, setTenants] = useState<MyTenantSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Lazy-fetch the tenants the first time the menu opens.
  useEffect(() => {
    if (!open || tenants !== null) return;
    let cancelled = false;
    tenantService
      .listMyTenants()
      .then((res) => {
        if (cancelled) return;
        setError(null);
        setTenants(res.tenants);
      })
      .catch((err) => {
        if (cancelled) return;
        setError(
          (err && typeof err === 'object' && 'userMessage' in err
            ? String((err as { userMessage?: string }).userMessage ?? '')
            : '') || "Couldn't load workspaces.",
        );
        setTenants([]);
      });
    return () => {
      cancelled = true;
    };
  }, [open, tenants]);

  if (!tenant?.tenantId) return null;

  const handleSwitch = (next: MyTenantSummary) => {
    if (next.tenantId === tenant.tenantId) return;
    setSelectedTenant({
      tenantId: next.tenantId,
      name: next.name,
      subdomain: next.subdomain,
      environment: next.environment,
    });
    // Never serve the previous tenant's module manifest, even briefly.
    invalidateModuleManifest();
    // Hard reload onto the home route so modules, routes, breadcrumbs and
    // tenant-scoped caches all rebind cleanly.
    window.location.assign('/');
  };

  const loading = open && tenants === null && error === null;
  const meta = [tenant.environment, tenant.subdomain].filter(Boolean).join(', ');

  return (
    <DropdownMenu open={open} onOpenChange={setOpen}>
      <DropdownMenuTrigger asChild>
        <SidebarMenuButton
          size="lg"
          collapsed={collapsed}
          aria-label={collapsed ? `Workspace: ${tenant.name ?? 'Workspace'}` : undefined}
          className="data-[state=open]:bg-sidebar-accent"
        >
          <TenantTile name={tenant.name} />
          {!collapsed && (
            <>
              <span className="grid flex-1 text-left leading-tight">
                <span className="truncate text-sm font-medium">{tenant.name ?? 'Workspace'}</span>
                {meta && <span className="truncate text-xs text-sidebar-foreground/70">{meta}</span>}
              </span>
              <ChevronsUpDownIcon className="ml-auto text-sidebar-foreground/60" />
            </>
          )}
        </SidebarMenuButton>
      </DropdownMenuTrigger>
      <DropdownMenuContent
        className="min-w-60"
        align="start"
        side={collapsed ? 'right' : 'bottom'}
        sideOffset={4}
      >
        <DropdownMenuLabel className="text-xs text-muted-foreground">Switch workspace</DropdownMenuLabel>
        {loading && (
          <div className="flex flex-col gap-1 p-1">
            <Skeleton className="h-8 w-full" />
            <Skeleton className="h-8 w-full" />
          </div>
        )}
        {error && <p className="px-2 py-1.5 text-sm text-destructive">{error}</p>}
        {tenants?.map((t) => {
          const isCurrent = t.tenantId === tenant.tenantId;
          const tMeta = [t.environment, t.subdomain].filter(Boolean).join(', ');
          return (
            <DropdownMenuItem key={t.tenantId} onSelect={() => handleSwitch(t)} className="gap-2 p-2">
              <TenantTile name={t.name} className="size-6 text-[10px]" />
              <span className="grid min-w-0 flex-1 leading-tight">
                <span className="truncate font-medium">{t.name}</span>
                {tMeta && <span className="truncate text-xs text-muted-foreground">{tMeta}</span>}
              </span>
              {isCurrent && <CheckIcon className="ml-auto" />}
            </DropdownMenuItem>
          );
        })}
        {tenants && tenants.length === 0 && !loading && !error && (
          <p className="px-2 py-1.5 text-sm text-muted-foreground">No other workspaces.</p>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

// ─── User menu ───────────────────────────────────────────────────────────
const formatRoleLabel = (role: string) =>
  role
    .replace(/[_-]+/g, ' ')
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/\b\w/g, (char) => char.toUpperCase());

function UserMenu({ user, collapsed, onLogout }: { user: AuthUser; collapsed: boolean; onLogout: () => void }) {
  const navigate = useNavigate();
  const { theme, setTheme } = useTheme();
  const [apiRoles, setApiRoles] = useState<string[]>([]);
  const [profilePhotoUrl, setProfilePhotoUrl] = useState<string | null>(null);
  const [imageError, setImageError] = useState(false);

  const initials = user.name
    .split(' ')
    .map((n) => n[0])
    .join('')
    .slice(0, 2)
    .toUpperCase();

  // Load roles and photo from the identity service when the token lacks them.
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
            setProfilePhotoUrl(
              photoUrl.startsWith('http')
                ? photoUrl
                : `${import.meta.env.VITE_API_URL || 'https://localhost:5001'}${photoUrl}`,
            );
          }
        } catch {
          if (!cancelled) setApiRoles([]);
        }
      }
    };
    void fetchInfo();
    return () => {
      cancelled = true;
    };
  }, [user.id, user.roles, user.roleSource]);

  const effectiveRoles = apiRoles.length > 0 ? apiRoles : user.roles && user.roles.length > 0 ? user.roles : ['User'];
  const roleLabel = effectiveRoles.map(formatRoleLabel).join(', ');
  const displayPhoto = !imageError ? profilePhotoUrl || user.picture || null : null;

  const avatar = (
    <Avatar className="size-8 rounded-md">
      {displayPhoto && <AvatarImage src={displayPhoto} alt={user.name} onError={() => setImageError(true)} />}
      <AvatarFallback className="rounded-md bg-sidebar-accent text-xs text-sidebar-accent-foreground">
        {initials}
      </AvatarFallback>
    </Avatar>
  );

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <SidebarMenuButton
          size="lg"
          collapsed={collapsed}
          aria-label={collapsed ? `Account: ${user.name}` : undefined}
          className="data-[state=open]:bg-sidebar-accent"
        >
          {avatar}
          {!collapsed && (
            <>
              <span className="grid flex-1 text-left leading-tight">
                <span className="truncate text-sm font-medium">{user.name}</span>
                <span className="truncate text-xs text-sidebar-foreground/70">{user.email ?? roleLabel}</span>
              </span>
              <ChevronsUpDownIcon className="ml-auto text-sidebar-foreground/60" />
            </>
          )}
        </SidebarMenuButton>
      </DropdownMenuTrigger>
      <DropdownMenuContent className="min-w-60" side={collapsed ? 'right' : 'top'} align="end" sideOffset={4}>
        <DropdownMenuLabel className="p-0 font-normal">
          <div className="flex items-center gap-2 px-1 py-1.5 text-left text-sm">
            {avatar}
            <div className="grid flex-1 leading-tight">
              <span className="truncate font-medium">{user.name}</span>
              <span className="truncate text-xs text-muted-foreground">{roleLabel}</span>
            </div>
          </div>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem onSelect={() => navigate('/setup-guides')}>
          <BookOpenIcon />
          Guides
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuLabel className="text-xs text-muted-foreground">Theme</DropdownMenuLabel>
        <DropdownMenuRadioGroup value={theme} onValueChange={(value) => setTheme(value as typeof theme)}>
          <DropdownMenuRadioItem value="light">
            <SunIcon /> Light
          </DropdownMenuRadioItem>
          <DropdownMenuRadioItem value="dark">
            <MoonIcon /> Dark
          </DropdownMenuRadioItem>
          <DropdownMenuRadioItem value="system">
            <MonitorIcon /> System
          </DropdownMenuRadioItem>
        </DropdownMenuRadioGroup>
        <DropdownMenuSeparator />
        <DropdownMenuItem onSelect={onLogout}>
          <LogOutIcon />
          Log out
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

// ─── Sidebar ─────────────────────────────────────────────────────────────
export function AonikSidebar({ collapsed = false, onToggle }: AonikSidebarProps) {
  const { user, logout } = useAuth();
  const { sections } = useVisibleNav();

  const handleLogout = useCallback(async () => {
    try {
      await logout();
    } catch (err) {
      console.error('Logout failed', err);
    }
  }, [logout]);

  // Ctrl/Cmd+B toggles the rail, as in the shadcn Sidebar.
  useEffect(() => {
    if (!onToggle) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key.toLowerCase() === 'b' && (e.metaKey || e.ctrlKey) && !e.shiftKey && !e.altKey) {
        e.preventDefault();
        onToggle();
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [onToggle]);

  return (
    <aside
      data-slot="sidebar"
      data-state={collapsed ? 'collapsed' : 'expanded'}
      className={cn(
        'sticky top-0 z-40 flex h-screen shrink-0 flex-col border-r border-sidebar-border bg-sidebar text-sidebar-foreground transition-[width] duration-200 ease-linear',
        collapsed ? 'w-12' : 'w-64',
      )}
    >
      <div data-slot="sidebar-header" className={cn('flex flex-col gap-2 p-2', collapsed && 'items-center')}>
        <Link
          to="/"
          aria-label="Aonik home"
          className={cn('flex h-10 items-center rounded-md outline-hidden focus-visible:ring-2 focus-visible:ring-sidebar-ring', collapsed ? 'justify-center' : 'px-2')}
        >
          {collapsed ? <AonikMark size={22} /> : <AonikWordmark size={19} />}
        </Link>
        <WorkspaceSwitcher collapsed={collapsed} />
      </div>

      <nav
        data-slot="sidebar-content"
        aria-label="Main"
        className="flex min-h-0 flex-1 flex-col overflow-y-auto overflow-x-hidden"
      >
        {sections.map((section) => {
          if (section.items.length === 0) return null;
          return (
            <SidebarGroup key={section.id} className={cn(collapsed && 'items-center py-1')}>
              {!collapsed && section.label && <SidebarGroupLabel>{section.label}</SidebarGroupLabel>}
              <SidebarMenu className={cn(collapsed && 'items-center')}>
                {section.items.map((item) =>
                  item.id === 'workspace' ? (
                    <WorkspaceNavItemRow key={item.id} item={item} collapsed={collapsed} />
                  ) : (
                    <NavItemRow key={item.id} item={item} collapsed={collapsed} />
                  ),
                )}
              </SidebarMenu>
            </SidebarGroup>
          );
        })}
      </nav>

      {user && (
        <div data-slot="sidebar-footer" className={cn('border-t border-sidebar-border p-2', collapsed && 'flex justify-center')}>
          <UserMenu user={user} collapsed={collapsed} onLogout={handleLogout} />
        </div>
      )}
    </aside>
  );
}
