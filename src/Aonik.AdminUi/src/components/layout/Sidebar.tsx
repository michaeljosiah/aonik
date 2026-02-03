import { useEffect, useState, useRef } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { cn } from '@/lib/utils';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { useTheme } from '@/contexts';
import {
  Search,
  LayoutDashboard,
  Grid3x3,
  Sparkles,
  Folders,
  Users,
  Store,
  Settings,
  ChevronRight,
  PanelLeftClose,
  PanelLeft,
  Settings2,
  X,
  Award,
  UserCog,
  Info,
  FileText,
  Sun,
  Moon,
  Monitor,
  LogOut,
  // Navigation icons
  CreditCard,
  BookOpen,
  Building,
  Receipt,
  Building2,
  AlertTriangle,
  ArrowRightLeft,
  RotateCcw,
  ShieldAlert,
  Banknote,
  Landmark,
  ClipboardList,
  GitCompare,
  Bot,
  Brain,
  Workflow,
  MessageSquare,
  Shield,
  Key,
  Cog,
  KeyRound,
  Webhook,
  ScrollText,
  Hash,
  Wrench,
  Globe,
  Layers,
  Image,
  BarChart3,
} from 'lucide-react';
import type { NavItem, NavItemGroup, NavigationSection } from '@/types';
import { identityService } from '@/services/identityService';
import { navigationSections } from '@/data/mockData';
import { getWorkspacePanelForRoute } from '@/workspace/registry';
import { useAuth, type AuthUser } from '@/auth/useAuth';
import { isPortalAdmin as resolvePortalAdmin } from '@/lib/roleUtils';

const iconMap: Record<string, React.ElementType> = {
  Search,
  LayoutDashboard,
  Grid3x3,
  Sparkles,
  Folders,
  Users,
  Store,
  Settings,
  // Billing
  FileText,
  Receipt,
  Building2,
  AlertTriangle,
  // Payments
  CreditCard,
  ArrowRightLeft,
  RotateCcw,
  ShieldAlert,
  Banknote,
  // Ledger
  BookOpen,
  Landmark,
  ClipboardList,
  GitCompare,
  // AI & Agents
  Bot,
  Brain,
  Workflow,
  MessageSquare,
  // Users & Access
  UserCog,
  Shield,
  Key,
  // Tenants
  Building,
  // Settings
  Cog,
  KeyRound,
  Webhook,
  ScrollText,
  Hash,
  Wrench,
  // Catalog
  Globe,
  // CMS
  Layers,
  Image,
  BarChart3,
};

interface SidebarProps {
  collapsed?: boolean;
  onToggle?: () => void;
}

