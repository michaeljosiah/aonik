import { useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { cn } from '@/lib/utils';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
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
} from 'lucide-react';
import type { NavItem, User } from '@/types';
import { navigationItems, currentUser } from '@/data/mockData';

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

function UserProfile({ user, collapsed }: { user: User; collapsed: boolean }) {
  const [isExpanded, setIsExpanded] = useState(false);
  const [theme, setTheme] = useState<'light' | 'dark' | 'system'>('system');
  
  const initials = user.name
    .split(' ')
    .map((n) => n[0])
    .join('')
    .toUpperCase();

  if (collapsed) {
    return (
      <div className="flex justify-center p-3 border-t border-[var(--color-border-light)]">
        <Avatar className="w-10 h-10 cursor-pointer">
          {user.avatar && <AvatarImage src={user.avatar} alt={user.name} />}
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
            {user.avatar && <AvatarImage src={user.avatar} alt={user.name} />}
            <AvatarFallback className="bg-[var(--color-brand-secondary-light)] text-[var(--color-brand-secondary)] text-xl">
              {initials}
            </AvatarFallback>
          </Avatar>
        )}

        <div className="bg-white rounded-xl shadow-lg">
        {!isExpanded ? (
          /* Collapsed card view */
          <div className="p-4">
            {/* Top row: Avatar and Admin badge + settings */}
            <div className="flex items-start justify-between mb-3">
              <Avatar 
                className="w-12 h-12 cursor-pointer"
                onClick={() => setIsExpanded(true)}
              >
                {user.avatar && <AvatarImage src={user.avatar} alt={user.name} />}
                <AvatarFallback className="bg-[var(--color-brand-secondary-light)] text-[var(--color-brand-secondary)] text-lg">
                  {initials}
                </AvatarFallback>
              </Avatar>
              <div className="flex items-center gap-2">
                <Badge variant="team" className="text-[11px] px-2 py-0.5">
                  Admin
                </Badge>
                <button 
                  className="p-1.5 rounded-md hover:bg-[var(--color-background)] text-[var(--color-text-tertiary)]"
                  onClick={() => setIsExpanded(true)}
                >
                  <Settings2 className="w-5 h-5" />
                </button>
              </div>
            </div>
            {/* Name and role */}
            <div>
              <p className="text-sm font-semibold text-[var(--color-text-primary)]">
                {user.name}
              </p>
              <p className="text-xs text-[var(--color-text-tertiary)]">{user.role}</p>
            </div>
          </div>
        ) : (
          /* Expanded card view */
          <div className="p-4">
            {/* Top row: Admin badge + close button */}
            <div className="flex items-center justify-end gap-2 mb-4">
              <Badge variant="team" className="text-[11px] px-2 py-0.5">
                Admin
              </Badge>
              <button 
                className="p-1.5 rounded-md hover:bg-[var(--color-background)] text-[var(--color-text-tertiary)]"
                onClick={() => setIsExpanded(false)}
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Name and role */}
            <div className="mb-4">
              <p className="text-base font-semibold text-[var(--color-text-primary)]">
                {user.name}
              </p>
              <p className="text-sm text-[var(--color-brand-primary)]">{user.role}</p>
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
                      ? "bg-white shadow-sm text-[var(--color-text-primary)]" 
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
                      ? "bg-white shadow-sm text-[var(--color-text-primary)]" 
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
              <button className="flex items-center gap-3 w-full px-2 py-2.5 rounded-lg text-sm text-[var(--color-text-primary)] hover:bg-[var(--color-background)] transition-colors">
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
  return (
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
        <UserProfile user={currentUser} collapsed={collapsed} />
      </div>
    </aside>
  );
}
