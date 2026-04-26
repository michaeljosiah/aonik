// AonikTopBar — production topbar shell, 1:1 visual port of
// templates/aonik-admin-starterkit/kit/shell-aonik.jsx (AonikTopBar).
//
// Preserves the live admin behaviours that the template doesn't model:
//   - workspace tabs (visible only when on /workspace) with create/rename/close
//   - notifications panel with unread badge
//   - fullscreen toggle (with state callback)
//   - leftSlot for the AI agent selector on /ai/chat
//   - Ask Aonik button + ⌘/ shortcut → opens the AI chat panel

import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  ChevronRight, HelpCircle, Maximize2, Minimize2, Bell, Settings,
  Sparkles, Plus, X, Home,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { NotificationsPanel } from '@/components/layout/NotificationsPanel';
import { useNotifications } from '@/hooks/useNotifications';
import { isElectron } from '@/lib/electron';
import { loadWorkspaceState } from '@/workspace/storage';
import { cn } from '@/lib/utils';

interface WorkspaceTab {
  id: string;
  name: string;
  isDefault: boolean;
  updatedAt: string;
}

interface AonikTopBarProps {
  breadcrumb?: string[];
  leftSlot?: React.ReactNode;
  isWorkspace?: boolean;
  onWorkspaceReset?: () => void;
  onFullscreenChange?: (isFullscreen: boolean) => void;
  onAskAonik?: () => void;
}