// Flyout menu component for grouped children
function FlyoutMenu({
  item,
  onClose,
  triggerRef,
}: {
  item: NavItem;
  onClose: () => void;
  triggerRef: React.RefObject<HTMLDivElement | null>;
}) {
  const location = useLocation();
  const menuRef = useRef<HTMLDivElement>(null);
  const [position, setPosition] = useState({ top: 0, left: 0 });

  const resolveHref = (href?: string) => {
    if (!href) return '#';
    const panel = getWorkspacePanelForRoute(href);
    if (panel) {
      return `/workspace?panel=${panel.id}`;
    }
    return href;
  };

  // Calculate position based on trigger element
  useEffect(() => {
    if (triggerRef.current) {
      const rect = triggerRef.current.getBoundingClientRect();
      setPosition({
        top: rect.top - 4,
        left: rect.right + 2, // Tighter gap (2px)
      });
    }
  }, [triggerRef]);

  // Get all items (either from childGroups or children)
  const groups: NavItemGroup[] = item.childGroups || (item.children ? [{ label: '', items: item.children }] : []);

  return (
    <div
      ref={menuRef}
      className="flyout-menu fixed w-56 bg-[var(--color-surface)] rounded-md shadow-lg border border-[var(--color-border)] z-[9999] overflow-hidden"
      style={{ top: `${position.top}px`, left: `${position.left}px` }}
      onMouseLeave={onClose}
    >
      {/* Groups */}
      <div className="py-1.5 max-h-80 overflow-y-auto">
        {groups.map((group, groupIndex) => (
          <div key={group.label || groupIndex}>
            {group.label && (
              <div className="px-3 pt-1.5 pb-0.5">
                <span className="text-[10px] font-semibold uppercase tracking-wider text-[var(--color-text-tertiary)]">
                  {group.label}
                </span>
              </div>
            )}
            <div className="px-1.5">
              {group.items.map((child) => {
                const ChildIcon = iconMap[child.icon] || LayoutDashboard;
                const isActive = child.href === location.pathname;
                return (
                  <Link
                    key={child.id}
                    to={resolveHref(child.href)}
                    className={cn(
                      'flex items-center gap-2.5 px-2 py-1 rounded-md text-sm transition-colors',
                      isActive
                        ? 'bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)]'
                        : 'text-[var(--color-text-secondary)] hover:bg-[var(--color-sidebar-hover)] hover:text-[var(--color-text-primary)]'
                    )}
                    onClick={onClose}
                  >
                    <ChildIcon className="w-4 h-4 shrink-0" />
                    <span className="truncate">{child.label}</span>
                  </Link>
                );
              })}
            </div>
            {groupIndex < groups.length - 1 && (
              <div className="my-1 mx-3 border-t border-[var(--color-border-light)]" />
            )}
          </div>
        ))}
      </div>

      {/* Footer - View all link */}
      {item.viewAllHref && (
        <div className="px-1.5 py-1 border-t border-[var(--color-border-light)]">
          <Link
            to={resolveHref(item.viewAllHref)}
            className="flex items-center justify-center gap-1 px-2 py-1 rounded-sm text-sm text-[var(--color-brand-primary)] hover:bg-[var(--color-sidebar-hover)] transition-colors"
            onClick={onClose}
          >
            <span>{item.viewAllLabel || 'View all'}</span>
          </Link>
        </div>
      )}
    </div>
  );
}

