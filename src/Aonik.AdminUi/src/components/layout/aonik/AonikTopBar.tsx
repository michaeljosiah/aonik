// AonikTopBar: the app shell's header (Spec 098 §7.5), shadcn style.
//
//   - left: sidebar toggle, then workspace tabs (on /workspace), a leftSlot
//     (the agent selector on /ai/chat), or the breadcrumb
//   - right: search (opens the Ctrl/Cmd+K command palette), Ask Aonik
//     (Ctrl/Cmd+/), guides, fullscreen, notifications, settings

import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  BellIcon,
  CircleHelpIcon,
  Maximize2Icon,
  Minimize2Icon,
  PanelLeftIcon,
  PlusIcon,
  SearchIcon,
  SettingsIcon,
  SparklesIcon,
  XIcon,
} from 'lucide-react';

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Field, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Kbd, KbdGroup } from '@/components/ui/kbd';
import { Separator } from '@/components/ui/separator';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { NotificationsPanel } from '@/components/layout/NotificationsPanel';
import { useNotifications } from '@/hooks/useNotifications';
import { isElectron } from '@/lib/electron';
import { loadWorkspaceState } from '@/workspace/storage';

import { CommandPalette } from './CommandPalette';

const MOD_KEY =
  typeof navigator !== 'undefined' && /Mac|iPhone|iPad/.test(navigator.platform) ? '⌘' : 'Ctrl';

function IconAction({ label, children }: { label: string; children: React.ReactElement }) {
  return (
    <Tooltip>
      <TooltipTrigger asChild>{children}</TooltipTrigger>
      <TooltipContent side="bottom">{label}</TooltipContent>
    </Tooltip>
  );
}

interface WorkspaceTab {
  id: string;
  name: string;
  isDefault: boolean;
  updatedAt: string;
}

/**
 * A breadcrumb item is either a plain label (renders as static text —
 * typically the current page) or a { label, href } pair (renders as a
 * Link). Mixing the two lets parent items navigate while the trailing
 * item stays inert.
 */
export type TopBarBreadcrumbItem = string | { label: string; href: string };

interface AonikTopBarProps {
  breadcrumb?: TopBarBreadcrumbItem[];
  onToggleSidebar?: () => void;
  leftSlot?: React.ReactNode;
  isWorkspace?: boolean;
  onWorkspaceReset?: () => void;
  onFullscreenChange?: (isFullscreen: boolean) => void;
  onAskAonik?: () => void;
}