export function AonikTopBar({
  breadcrumb = ['My Space'],
  leftSlot,
  isWorkspace,
  onWorkspaceReset,
  onFullscreenChange,
  onAskAonik,
}: AonikTopBarProps) {
  const [showNotifications, setShowNotifications] = useState(false);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [workspaceTabs, setWorkspaceTabs] = useState<WorkspaceTab[]>([]);
  const [activeWorkspaceId, setActiveWorkspaceId] = useState('');
  const [editingWorkspaceId, setEditingWorkspaceId] = useState<string | null>(null);
  const [editingName, setEditingName] = useState('');
  const editingInputRef = useRef<HTMLInputElement>(null);
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [newWorkspaceName, setNewWorkspaceName] = useState('');
  const [confirmId, setConfirmId] = useState<string | null>(null);
  const [confirmName, setConfirmName] = useState('');

  const { notifications, unreadCount, loading, markRead, dismiss, markAllRead } = useNotifications();

  // Hydrate workspace tabs and listen for state-change events from WorkspacePage
  useEffect(() => {
    if (!isWorkspace) {
      setWorkspaceTabs([]);
      setActiveWorkspaceId('');
      return;
    }

    const state = loadWorkspaceState();
    setWorkspaceTabs(
      (state.layouts ?? []).map((l) => ({
        id: l.id,
        name: l.name,
        isDefault: l.isDefault,
        updatedAt: l.updatedAt,
      })),
    );
    setActiveWorkspaceId(state.activeLayoutId ?? '');

    const handler = (event: Event) => {
      const detail = (event as CustomEvent).detail as
        | { layouts?: WorkspaceTab[]; activeLayoutId?: string }
        | undefined;
      if (!detail) return;
      if (detail.layouts) setWorkspaceTabs(detail.layouts);
      if (detail.activeLayoutId !== undefined) setActiveWorkspaceId(detail.activeLayoutId ?? '');
    };
    window.addEventListener('aonik:workspace:state', handler);
    return () => window.removeEventListener('aonik:workspace:state', handler);
  }, [isWorkspace]);

  useEffect(() => {
    if (!editingWorkspaceId) return;
    editingInputRef.current?.focus();
    editingInputRef.current?.select();
  }, [editingWorkspaceId]);

  useEffect(() => {
    if (!isCreateOpen) return;
    setNewWorkspaceName('');
  }, [isCreateOpen]);

  // Track fullscreen state
  useEffect(() => {
    const handler = () => {
      const fullscreen = !!document.fullscreenElement;
      setIsFullscreen(fullscreen);
      onFullscreenChange?.(fullscreen);
    };
    document.addEventListener('fullscreenchange', handler);
    return () => document.removeEventListener('fullscreenchange', handler);
  }, [onFullscreenChange]);

  // ⌘/ shortcut for Ask Aonik (Ctrl+/ on non-mac)
  useEffect(() => {
    if (!onAskAonik) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === '/' && (e.metaKey || e.ctrlKey) && !e.shiftKey && !e.altKey) {
        e.preventDefault();
        onAskAonik();
      }
    };
    document.addEventListener('keydown', handler);
    return () => document.removeEventListener('keydown', handler);
  }, [onAskAonik]);

  const toggleFullscreen = async () => {
    try {
      if (!document.fullscreenElement) await document.documentElement.requestFullscreen();
      else await document.exitFullscreen();
    } catch (err) {
      console.error('Fullscreen toggle failed:', err);
    }
  };

  const dispatchWorkspaceLoad = (layoutId: string) =>
    window.dispatchEvent(new CustomEvent('aonik:workspace:load', { detail: { layoutId } }));
  const dispatchWorkspaceCreate = (name: string) =>
    window.dispatchEvent(new CustomEvent('aonik:workspace:create', { detail: { name } }));
  const dispatchWorkspaceRename = (layoutId: string, name: string) =>
    window.dispatchEvent(new CustomEvent('aonik:workspace:rename', { detail: { layoutId, name } }));
  const dispatchWorkspaceRemove = (layoutId: string) =>
    window.dispatchEvent(new CustomEvent('aonik:workspace:remove', { detail: { layoutId } }));

  const handleCreateSubmit = () => {
    const trimmed = newWorkspaceName.trim();
    if (!trimmed) return;
    dispatchWorkspaceCreate(trimmed);
    setIsCreateOpen(false);
  };
  const handleRemoveRequest = (id: string, name: string) => {
    setConfirmId(id);
    setConfirmName(name);
  };
  const handleRemoveConfirm = () => {
    if (!confirmId) return;
    dispatchWorkspaceRemove(confirmId);
    setConfirmId(null);
    setConfirmName('');
  };
  const handleRenameCommit = (layoutId: string, name: string) => {
    const trimmed = name.trim();
    setEditingWorkspaceId(null);
    if (!trimmed) return;
    setWorkspaceTabs((current) =>
      current.map((t) => (t.id === layoutId ? { ...t, name: trimmed } : t)),
    );
    dispatchWorkspaceRename(layoutId, trimmed);
  };

  const electronDragStyle = isElectron
    ? ({ WebkitAppRegion: 'drag' } as React.CSSProperties)
    : undefined;
  const electronNoDragStyle = isElectron
    ? ({ WebkitAppRegion: 'no-drag' } as React.CSSProperties)
    : undefined;

  return (
    <>
      <header
        className="sticky top-0 z-10 flex h-[56px] shrink-0 items-center justify-between gap-4 border-b border-[var(--color-border-light)] bg-[var(--color-surface)] px-5"
        style={electronDragStyle}
      >
        {/* Left: workspace tabs OR leftSlot OR breadcrumbs */}
        <nav
          className="flex min-w-0 items-center gap-2.5 text-sm"
          style={electronNoDragStyle}
        >
          {isWorkspace ? (
            <div className="flex min-w-0 items-center gap-2">
              <div className="flex min-w-0 items-center gap-2 overflow-x-auto">
                {workspaceTabs.map((ws) => {
                  const isActive = ws.id === activeWorkspaceId;
                  const isEditing = ws.id === editingWorkspaceId;
                  return (
                    <button
                      key={ws.id}
                      type="button"
                      className={cn(
                        'flex items-center gap-2 whitespace-nowrap rounded-md border px-3 py-1.5 text-sm transition-colors',
                        isActive
                          ? 'border-[var(--color-brand-primary)] bg-[var(--color-brand-primary)] text-white'
                          : 'border-[var(--color-border-light)] bg-[var(--color-surface-elevated)] text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]',
                      )}
                      onClick={() => {
                        if (isEditing) return;
                        dispatchWorkspaceLoad(ws.id);
                      }}
                      onDoubleClick={() => {
                        setEditingWorkspaceId(ws.id);
                        setEditingName(ws.name);
                      }}
                    >
                      {isEditing ? (
                        <input
                          ref={editingInputRef}
                          value={editingName}
                          onChange={(e) => setEditingName(e.target.value)}
                          onBlur={() => handleRenameCommit(ws.id, editingName)}
                          onKeyDown={(e) => {
                            if (e.key === 'Enter') handleRenameCommit(ws.id, editingName);
                            if (e.key === 'Escape') setEditingWorkspaceId(null);
                          }}
                          className="w-36 border-none bg-transparent text-sm outline-none"
                        />
                      ) : (
                        <>
                          <span className="max-w-[12rem] truncate">{ws.name}</span>
                          {!ws.isDefault && (
                            <button
                              type="button"
                              className={cn(
                                'ml-1 rounded-full p-0.5 transition-colors',
                                isActive
                                  ? 'text-white/70 hover:text-white'
                                  : 'text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)]',
                              )}
                              aria-label={`Close ${ws.name}`}
                              title="Close workspace"
                              onClick={(e) => {
                                e.stopPropagation();
                                handleRemoveRequest(ws.id, ws.name);
                              }}
                            >
                              <X className="h-3 w-3" />
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
                onClick={() => setIsCreateOpen(true)}
                aria-label="Create workspace"
                title="Create workspace"
              >
                <Plus className="h-4 w-4" />
              </Button>
              {onWorkspaceReset && (
                <Button
                  variant="ghost"
                  size="sm"
                  className="ml-1 text-[var(--color-text-secondary)]"
                  onClick={onWorkspaceReset}
                >
                  Reset layout
                </Button>
              )}
            </div>
          ) : leftSlot ? (
            leftSlot
          ) : (
            <>
              <Home className="h-3.5 w-3.5 text-[var(--color-text-tertiary)]" />
              {breadcrumb.map((item, idx) => {
                const isLast = idx === breadcrumb.length - 1;
                return (
                  <span key={`${item}-${idx}`} className="flex items-center gap-2.5">
                    {idx > 0 && (
                      <ChevronRight className="h-3 w-3 text-[var(--color-text-tertiary)]" />
                    )}
                    <span
                      className={cn(
                        'text-[13px]',
                        isLast
                          ? 'font-semibold text-[var(--color-text-primary)]'
                          : 'text-[var(--color-text-secondary)]',
                      )}
                    >
                      {item}
                    </span>
                  </span>
                );
              })}
            </>
          )}
        </nav>

        {/* Right: actions */}
        <div className="flex items-center gap-1" style={electronNoDragStyle}>
          {onAskAonik && (
            <button
              type="button"
              onClick={onAskAonik}
              className="inline-flex h-[30px] items-center gap-1.5 rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-2.5 text-[13px] font-medium text-[var(--color-text-primary)] transition-colors hover:bg-[var(--color-surface-inset)]"
              title="Ask Aonik (⌘/)"
            >
              <Sparkles className="h-3 w-3 text-[var(--color-brand-primary)]" />
              Ask Aonik
              <span className="ml-1 rounded border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-1.5 py-px font-mono text-[10px] text-[var(--color-text-tertiary)]">
                ⌘/
              </span>
            </button>
          )}
          <span className="mx-1.5 h-5 w-px bg-[var(--color-border-light)]" aria-hidden />
          <Link
            to="/setup-guides"
            className="hover-halo"
            title="Guides"
            aria-label="Open setup guides"
          >
            <HelpCircle className="h-4 w-4" />
          </Link>
          <button
            type="button"
            onClick={toggleFullscreen}
            className="hover-halo"
            title={isFullscreen ? 'Exit fullscreen' : 'Enter fullscreen'}
            aria-label="Toggle fullscreen"
          >
            {isFullscreen ? <Minimize2 className="h-4 w-4" /> : <Maximize2 className="h-4 w-4" />}
          </button>
          <button
            type="button"
            className="hover-halo relative"
            onClick={() => setShowNotifications(true)}
            title="Notifications"
            aria-label="Open notifications"
          >
            <Bell className="h-4 w-4" />
            {unreadCount > 0 && (
              <span className="absolute -right-0.5 -top-0.5 inline-flex h-3.5 min-w-[14px] items-center justify-center rounded-full border-[1.5px] border-[var(--color-surface)] bg-[var(--color-error)] px-1 font-mono text-[9px] font-bold text-white">
                {unreadCount > 99 ? '99+' : unreadCount}
              </span>
            )}
          </button>
          <Link
            to="/settings"
            className="hover-halo"
            title="Settings"
            aria-label="Open settings"
          >
            <Settings className="h-4 w-4" />
          </Link>
        </div>
      </header>

      <NotificationsPanel
        open={showNotifications}
        onClose={() => setShowNotifications(false)}
        notifications={notifications}
        unreadCount={unreadCount}
        loading={loading}
        onMarkRead={markRead}
        onDismiss={dismiss}
        onMarkAllRead={markAllRead}
      />

      <Dialog open={isCreateOpen} onOpenChange={setIsCreateOpen}>
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
              onChange={(e) => setNewWorkspaceName(e.target.value)}
              placeholder="New Workspace"
              autoFocus
              onKeyDown={(e) => {
                if (e.key === 'Enter') handleCreateSubmit();
              }}
            />
          </div>
          <DialogFooter>
            <Button variant="ghost" onClick={() => setIsCreateOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleCreateSubmit}>Create workspace</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(confirmId)}
        onOpenChange={(open) => {
          if (open) return;
          setConfirmId(null);
          setConfirmName('');
        }}
      >
        <DialogContent className="max-w-[420px]">
          <DialogHeader>
            <DialogTitle>Close workspace</DialogTitle>
            <DialogDescription>
              This will remove the saved layout for &quot;{confirmName}&quot;.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button
              variant="ghost"
              onClick={() => {
                setConfirmId(null);
                setConfirmName('');
              }}
            >
              Cancel
            </Button>
            <Button onClick={handleRemoveConfirm}>Close workspace</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