function NavItemComponent({
  item,
  collapsed,
}: {
  item: NavItem;
  collapsed: boolean;
}) {
  const location = useLocation();
  const [showFlyout, setShowFlyout] = useState(false);
  const [clickedOpen, setClickedOpen] = useState(false);
  const hoverTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const triggerRef = useRef<HTMLDivElement>(null);
  const Icon = iconMap[item.icon] || LayoutDashboard;
  const isWorkspace = location.pathname === '/workspace';
  const activeWorkspaceHref = sessionStorage.getItem('aonik:active-workspace-href');
  const isActive = item.href === location.pathname || (isWorkspace && item.href === activeWorkspaceHref);
  const hasChildren = (item.childGroups && item.childGroups.length > 0) || (item.children && item.children.length > 0);

  // Clean up timeout on unmount
  useEffect(() => {
    return () => {
      if (hoverTimeoutRef.current) {
        clearTimeout(hoverTimeoutRef.current);
      }
    };
  }, []);

  const handleMouseEnter = () => {
    if (!collapsed && hasChildren) {
      // Small delay to prevent accidental triggers
      hoverTimeoutRef.current = setTimeout(() => {
        setShowFlyout(true);
      }, 100);
    }
  };

  const handleMouseLeave = () => {
    if (hoverTimeoutRef.current) {
      clearTimeout(hoverTimeoutRef.current);
    }
    if (!clickedOpen) {
      setShowFlyout(false);
    }
  };

  const handleCollapsedToggleClick = () => {
    if (collapsed && hasChildren) {
      // In collapsed mode, click toggles flyout
      setClickedOpen(!clickedOpen);
      setShowFlyout(!showFlyout);
    }
  };

  const handleFlyoutClose = () => {
    setShowFlyout(false);
    setClickedOpen(false);
  };

  const baseClasses = cn(
    'flex items-center gap-3 px-3 py-2.5 rounded-sm text-sm font-medium transition-all duration-200 cursor-pointer relative',
    'hover:bg-[var(--color-sidebar-hover)]',
    isActive && 'bg-[var(--color-sidebar-active)] text-white hover:bg-[var(--color-sidebar-active)]',
    !isActive && 'text-[var(--color-text-secondary)]',
    showFlyout && !isActive && 'bg-[var(--color-sidebar-hover)]'
  );

  const content = (
    <>
      <Icon className={cn('w-5 h-5 shrink-0', isActive ? 'text-white' : 'text-[var(--color-text-secondary)]')} />
      {!collapsed && (
        <>
          <span className="flex-1 truncate">{item.label}</span>
          {hasChildren && (
            <ChevronRight className={cn(
              'w-4 h-4 text-[var(--color-text-tertiary)] transition-transform',
              showFlyout && 'rotate-90'
            )} />
          )}
        </>
      )}
    </>
  );

  // For collapsed sidebar with children, show tooltip on hover
  if (collapsed && hasChildren) {
    return (
      <div ref={triggerRef} className="relative" onMouseLeave={handleFlyoutClose}>
        <Tooltip>
          <TooltipTrigger asChild>
            <div className={baseClasses} onClick={handleCollapsedToggleClick}>
              {content}
            </div>
          </TooltipTrigger>
          {!showFlyout && (
            <TooltipContent side="right" sideOffset={8}>
              <p>{item.label}</p>
            </TooltipContent>
          )}
        </Tooltip>
        {showFlyout && <FlyoutMenu item={item} onClose={handleFlyoutClose} triggerRef={triggerRef} />}
      </div>
    );
  }

  // For collapsed sidebar without children, show tooltip
  if (collapsed && !hasChildren) {
    const href = item.href || '#';
    const panel = getWorkspacePanelForRoute(href);
    const targetHref = panel ? `/workspace?panel=${panel.id}` : href;
    const handleCollapsedWorkspaceClick = () => {
      if (panel) {
        sessionStorage.setItem('aonik:active-workspace-href', href);
      }
    };
    return (
      <Tooltip>
        <TooltipTrigger asChild>
          <Link to={targetHref} className={baseClasses} onClick={handleCollapsedWorkspaceClick}>
            {content}
          </Link>
        </TooltipTrigger>
        <TooltipContent side="right" sideOffset={8}>
          <p>{item.label}</p>
        </TooltipContent>
      </Tooltip>
    );
  }

  // For expanded sidebar with children, show flyout on hover
  if (hasChildren) {
    return (
      <div
        ref={triggerRef}
        className="relative"
        onMouseEnter={handleMouseEnter}
        onMouseLeave={handleMouseLeave}
      >
        <div className={baseClasses}>
          {content}
        </div>
        {showFlyout && <FlyoutMenu item={item} onClose={handleFlyoutClose} triggerRef={triggerRef} />}
      </div>
    );
  }

  // For expanded sidebar without children, simple link
  const href = item.href || '#';
  const panel = getWorkspacePanelForRoute(href);
  const targetHref = panel ? `/workspace?panel=${panel.id}` : href;
  const handleExpandedWorkspaceClick = () => {
    if (panel) {
      sessionStorage.setItem('aonik:active-workspace-href', href);
    }
  };
  return (
    <Link to={targetHref} className={baseClasses} onClick={handleExpandedWorkspaceClick}>
      {content}
    </Link>
  );
}

const formatRoleLabel = (role: string) =>
  role
    .replace(/[_-]+/g, ' ')
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/\b\w/g, (char) => char.toUpperCase());

