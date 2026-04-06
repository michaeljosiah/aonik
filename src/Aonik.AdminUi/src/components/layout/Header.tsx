import { Home, Bell, MessageSquareText, Maximize2, Minimize2, Plus, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { useState, useRef, useEffect } from 'react';
import { NotificationsPanel } from '@/components/layout/NotificationsPanel';
import { useNotifications } from '@/hooks/useNotifications';
import { isElectron } from '@/lib/electron';

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
  const [showNotifications, setShowNotifications] = useState(false);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [workspaceTabs, setWorkspaceTabs] = useState<WorkspaceTab[]>([]);
  const [activeWorkspaceId, setActiveWorkspaceId] = useState('');
  const [editingWorkspaceId, setEditingWorkspaceId] = useState<string | null>(null);
  const [editingName, setEditingName] = useState('');
  const editingInputRef = useRef<HTMLInputElement>(null);
  const [isCreateWorkspaceOpen, setIsCreateWorkspaceOpen] = useState(false);
  const [newWorkspaceName, setNewWorkspaceName] = useState('');
  const [confirmWorkspaceId, setConfirmWorkspaceId] = useState<string | null>(null);
  const [confirmWorkspaceName, setConfirmWorkspaceName] = useState('');
  const { notifications, unreadCount, loading: notificationsLoading, markRead, dismiss, markAllRead } = useNotifications();

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
    <header className={`sticky top-0 z-10 shrink-0 flex items-center justify-between h-[50px] px-6 bg-[var(--color-navbar-bg)] border-b border-[var(--color-border)]${isElectron ? ' [app-region:drag]' : ''}`} style={isElectron ? { WebkitAppRegion: 'drag' } as React.CSSProperties : undefined}>
      {/* Breadcrumb / Left Slot */}
      <nav className="flex items-center gap-2 text-sm min-w-0" style={isElectron ? { WebkitAppRegion: 'no-drag' } as React.CSSProperties : undefined}>
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
              <DialogContent className="max-w-[420px]">
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
              <DialogContent className="max-w-[420px]">
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
      <div className="flex items-center gap-2" style={isElectron ? { WebkitAppRegion: 'no-drag' } as React.CSSProperties : undefined}>
        <Button
          variant="ghost"
          size="icon-sm"
          className="relative text-[var(--color-text-secondary)]"
          onClick={() => setShowNotifications(true)}
          aria-label="Open notifications"
        >
          <Bell className="w-4 h-4" />
          {unreadCount > 0 && (
            <span className="absolute -top-1 -right-1 min-w-[1.1rem] h-[1.1rem] px-1 rounded-full bg-[var(--color-error)] text-white text-[10px] leading-[1.1rem] text-center font-semibold">
              {unreadCount > 99 ? '99+' : unreadCount}
            </span>
          )}
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
    <NotificationsPanel
      open={showNotifications}
      onClose={() => setShowNotifications(false)}
      notifications={notifications}
      unreadCount={unreadCount}
      loading={notificationsLoading}
      onMarkRead={markRead}
      onDismiss={dismiss}
      onMarkAllRead={markAllRead}
    />
    </>
  );
}
