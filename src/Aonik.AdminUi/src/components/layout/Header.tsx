import { Home, Bell, MessageSquareText, Maximize2, Minimize2, Sun, Moon, Monitor, Building2, Plus, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { useTheme } from '@/contexts';
import { useState, useRef, useEffect } from 'react';
import { getSelectedTenant } from '@/lib/tenantContext';
import { NotificationsPanel } from '@/components/layout/NotificationsPanel';

import { loadWorkspaceState } from '@/workspace/storage';

interface WorkspaceTab {
  id: string;
  name: string;
  isDefault: boolean;
  updatedAt: string;
}

interface HeaderProps {
  title?: string;
  breadcrumb?: string[];
  leftSlot?: React.ReactNode;
  onFullscreenChange?: (isFullscreen: boolean) => void;
  onWorkspaceReset?: () => void;
  isWorkspace?: boolean;
  onAiChatToggle?: () => void;
}

export function Header({ breadcrumb = ['My Space'], leftSlot, onFullscreenChange, onWorkspaceReset, isWorkspace, onAiChatToggle }: HeaderProps) {
  const { theme, resolvedTheme, setTheme } = useTheme();
  const [showThemeMenu, setShowThemeMenu] = useState(false);
  const [showNotifications, setShowNotifications] = useState(false);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const [tenantLabel, setTenantLabel] = useState<string | null>(null);
  const [tenantEnv, setTenantEnv] = useState<string | null>(null);
  const [workspaceTabs, setWorkspaceTabs] = useState<WorkspaceTab[]>([]);
  const [activeWorkspaceId, setActiveWorkspaceId] = useState('');
  const [editingWorkspaceId, setEditingWorkspaceId] = useState<string | null>(null);
  const [editingName, setEditingName] = useState('');
  const editingInputRef = useRef<HTMLInputElement>(null);
  const [isCreateWorkspaceOpen, setIsCreateWorkspaceOpen] = useState(false);
  const [newWorkspaceName, setNewWorkspaceName] = useState('');
  const [confirmWorkspaceId, setConfirmWorkspaceId] = useState<string | null>(null);
  const [confirmWorkspaceName, setConfirmWorkspaceName] = useState('');

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

  useEffect(() => {
    if (!isWorkspace) {
      setWorkspaceTabs([]);
      setActiveWorkspaceId('');
      return;
    }

    const state = loadWorkspaceState();
    setWorkspaceTabs(
      (state.layouts ?? []).map((layout) => ({
        id: layout.id,
        name: layout.name,
        isDefault: layout.isDefault,
        updatedAt: layout.updatedAt,
      }))
    );
    setActiveWorkspaceId(state.activeLayoutId ?? '');

    const handleWorkspaceState = (event: Event) => {
      const detail = (event as CustomEvent).detail as
        | { layouts?: WorkspaceTab[]; activeLayoutId?: string }
        | undefined;
      if (!detail) return;
      if (detail.layouts) {
        setWorkspaceTabs(detail.layouts);
      }
      if (detail.activeLayoutId !== undefined) {
        setActiveWorkspaceId(detail.activeLayoutId ?? '');
      }
    };

    window.addEventListener('aonik:workspace:state', handleWorkspaceState);
    return () => window.removeEventListener('aonik:workspace:state', handleWorkspaceState);
  }, [isWorkspace]);

  useEffect(() => {
    if (!editingWorkspaceId) return;
    editingInputRef.current?.focus();
    editingInputRef.current?.select();
  }, [editingWorkspaceId]);

  useEffect(() => {
    if (!isCreateWorkspaceOpen) return;
    setNewWorkspaceName('');
  }, [isCreateWorkspaceOpen]);

  // Track fullscreen state changes
  useEffect(() => {
    const handleFullscreenChange = () => {
      const fullscreen = !!document.fullscreenElement;
      setIsFullscreen(fullscreen);
      onFullscreenChange?.(fullscreen);
    };

    document.addEventListener('fullscreenchange', handleFullscreenChange);
    return () => document.removeEventListener('fullscreenchange', handleFullscreenChange);
  }, [onFullscreenChange]);

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

  const dispatchWorkspaceLoad = (layoutId: string) => {
    window.dispatchEvent(new CustomEvent('aonik:workspace:load', { detail: { layoutId } }));
  };

  const dispatchWorkspaceCreate = (name: string) => {
    window.dispatchEvent(new CustomEvent('aonik:workspace:create', { detail: { name } }));
  };

  const dispatchWorkspaceRename = (layoutId: string, name: string) => {
    window.dispatchEvent(new CustomEvent('aonik:workspace:rename', { detail: { layoutId, name } }));
  };

  const dispatchWorkspaceRemove = (layoutId: string) => {
    window.dispatchEvent(new CustomEvent('aonik:workspace:remove', { detail: { layoutId } }));
  };

  const handleWorkspaceCreate = () => {
    setIsCreateWorkspaceOpen(true);
  };

  const handleWorkspaceCreateSubmit = () => {
    const trimmed = newWorkspaceName.trim();
    if (!trimmed) return;
    dispatchWorkspaceCreate(trimmed);
    setIsCreateWorkspaceOpen(false);
  };

  const handleWorkspaceRemoveRequest = (workspaceId: string, workspaceName: string) => {
    setConfirmWorkspaceId(workspaceId);
    setConfirmWorkspaceName(workspaceName);
  };

  const handleWorkspaceRemoveConfirm = () => {
    if (!confirmWorkspaceId) return;
    dispatchWorkspaceRemove(confirmWorkspaceId);
    setConfirmWorkspaceId(null);
    setConfirmWorkspaceName('');
  };

  const handleWorkspaceRenameCommit = (layoutId: string, name: string) => {
    const trimmed = name.trim();
    setEditingWorkspaceId(null);
    if (!trimmed) return;
    setWorkspaceTabs((current) =>
      current.map((item) => (item.id === layoutId ? { ...item, name: trimmed } : item))
    );
    dispatchWorkspaceRename(layoutId, trimmed);
  };

  return (
    <>
    <header className="sticky top-0 z-10 shrink-0 flex items-center justify-between h-14 px-6 bg-[var(--color-surface)] border-b border-[var(--color-border-light)]">
      {/* Breadcrumb / Left Slot */}
      <nav className="flex items-center gap-2 text-sm min-w-0">
        {isWorkspace ? (
          <div className="flex items-center gap-2 min-w-0">
            <div className="flex items-center gap-2 min-w-0 overflow-x-auto">
              {workspaceTabs.map((workspace) => {
                const isActive = workspace.id === activeWorkspaceId;
                const isEditing = workspace.id === editingWorkspaceId;
                return (
                  <button
                    key={workspace.id}
                    type="button"
                    className={`flex items-center gap-2 px-3 py-1.5 rounded-md border text-sm transition-colors whitespace-nowrap ${
                      isActive
                        ? 'bg-[var(--color-brand-primary)] text-white border-[var(--color-brand-primary)]'
                        : 'bg-[var(--color-surface-elevated)] text-[var(--color-text-secondary)] border-[var(--color-border-light)] hover:text-[var(--color-text-primary)]'
                    }`}
                    onClick={() => {
                      if (isEditing) return;
                      dispatchWorkspaceLoad(workspace.id);
                    }}
                    onDoubleClick={() => {
                      setEditingWorkspaceId(workspace.id);
                      setEditingName(workspace.name);
                    }}
                  >
                    {isEditing ? (
                      <input
                        ref={editingInputRef}
                        value={editingName}
                        onChange={(event) => setEditingName(event.target.value)}
                        onBlur={() => handleWorkspaceRenameCommit(workspace.id, editingName)}
                        onKeyDown={(event) => {
                          if (event.key === 'Enter') {
                            handleWorkspaceRenameCommit(workspace.id, editingName);
                          }
                          if (event.key === 'Escape') {
                            setEditingWorkspaceId(null);
                          }
                        }}
                        className="bg-transparent border-none outline-none text-inherit text-sm w-36"
                      />
                    ) : (
                      <>
                        <span className="truncate max-w-[12rem]">{workspace.name}</span>
                        {!workspace.isDefault && (
                          <button
                            type="button"
                            className={`ml-1 rounded-full p-0.5 transition-colors ${
                              isActive
                                ? 'text-white/70 hover:text-white'
                                : 'text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)]'
                            }`}
                            aria-label={`Close ${workspace.name}`}
                            title="Close workspace"
                            onClick={(event) => {
                              event.stopPropagation();
                              handleWorkspaceRemoveRequest(workspace.id, workspace.name);
                            }}
                          >
                            <X className="w-3 h-3" />
                          </button>
                        )}
                      </>
                    )}
                  </button>
                );
              })}
            </div>
            <Button
              variant="ghost"
              size="icon-sm"
              className="text-[var(--color-text-secondary)]"
              onClick={handleWorkspaceCreate}
              aria-label="Create workspace"
              title="Create workspace"
            >
              <Plus className="w-4 h-4" />
            </Button>
            <Dialog open={isCreateWorkspaceOpen} onOpenChange={setIsCreateWorkspaceOpen}>
              <DialogContent className="sm:max-w-[420px]">
                <DialogHeader>
                  <DialogTitle>Create workspace</DialogTitle>
                  <DialogDescription>Save the current layout as a named workspace.</DialogDescription>
                </DialogHeader>
                <div className="grid gap-2">
                  <label htmlFor="workspaceName" className="text-sm font-medium text-[var(--color-text-primary)]">
                    Workspace name
                  </label>
                  <Input
                    id="workspaceName"
                    value={newWorkspaceName}
                    onChange={(event) => setNewWorkspaceName(event.target.value)}
                    placeholder="New Workspace"
                    autoFocus
                    onKeyDown={(event) => {
                      if (event.key === 'Enter') {
                        handleWorkspaceCreateSubmit();
                      }
                    }}
                  />
                </div>
                <DialogFooter>
                  <Button variant="ghost" onClick={() => setIsCreateWorkspaceOpen(false)}>
                    Cancel
                  </Button>
                  <Button onClick={handleWorkspaceCreateSubmit}>Create workspace</Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>
            <Dialog
              open={Boolean(confirmWorkspaceId)}
              onOpenChange={(open) => {
                if (open) return;
                setConfirmWorkspaceId(null);
                setConfirmWorkspaceName('');
              }}
            >
              <DialogContent className="sm:max-w-[420px]">
                <DialogHeader>
                  <DialogTitle>Close workspace</DialogTitle>
                  <DialogDescription>
                    This will remove the saved layout for "{confirmWorkspaceName}".
                  </DialogDescription>
                </DialogHeader>
                <DialogFooter>
                  <Button
                    variant="ghost"
                    onClick={() => {
                      setConfirmWorkspaceId(null);
                      setConfirmWorkspaceName('');
                    }}
                  >
                    Cancel
                  </Button>
                  <Button onClick={handleWorkspaceRemoveConfirm}>Close workspace</Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>
          </div>
        ) : leftSlot ? (
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

        {onWorkspaceReset && (
          <Button
            variant="ghost"
            size="sm"
            className="ml-3 text-[var(--color-text-secondary)]"
            onClick={onWorkspaceReset}
          >
            Reset layout
          </Button>
        )}
      </nav>

      {/* Actions */}
      <div className="flex items-center gap-2">
        {tenantLabel && (
          <span className="flex items-center gap-2 px-2 py-1 rounded-md bg-[var(--color-surface-elevated)] border border-[var(--color-border-light)]">
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
        <Button
          variant="ghost"
          size="icon-sm"
          className="text-[var(--color-text-secondary)]"
          aria-label="Open AI chat"
          title="Open AI chat"
          onClick={onAiChatToggle}
        >
          <MessageSquareText className="w-4 h-4" />
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
