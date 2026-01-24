import { useEffect, useState } from 'react';
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
  ChevronDown,
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
  Globe,
} from 'lucide-react';
import type { NavItem } from '@/types';
import { identityService } from '@/services/identityService';
import { navigationItems } from '@/data/mockData';
import { useAuth, type AuthUser } from '@/auth/useAuth';

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
  // Catalog
  Globe,
};

interface SidebarProps {
  collapsed?: boolean;
  onToggle?: () => void;
}

function NavItemComponent({
  item,
  collapsed,
  level = 0,
}: {
  item: NavItem;
  collapsed: boolean;
  level?: number;
}) {
  const location = useLocation();
  const [expanded, setExpanded] = useState(false);
  const Icon = iconMap[item.icon] || LayoutDashboard;
  const isActive = item.href === location.pathname;
  const hasChildren = item.children && item.children.length > 0;

  const handleClick = () => {
    if (hasChildren) {
      setExpanded(!expanded);
    }
  };

  const baseClasses = cn(
    'flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all duration-200 cursor-pointer',
    'hover:bg-[var(--color-sidebar-hover)]',
    isActive && 'bg-[var(--color-sidebar-active)] text-white hover:bg-[var(--color-sidebar-active)]',
    !isActive && 'text-[var(--color-text-secondary)]',
    level > 0 && 'ml-6 text-[13px]'
  );

  const content = (
    <>
      <Icon className={cn('w-5 h-5 shrink-0', isActive ? 'text-white' : 'text-[var(--color-text-secondary)]')} />
      {!collapsed && (
        <>
          <span className="flex-1 truncate">{item.label}</span>
          {hasChildren && (
            <span className="text-[var(--color-text-tertiary)]">
              {expanded ? <ChevronDown className="w-4 h-4" /> : <ChevronRight className="w-4 h-4" />}
            </span>
          )}
        </>
      )}
    </>
  );

  return (
    <div>
      {item.href && !hasChildren ? (
        <Link to={item.href} className={baseClasses}>
          {content}
        </Link>
      ) : (
        <div className={baseClasses} onClick={handleClick}>
          {content}
        </div>
      )}
      {hasChildren && expanded && !collapsed && (
        <div className="mt-1 space-y-1">
          {item.children?.map((child) => (
            <NavItemComponent key={child.id} item={child} collapsed={collapsed} level={level + 1} />
          ))}
        </div>
      )}
    </div>
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
  const { theme, setTheme } = useTheme();
  
  const initials = user.name
    .split(' ')
    .map((n) => n[0])
    .join('')
    .toUpperCase();

  // Fetch roles from API if not available in claims
  useEffect(() => {
    const fetchRoles = async () => {
      if (user.roleSource === 'api' || (!user.roles || user.roles.length === 0)) {
        setIsLoadingRoles(true);
        try {
          const response = await identityService.getUserInfo();
          setApiRoles(response.roles);
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

  // Determine display role from roles array (prefer API roles if available)
  const effectiveRoles = apiRoles.length > 0 ? apiRoles : (user.roles && user.roles.length > 0 ? user.roles : ['User']);
  const roleLabel = isLoadingRoles ? 'Loading...' : effectiveRoles.map((role) => formatRoleLabel(role)).join(', ');
  
  // Check if user has admin role
  const isAdmin = effectiveRoles.some(role => 
    role.toLowerCase().includes('admin') || 
    role.toLowerCase().includes('administrator')
  );

  if (collapsed) {
    return (
      <div className="flex justify-center p-3 border-t border-[var(--color-border-light)]">
        <Avatar className="w-10 h-10 cursor-pointer">
          {user.picture && <AvatarImage src={user.picture} alt={user.name} />}
          <AvatarFallback className="bg-[var(--color-brand-secondary-light)] text-[var(--color-brand-secondary)]">
            {initials}
          </AvatarFallback>
        </Avatar>
      </div>
    );
  }

  const menuItems = [
    { icon: Award, label: 'API Documentation' },
    { icon: UserCog, label: 'Manage profile' },
    { icon: Info, label: 'About Aonik' },
    { icon: FileText, label: 'Release notes' },
  ];

  return (
    <div className={cn(
      "border-t border-[var(--color-border-light)] p-3",
      isExpanded && "pt-10"
    )}>
      {/* Card container with relative positioning for avatar overlap */}
      <div className={cn(
        "relative",
        isExpanded && "w-80 z-50"
      )}>
        {/* Avatar - positioned to overlap the top of the card when expanded */}
        {isExpanded && (
          <Avatar 
            className="w-16 h-16 absolute -top-8 left-4 cursor-pointer border-4 border-[var(--color-sidebar-bg)] z-10"
            onClick={() => setIsExpanded(false)}
          >
            {user.picture && <AvatarImage src={user.picture} alt={user.name} />}
            <AvatarFallback className="bg-[var(--color-brand-secondary-light)] text-[var(--color-brand-secondary)] text-xl">
              {initials}
            </AvatarFallback>
          </Avatar>
        )}

        <div className="bg-[var(--color-surface-elevated)] rounded-xl shadow-lg border border-[var(--color-border)]">
        {!isExpanded ? (
          /* Collapsed card view */
          <div className="p-4">
            {/* Top row: Avatar and Admin badge + settings */}
            <div className="flex items-start justify-between mb-3">
              <Avatar 
                className="w-12 h-12 cursor-pointer"
                onClick={() => setIsExpanded(true)}
              >
                {user.picture && <AvatarImage src={user.picture} alt={user.name} />}
                <AvatarFallback className="bg-[var(--color-brand-secondary-light)] text-[var(--color-brand-secondary)] text-lg">
                  {initials}
                </AvatarFallback>
              </Avatar>
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
                  <p className="text-sm font-semibold text-[var(--color-text-primary)] truncate cursor-default">
                    {user.name}
                  </p>
                </TooltipTrigger>
                <TooltipContent side="top">
                  <p>{user.name}</p>
                </TooltipContent>
              </Tooltip>
              <p className="text-xs text-[var(--color-text-tertiary)] truncate">{roleLabel}</p>
            </div>
          </div>
        ) : (
          /* Expanded card view */
          <div className="p-4">
            {/* Top row: Admin badge + close button */}
            <div className="flex items-center justify-end gap-2 mb-4">
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
              <p className="text-sm text-[var(--color-brand-primary)] truncate">{roleLabel}</p>
            </div>

            {/* Menu items */}
            <div className="space-y-1 py-3 border-t border-[var(--color-border-light)]">
              {menuItems.map((item) => (
                <button
                  key={item.label}
                  className="flex items-center gap-3 w-full px-2 py-2.5 rounded-lg text-sm text-[var(--color-text-primary)] hover:bg-[var(--color-background)] transition-colors"
                >
                  <item.icon className="w-5 h-5 text-[var(--color-text-secondary)]" />
                  {item.label}
                </button>
              ))}
            </div>

            {/* Theme switcher */}
            <div className="py-3 border-t border-[var(--color-border-light)]">
              <p className="text-sm font-medium text-[var(--color-text-primary)] mb-2">Theme</p>
              <div className="flex bg-[var(--color-background)] rounded-lg p-1">
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
                className="flex items-center gap-3 w-full px-2 py-2.5 rounded-lg text-sm text-[var(--color-text-primary)] hover:bg-[var(--color-background)] transition-colors"
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

  const handleLogout = async () => {
    try {
      await logout();
    } catch (error) {
      console.error('Logout failed:', error);
    }
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
            <Link to="/" className="text-xl font-bold text-[var(--color-brand-primary)]">C</Link>
          )}
          <button
            onClick={onToggle}
            className={cn(
              'p-1.5 rounded-md hover:bg-[var(--color-sidebar-hover)] text-[var(--color-text-tertiary)] transition-colors',
              collapsed ? 'ml-0' : 'ml-auto'
            )}
          >
            {collapsed ? <PanelLeft className="w-4 h-4" /> : <PanelLeftClose className="w-4 h-4" />}
          </button>
        </div>

        {/* Navigation */}
        <nav className="flex-1 overflow-y-auto p-3 space-y-1">
          {navigationItems.map((item) => (
            <NavItemComponent key={item.id} item={item} collapsed={collapsed} />
          ))}
        </nav>

        {/* User Profile - fixed at bottom */}
        <div className="shrink-0 mt-auto">
          {user && <UserProfile user={user} collapsed={collapsed} onLogout={handleLogout} />}
        </div>
      </aside>
    </TooltipProvider>
  );
}