export function AonikTopBar({
  breadcrumb = ['My Space'],
  onToggleSidebar,
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
  const [paletteOpen, setPaletteOpen] = useState(false);

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

  // Ctrl/Cmd+K opens the command palette.
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key.toLowerCase() === 'k' && (e.metaKey || e.ctrlKey) && !e.shiftKey && !e.altKey) {
        e.preventDefault();
        setPaletteOpen((v) => !v);
      }
    };
    document.addEventListener('keydown', handler);
    return () => document.removeEventListener('keydown', handler);
  }, []);

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
        className="sticky top-0 z-10 flex h-14 shrink-0 items-center gap-2 border-b bg-background px-3"
        style={electronDragStyle}
      >
        {onToggleSidebar && (
          <>
            <IconAction label={`Toggle sidebar (${MOD_KEY}+B)`}>
              <Button
                variant="ghost"
                size="icon-sm"
                onClick={onToggleSidebar}
                aria-label="Toggle sidebar"
                style={electronNoDragStyle}
              >
                <PanelLeftIcon />
              </Button>
            </IconAction>
            <Separator orientation="vertical" className="mr-1 data-[orientation=vertical]:h-4" />
          </>
        )}

        <div className="flex min-w-0 items-center gap-2" style={electronNoDragStyle}>
          {isWorkspace ? (
            <div className="flex min-w-0 items-center gap-1.5">
              <div
                role="tablist"
                aria-label="Workspaces"
                className="inline-flex h-9 min-w-0 items-center gap-0.5 overflow-x-auto rounded-lg bg-muted p-[3px]"
              >
                {workspaceTabs.map((ws) => {
                  const isActive = ws.id === activeWorkspaceId;
                  const isEditing = ws.id === editingWorkspaceId;
                  return (
                    <div
                      key={ws.id}
                      data-state={isActive ? 'active' : 'inactive'}
                      className="flex h-full items-center rounded-md text-sm text-muted-foreground transition-colors hover:text-foreground data-[state=active]:bg-background data-[state=active]:text-foreground data-[state=active]:shadow-sm"
                    >
                      {isEditing ? (
                        <input
                          ref={editingInputRef}
                          aria-label="Workspace name"
                          value={editingName}
                          onChange={(e) => setEditingName(e.target.value)}
                          onBlur={() => handleRenameCommit(ws.id, editingName)}
                          onKeyDown={(e) => {
                            if (e.key === 'Enter') handleRenameCommit(ws.id, editingName);
                            if (e.key === 'Escape') setEditingWorkspaceId(null);
                          }}
                          className="h-full w-36 rounded-md bg-transparent px-2.5 text-sm outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
                        />
                      ) : (
                        <button
                          type="button"
                          role="tab"
                          aria-selected={isActive}
                          title="Double-click to rename"
                          className="h-full max-w-[12rem] truncate rounded-md px-2.5 outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
                          onClick={() => dispatchWorkspaceLoad(ws.id)}
                          onDoubleClick={() => {
                            setEditingWorkspaceId(ws.id);
                            setEditingName(ws.name);
                          }}
                        >
                          {ws.name}
                        </button>
                      )}
                      {!ws.isDefault && !isEditing && (
                        <button
                          type="button"
                          aria-label={`Close ${ws.name}`}
                          className="mr-1 rounded-sm p-0.5 text-muted-foreground outline-none hover:text-foreground focus-visible:ring-[3px] focus-visible:ring-ring/50"
                          onClick={() => handleRemoveRequest(ws.id, ws.name)}
                        >
                          <XIcon className="size-3" />
                        </button>
                      )}
                    </div>
                  );
                })}
              </div>
              <IconAction label="Create workspace">
                <Button variant="ghost" size="icon-sm" onClick={() => setIsCreateOpen(true)} aria-label="Create workspace">
                  <PlusIcon />
                </Button>
              </IconAction>
              {onWorkspaceReset && (
                <Button variant="ghost" size="sm" onClick={onWorkspaceReset}>
                  Reset layout
                </Button>
              )}
            </div>
          ) : leftSlot ? (
            leftSlot
          ) : (
            <Breadcrumb>
              <BreadcrumbList>
                {breadcrumb.map((item, idx) => {
                  const isLast = idx === breadcrumb.length - 1;
                  const label = typeof item === 'string' ? item : item.label;
                  const href = typeof item === 'string' ? null : item.href;
                  return (
                    <span key={`${label}-${idx}`} className="contents">
                      {idx > 0 && <BreadcrumbSeparator />}
                      <BreadcrumbItem>
                        {isLast ? (
                          <BreadcrumbPage>{label}</BreadcrumbPage>
                        ) : href ? (
                          <BreadcrumbLink asChild>
                            <Link to={href}>{label}</Link>
                          </BreadcrumbLink>
                        ) : (
                          <span>{label}</span>
                        )}
                      </BreadcrumbItem>
                    </span>
                  );
                })}
              </BreadcrumbList>
            </Breadcrumb>
          )}
        </div>

        <div className="ml-auto flex items-center gap-1" style={electronNoDragStyle}>
          <Button
            variant="outline"
            size="sm"
            onClick={() => setPaletteOpen(true)}
            className="hidden w-56 justify-start font-normal text-muted-foreground md:inline-flex"
          >
            <SearchIcon />
            Search
            <KbdGroup className="ml-auto">
              <Kbd>{MOD_KEY}</Kbd>
              <Kbd>K</Kbd>
            </KbdGroup>
          </Button>
          <IconAction label="Search">
            <Button variant="ghost" size="icon-sm" className="md:hidden" onClick={() => setPaletteOpen(true)} aria-label="Search">
              <SearchIcon />
            </Button>
          </IconAction>
          {onAskAonik && (
            <Button variant="outline" size="sm" onClick={onAskAonik} title={`Ask Aonik (${MOD_KEY}+/)`}>
              <SparklesIcon className="text-primary" />
              Ask Aonik
              <Kbd className="hidden lg:inline-flex">{MOD_KEY}/</Kbd>
            </Button>
          )}
          <Separator orientation="vertical" className="mx-1 data-[orientation=vertical]:h-4" />
          <IconAction label="Guides">
            <Button variant="ghost" size="icon-sm" asChild>
              <Link to="/setup-guides" aria-label="Guides">
                <CircleHelpIcon />
              </Link>
            </Button>
          </IconAction>
          <IconAction label={isFullscreen ? 'Exit full screen' : 'Full screen'}>
            <Button variant="ghost" size="icon-sm" onClick={toggleFullscreen} aria-label={isFullscreen ? 'Exit full screen' : 'Full screen'}>
              {isFullscreen ? <Minimize2Icon /> : <Maximize2Icon />}
            </Button>
          </IconAction>
          <IconAction label="Notifications">
            <Button
              variant="ghost"
              size="icon-sm"
              className="relative"
              onClick={() => setShowNotifications(true)}
              aria-label={unreadCount > 0 ? `Notifications, ${unreadCount} unread` : 'Notifications'}
            >
              <BellIcon />
              {unreadCount > 0 && (
                <span className="absolute -top-0.5 -right-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-destructive px-1 font-mono text-[10px] font-semibold tabular-nums text-destructive-foreground ring-2 ring-background">
                  {unreadCount > 99 ? '99+' : unreadCount}
                </span>
              )}
            </Button>
          </IconAction>
          <IconAction label="Settings">
            <Button variant="ghost" size="icon-sm" asChild>
              <Link to="/settings" aria-label="Settings">
                <SettingsIcon />
              </Link>
            </Button>
          </IconAction>
        </div>
      </header>

      <CommandPalette
        open={paletteOpen}
        onOpenChange={setPaletteOpen}
        onAskAonik={onAskAonik}
        shortcutLabel={MOD_KEY === '⌘' ? '⌘' : 'Ctrl+'}
      />

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
          <Field>
            <FieldLabel htmlFor="workspaceName">Workspace name</FieldLabel>
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
          </Field>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsCreateOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleCreateSubmit}>Create workspace</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <AlertDialog
        open={Boolean(confirmId)}
        onOpenChange={(open) => {
          if (open) return;
          setConfirmId(null);
          setConfirmName('');
        }}
      >
        <AlertDialogContent className="max-w-[420px]">
          <AlertDialogHeader>
            <AlertDialogTitle>Close workspace?</AlertDialogTitle>
            <AlertDialogDescription>
              The saved layout for &quot;{confirmName}&quot; will be removed.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={handleRemoveConfirm}>
              Close workspace
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