function UserProfile({ user, collapsed, onLogout }: { user: AuthUser; collapsed: boolean; onLogout: () => void }) {
  const [isExpanded, setIsExpanded] = useState(false);
  const [apiRoles, setApiRoles] = useState<string[]>([]);
  const [isLoadingRoles, setIsLoadingRoles] = useState(false);
  const [profilePhotoUrl, setProfilePhotoUrl] = useState<string | null>(null);
  const [imageLoading, setImageLoading] = useState(false);
  const [imageError, setImageError] = useState(false);
  const { theme, setTheme } = useTheme();
  
  const initials = user.name
    .split(' ')
    .map((n) => n[0])
    .join('')
    .toUpperCase();

  // Fetch roles and profile photo from API if not available in claims
  useEffect(() => {
    const fetchRoles = async () => {
      if (user.roleSource === 'api' || (!user.roles || user.roles.length === 0)) {
        setIsLoadingRoles(true);
        try {
          const response = await identityService.getUserInfo();
          setApiRoles(response.roles);
          // Use small thumbnail for sidebar (128x128), fallback to tiny (64x64) or original
          const photoUrl = response.photoUrlSmall || response.photoUrlTiny || response.photoUrl;
          if (photoUrl) {
            // Convert relative path to absolute URL
            const fullPhotoUrl = photoUrl.startsWith('http') 
              ? photoUrl 
              : `${import.meta.env.VITE_API_URL || 'https://localhost:5001'}${photoUrl}`;
            setProfilePhotoUrl(fullPhotoUrl);
            setImageError(false);
            // Don't set imageLoading here - let the image onLoad/onError handlers manage it
          }
        } catch (error) {
          console.error('Failed to fetch user info:', error);
          setApiRoles([]);
        } finally {
          setIsLoadingRoles(false);
        }
      }
    };

    fetchRoles();
  }, [user.id, user.roles, user.roleSource]);

  // Manage image loading state with timeout fallback
  useEffect(() => {
    if (profilePhotoUrl && !imageError) {
      setImageLoading(true);
      
      // Fallback timeout to prevent infinite loading (5 seconds)
      const timeout = setTimeout(() => {
        setImageLoading(false);
        console.warn('Image loading timeout - falling back to initials');
      }, 5000);
      
      return () => clearTimeout(timeout);
    } else {
      setImageLoading(false);
    }
  }, [profilePhotoUrl, imageError]);

  // Determine display role from roles array (prefer API roles if available)
  const effectiveRoles = apiRoles.length > 0 ? apiRoles : (user.roles && user.roles.length > 0 ? user.roles : ['User']);
  const roleLabel = isLoadingRoles ? 'Loading...' : effectiveRoles.map((role) => formatRoleLabel(role)).join(', ');
  
  // Check if user has admin role
  const isAdmin = effectiveRoles.some(role => 
    role.toLowerCase().includes('admin') || 
    role.toLowerCase().includes('administrator')
  );

  // Handler for successful image load
  const handleImageLoad = () => {
    setImageLoading(false);
    setImageError(false);
  };

  // Handler for image error
  const handleImageError = () => {
    setImageLoading(false);
    setImageError(true);
  };

  // Get the photo URL to display (uploaded photo takes priority over Auth0 picture)
  const displayPhotoUrl = !imageError ? (profilePhotoUrl || user.picture) : null;

  if (collapsed) {
    return (
      <div className="flex justify-center p-3 border-t border-[var(--color-border-light)]">
        <Avatar className="w-9 h-9 cursor-pointer relative">
          {displayPhotoUrl && (
            <>
              <AvatarImage 
                src={displayPhotoUrl} 
                alt={user.name}
                onLoad={handleImageLoad}
                onError={handleImageError}
                className={imageLoading ? 'opacity-0' : 'opacity-100 transition-opacity duration-200'}
              />
              {imageLoading && (
                <div className="absolute inset-0 flex items-center justify-center bg-[var(--color-surface-inset)]">
                  <div className="w-5 h-5 border-2 border-[var(--color-border-light)] border-t-[var(--color-brand-primary)] rounded-full animate-spin" />
                </div>
              )}
            </>
          )}
          <AvatarFallback className="bg-[var(--color-brand-secondary-light)] text-[var(--color-brand-secondary)]">
            {initials}
          </AvatarFallback>
        </Avatar>
      </div>
    );
  }

  const menuItems = [
    { icon: FileText, label: 'Guides', href: '/setup-guides' },
    { icon: Award, label: 'API Documentation' },
    { icon: UserCog, label: 'Manage profile' },
    { icon: Info, label: 'About Aonik' },
    { icon: FileText, label: 'Release notes' },
  ];

  return (
    <div className={cn(
      "border-t border-[var(--color-border-light)] p-3",
      !isExpanded && "pt-9",
      isExpanded && "pt-9"
    )}>
      {/* Card container with relative positioning for avatar overlap */}
      <div className={cn(
        "relative",
        isExpanded && "w-80 z-50"
      )}>
        {/* Avatar - positioned to overlap the top of the card (40% above, 60% below) */}
        <Avatar 
          className={cn(
            "absolute left-4 cursor-pointer border-4 border-[var(--color-background)] z-10",
            isExpanded ? "w-16 h-16 -top-6" : "w-16 h-16 -top-6"
          )}
          onClick={() => setIsExpanded(!isExpanded)}
        >
          {displayPhotoUrl && (
            <>
              <AvatarImage 
                src={displayPhotoUrl} 
                alt={user.name}
                onLoad={handleImageLoad}
                onError={handleImageError}
                className={imageLoading ? 'opacity-0' : 'opacity-100 transition-opacity duration-200'}
              />
              {imageLoading && (
                <div className="absolute inset-0 flex items-center justify-center bg-[var(--color-surface-inset)]">
                  <div className={cn(
                    "border-2 border-[var(--color-border-light)] border-t-[var(--color-brand-primary)] rounded-full animate-spin",
                    "w-8 h-8"
                  )} />
                </div>
              )}
            </>
          )}
          <AvatarFallback className={cn(
            "bg-[var(--color-brand-secondary-light)] text-[var(--color-brand-secondary)]",
            "text-2xl"
          )}>
            {initials}
          </AvatarFallback>
        </Avatar>

        <div className="bg-[var(--color-surface-elevated)] rounded-md shadow-lg border border-[var(--color-border)]">
        {!isExpanded ? (
          /* Collapsed card view */
          <div className="p-4 pt-3">
            {/* Top row: Admin badge + settings (avatar moved outside) */}
            <div className="flex items-start justify-end mb-6">
              <div className="flex items-center gap-2">
                {isAdmin && (
                  <Badge variant="team" className="text-[11px] px-2 py-0.5">
                    Admin
                  </Badge>
                )}
                 <button 
                   className="p-1.5 rounded-md hover:bg-[var(--color-background)] text-[var(--color-text-tertiary)]"
                   onClick={() => setIsExpanded(true)}
                 >
                   <Settings2 className="w-5 h-5" />
                 </button>
              </div>
            </div>
            {/* Name and role */}
            <div className="min-w-0">
              <Tooltip>
                <TooltipTrigger asChild>
                  <p className="text-base font-semibold text-[var(--color-text-primary)] truncate cursor-default">
                    {user.name}
                  </p>
                </TooltipTrigger>
                <TooltipContent side="top">
                  <p>{user.name}</p>
                </TooltipContent>
              </Tooltip>
              <p className="text-sm text-[var(--color-text-tertiary)] truncate mt-0.5">{roleLabel}</p>
            </div>
          </div>
        ) : (
          /* Expanded card view */
          <div className="p-4 pt-3">
            {/* Top row: Admin badge + close button */}
            <div className="flex items-center justify-end gap-2 mb-6">
              {isAdmin && (
                <Badge variant="team" className="text-[11px] px-2 py-0.5">
                  Admin
                </Badge>
              )}
               <button 
                 className="p-1.5 rounded-md hover:bg-[var(--color-background)] text-[var(--color-text-tertiary)]"
                 onClick={() => setIsExpanded(false)}
               >
                 <X className="w-5 h-5" />
               </button>
            </div>

            {/* Name and role */}
            <div className="mb-4 min-w-0">
              <Tooltip>
                <TooltipTrigger asChild>
                  <p className="text-base font-semibold text-[var(--color-text-primary)] truncate cursor-default">
                    {user.name}
                  </p>
                </TooltipTrigger>
                <TooltipContent side="top">
                  <p>{user.name}</p>
                </TooltipContent>
              </Tooltip>
              <p className="text-sm text-[var(--color-text-tertiary)] truncate mt-0.5">{roleLabel}</p>
            </div>

            {/* Menu items */}
            <div className="space-y-1 py-3 border-t border-[var(--color-border-light)]">
              {menuItems.map((item) => (
                <button
                  key={item.label}
                  className="flex items-center gap-3 w-full px-2 py-2.5 rounded-md text-sm text-[var(--color-text-primary)] hover:bg-[var(--color-background)] transition-colors"
                  onClick={() => {
                    if (item.href) {
                      window.location.href = item.href;
                    }
                  }}
                >
                  <item.icon className="w-5 h-5 text-[var(--color-text-secondary)]" />
                  {item.label}
                </button>
              ))}
            </div>

            {/* Theme switcher */}
            <div className="py-3 border-t border-[var(--color-border-light)]">
              <p className="text-sm font-medium text-[var(--color-text-primary)] mb-2">Theme</p>
              <div className="flex bg-[var(--color-background)] rounded-md p-1">
                <button
                  onClick={() => setTheme('light')}
                  className={cn(
                    "flex-1 flex items-center justify-center gap-1.5 py-2 px-3 rounded-md text-xs font-medium transition-colors",
                    theme === 'light' 
                      ? "bg-[var(--color-surface)] shadow-sm text-[var(--color-text-primary)]" 
                      : "text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]"
                  )}
                >
                  <Sun className="w-4 h-4" />
                  Light
                </button>
                <button
                  onClick={() => setTheme('dark')}
                  className={cn(
                    "flex-1 flex items-center justify-center gap-1.5 py-2 px-3 rounded-md text-xs font-medium transition-colors",
                    theme === 'dark' 
                      ? "bg-[var(--color-surface)] shadow-sm text-[var(--color-text-primary)]" 
                      : "text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]"
                  )}
                >
                  <Moon className="w-4 h-4" />
                  Dark
                </button>
                <button
                  onClick={() => setTheme('system')}
                  className={cn(
                    "flex-1 flex items-center justify-center gap-1.5 py-2 px-3 rounded-md text-xs font-medium transition-colors",
                    theme === 'system' 
                      ? "bg-[var(--color-brand-primary)] text-white" 
                      : "text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]"
                  )}
                >
                  <Monitor className="w-4 h-4" />
                  System
                </button>
              </div>
            </div>

            {/* Log out */}
            <div className="pt-3 border-t border-[var(--color-border-light)]">
              <button 
                onClick={onLogout}
                className="flex items-center gap-3 w-full px-2 py-2.5 rounded-md text-sm text-[var(--color-text-primary)] hover:bg-[var(--color-background)] transition-colors"
              >
                <LogOut className="w-5 h-5 text-[var(--color-text-secondary)]" />
                Log out
              </button>
            </div>
          </div>
        )}
        </div>
      </div>
    </div>
  );
}

export function Sidebar({ collapsed = false, onToggle }: SidebarProps) {
  const { user, logout } = useAuth();
  const [navRoles, setNavRoles] = useState<string[]>([]);
  const [isLoadingNavRoles, setIsLoadingNavRoles] = useState(false);

  const handleLogout = async () => {
    try {
      await logout();
    } catch (error) {
      console.error('Logout failed:', error);
    }
  };

  useEffect(() => {
    const hydrateRoles = async () => {
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
        const response = await identityService.getUserInfo();
        setNavRoles(response.roles);
      } catch (error) {
        console.error('Failed to fetch user roles for navigation:', error);
        setNavRoles([]);
      } finally {
        setIsLoadingNavRoles(false);
      }
    };

    hydrateRoles();
  }, [user]);

  const isPortalAdmin = resolvePortalAdmin(navRoles);

  const visibleSections = navigationSections.filter((section) => {
    if (section.audience === 'host') {
      return isPortalAdmin;
    }
    if (section.audience === 'tenant') {
      return !isPortalAdmin && !isLoadingNavRoles;
    }
    return true;
  });

  const isNavItemVisible = (item: NavItem) => {
    if (item.audience === 'host') {
      return isPortalAdmin;
    }
    if (item.audience === 'tenant') {
      return !isPortalAdmin && !isLoadingNavRoles;
    }
    return true;
  };

  const filterNavItems = (items: NavItem[]) => {
    return items.reduce<NavItem[]>((acc, item) => {
      if (!isNavItemVisible(item)) {
        return acc;
      }

      const filteredChildren = item.children?.filter(isNavItemVisible);
      const filteredChildGroups = item.childGroups
        ?.map((group) => ({
          ...group,
          items: group.items.filter(isNavItemVisible),
        }))
        .filter((group) => group.items.length > 0);

      acc.push({
        ...item,
        children: filteredChildren,
        childGroups: filteredChildGroups,
      });

      return acc;
    }, []);
  };

  return (
    <TooltipProvider delayDuration={300}>
      <aside
        className={cn(
          'sticky top-0 flex flex-col h-screen bg-[var(--color-sidebar-bg)] border-r border-[var(--color-border-light)] transition-all duration-300 z-40',
          collapsed ? 'w-16' : 'w-60'
        )}
      >
        {/* Logo */}
        <div className={cn(
          'flex items-center h-14 px-4 border-b border-[var(--color-border-light)] shrink-0',
          collapsed && 'justify-center px-2'
        )}>
          {!collapsed ? (
            <Link to="/" className="flex items-center gap-1">
              <span className="text-xl font-bold text-[var(--color-text-primary)]">Aonik</span>
              <span className="text-xl text-[var(--color-brand-secondary)]">.</span>
            </Link>
          ) : (
            <Link to="/" className="text-xl font-bold text-[var(--color-brand-primary)]">A</Link>
          )}
          <button
            onClick={onToggle}
            className={cn(
              'p-1.5 rounded-sm hover:bg-[var(--color-sidebar-hover)] text-[var(--color-text-tertiary)] transition-colors',
              collapsed ? 'ml-0' : 'ml-auto'
            )}
          >
            {collapsed ? <PanelLeft className="w-4 h-4" /> : <PanelLeftClose className="w-4 h-4" />}
          </button>
        </div>

        {/* Navigation */}
        <nav className="flex-1 overflow-y-auto p-3 space-y-3">
          {visibleSections.map((section: NavigationSection) => {
            const sectionItems = filterNavItems(section.items);
            if (sectionItems.length === 0) {
              return null;
            }

            return (
              <div key={section.id} className="space-y-1">
                {section.label && (
                  <div className="px-3 pt-2 pb-1">
                    <span className="text-[10px] font-semibold uppercase tracking-wider text-[var(--color-text-tertiary)]">
                      {section.label}
                    </span>
                  </div>
                )}
                {sectionItems.map((item) => (
                  <NavItemComponent key={item.id} item={item} collapsed={collapsed} />
                ))}
              </div>
            );
          })}
        </nav>

        {/* User Profile - fixed at bottom */}
        <div className="shrink-0 mt-auto">
          {user && <UserProfile user={user} collapsed={collapsed} onLogout={handleLogout} />}
        </div>
      </aside>
    </TooltipProvider>
  );
}
