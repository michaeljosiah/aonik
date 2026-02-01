import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { WorkspaceContext } from './context';
import type { DockviewApi, IDockviewPanel } from 'dockview';
import { getWorkspacePanelConfig, defaultWorkspaceLayoutPanels } from './registry';
import { loadWorkspaceState, saveWorkspaceState } from './storage';
import type { WorkspaceAction, WorkspaceLayoutRecord, WorkspaceLayoutSnapshot } from './types';
import { WorkspaceEventBus } from './workspaceEventBus';

const defaultLayoutId = 'layout-default';

function generateLayoutId() {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }
  return `layout-${Date.now()}`;
}

export function WorkspaceProvider({ children }: { children: ReactNode }) {
  const [api, setApi] = useState<DockviewApi | null>(null);
  const [layouts, setLayouts] = useState<WorkspaceLayoutRecord[]>([]);
  const [activeLayoutId, setActiveLayoutId] = useState('');
  const [storageLoaded, setStorageLoaded] = useState(false);
  const eventBus = useMemo(() => new WorkspaceEventBus(), []);
  const isRestoringRef = useRef(false);

  const persistLayouts = useCallback(
    (nextLayouts: WorkspaceLayoutRecord[], nextActiveLayoutId?: string) => {
      saveWorkspaceState({
        activeLayoutId: nextActiveLayoutId,
        layouts: nextLayouts,
      });
    },
    []
  );

  const addPanelToDock = useCallback(
    (
      panelId: string,
      options?: { referencePanel?: IDockviewPanel; direction?: 'within' | 'below' | 'above' | 'right' | 'left' }
    ) => {
      if (!api) return undefined;
      const config = getWorkspacePanelConfig(panelId);
      if (!config) return undefined;

      return api.addPanel({
        id: config.id,
        component: 'workspace-panel',
        title: config.title,
        params: { panelId: config.id },
        position: options?.referencePanel
          ? { referencePanel: options.referencePanel, direction: options.direction }
          : undefined,
        initialWidth: config.defaultWidth,
        initialHeight: config.defaultHeight,
      });
    },
    [api]
  );

  const openPanel = useCallback(
    (panelId: string) => {
      if (!api) return;
      const existing = api.getPanel(panelId);
      if (existing) {
        existing.api?.setActive();
        return;
      }
      if (api.activePanel) {
        addPanelToDock(panelId, { referencePanel: api.activePanel, direction: 'within' });
        return;
      }
      addPanelToDock(panelId);
    },
    [api, addPanelToDock]
  );

  const closePanel = useCallback(
    (panelId: string) => {
      if (!api) return;
      const panel = api.getPanel(panelId);
      if (panel) {
        api.removePanel(panel);
      }
    },
    [api]
  );

  const maximizeActivePanel = useCallback(() => {
    if (!api?.activePanel) return;
    if (api.hasMaximizedGroup()) {
      api.exitMaximizedGroup();
    } else {
      api.maximizeGroup(api.activePanel);
    }
  }, [api]);

  const exitMaximizedGroup = useCallback(() => {
    if (!api?.hasMaximizedGroup()) return;
    api.exitMaximizedGroup();
  }, [api]);

  const saveActiveLayout = useCallback(() => {
    if (!api || !activeLayoutId) return;
    const snapshot = api.toJSON() as WorkspaceLayoutSnapshot;
    setLayouts((current) => {
      const updatedAt = new Date().toISOString();
      const next = current.map((layout) =>
        layout.id === activeLayoutId
          ? { ...layout, layout: snapshot, updatedAt }
          : layout
      );
      persistLayouts(next, activeLayoutId);
      return next;
    });
  }, [activeLayoutId, api, persistLayouts]);

  const createLayoutFromActive = useCallback(
    (name: string) => {
      if (!api) return;
      const snapshot = api.toJSON() as WorkspaceLayoutSnapshot;
      const newLayout: WorkspaceLayoutRecord = {
        id: generateLayoutId(),
        name,
        isDefault: false,
        updatedAt: new Date().toISOString(),
        layout: snapshot,
      };
      setLayouts((current) => {
        const next = [...current, newLayout];
        persistLayouts(next, newLayout.id);
        return next;
      });
      setActiveLayoutId(newLayout.id);
    },
    [api, persistLayouts]
  );

  const loadLayout = useCallback(
    (layoutId: string) => {
      if (!api) return;
      const layout = layouts.find((item) => item.id === layoutId);
      if (!layout) return;

      isRestoringRef.current = true;
      api.fromJSON(layout.layout);
      setActiveLayoutId(layoutId);
      persistLayouts(layouts, layoutId);
      setTimeout(() => {
        isRestoringRef.current = false;
      }, 0);
    },
    [api, layouts, persistLayouts]
  );

  const resetToDefaultLayout = useCallback(() => {
    if (!api) return;
    isRestoringRef.current = true;
    api.clear();

    let basePanel: IDockviewPanel | undefined;
    defaultWorkspaceLayoutPanels.forEach((panelId, index) => {
      if (index === 0) {
        basePanel = addPanelToDock(panelId);
      } else if (basePanel) {
        addPanelToDock(panelId, { referencePanel: basePanel, direction: 'within' });
      } else {
        addPanelToDock(panelId);
      }
    });

    const snapshot = api.toJSON() as WorkspaceLayoutSnapshot;
    const defaultLayout: WorkspaceLayoutRecord = {
      id: defaultLayoutId,
      name: 'Workspace',
      isDefault: true,
      updatedAt: new Date().toISOString(),
      layout: snapshot,
    };

    setLayouts([defaultLayout]);
    setActiveLayoutId(defaultLayoutId);
    persistLayouts([defaultLayout], defaultLayoutId);

    setTimeout(() => {
      isRestoringRef.current = false;
    }, 0);
  }, [api, addPanelToDock, persistLayouts]);

  const renameLayout = useCallback(
    (layoutId: string, newName: string) => {
      const trimmed = newName.trim();
      if (!trimmed) return;
      setLayouts((current) => {
        const next = current.map((layout) =>
          layout.id === layoutId
            ? {
                ...layout,
                name: trimmed,
                updatedAt: new Date().toISOString(),
              }
            : layout
        );
        persistLayouts(next, activeLayoutId);
        return next;
      });
    },
    [activeLayoutId, persistLayouts]
  );

  const dispatchAction = useCallback(
    (action: WorkspaceAction) => {
      switch (action.type) {
        case 'open-panel':
          if (action.payload?.panelId) {
            openPanel(String(action.payload.panelId));
          }
          break;
        case 'close-panel':
          if (action.payload?.panelId) {
            closePanel(String(action.payload.panelId));
          }
          break;
        case 'maximize-active':
          maximizeActivePanel();
          break;
        case 'exit-maximized':
          exitMaximizedGroup();
          break;
        case 'save-layout':
          saveActiveLayout();
          break;
        case 'load-layout':
          if (action.payload?.layoutId) {
            loadLayout(String(action.payload.layoutId));
          }
          break;
        case 'reset-layout':
          resetToDefaultLayout();
          break;
        default:
          break;
      }
    },
    [closePanel, exitMaximizedGroup, loadLayout, maximizeActivePanel, openPanel, resetToDefaultLayout, saveActiveLayout]
  );

  const removeLayout = useCallback(
    (layoutId: string) => {
      if (!layoutId) return;
      const target = layouts.find((layout) => layout.id === layoutId);
      if (!target || target.isDefault) return;
      let nextActiveLayout: WorkspaceLayoutRecord | undefined;
      let shouldReset = false;

      setLayouts((current) => {
        const next = current.filter((layout) => layout.id !== layoutId);
        let nextActive = activeLayoutId;
        if (layoutId === activeLayoutId) {
          nextActiveLayout = next[0];
          nextActive = nextActiveLayout?.id ?? '';
          if (!nextActiveLayout) {
            shouldReset = true;
          }
        }
        persistLayouts(next, nextActive || undefined);
        return next;
      });

      if (layoutId === activeLayoutId) {
        if (nextActiveLayout && api) {
          isRestoringRef.current = true;
          api.fromJSON(nextActiveLayout.layout);
          setActiveLayoutId(nextActiveLayout.id);
          setTimeout(() => {
            isRestoringRef.current = false;
          }, 0);
        } else {
          setActiveLayoutId('');
          if (shouldReset) {
            resetToDefaultLayout();
          }
        }
      }
    },
    [activeLayoutId, api, layouts, persistLayouts, resetToDefaultLayout]
  );

  const setApiInstance = useCallback((nextApi: DockviewApi) => {
    setApi(nextApi);
  }, []);

  useEffect(() => {
    const stored = loadWorkspaceState();
    setLayouts(stored.layouts ?? []);
    setActiveLayoutId(stored.activeLayoutId ?? '');
    setStorageLoaded(true);
  }, []);

  useEffect(() => {
    if (!api || !storageLoaded) return;
    if (layouts.length === 0) {
      resetToDefaultLayout();
      return;
    }

    if (!activeLayoutId && layouts.length > 0) {
      setActiveLayoutId(layouts[0].id);
      return;
    }

    if (activeLayoutId) {
      const layout = layouts.find((item) => item.id === activeLayoutId);
      if (layout) {
        isRestoringRef.current = true;
        api.fromJSON(layout.layout);
        setTimeout(() => {
          isRestoringRef.current = false;
        }, 0);
      }
    }
  }, [activeLayoutId, api, layouts, resetToDefaultLayout, storageLoaded]);

  useEffect(() => {
    const summary = layouts.map((layout) => ({
      id: layout.id,
      name: layout.name,
      isDefault: layout.isDefault,
      updatedAt: layout.updatedAt,
    }));
    window.dispatchEvent(
      new CustomEvent('aonik:workspace:state', {
        detail: { layouts: summary, activeLayoutId },
      })
    );
  }, [activeLayoutId, layouts]);

  useEffect(() => {
    if (!api) return;
    const disposable = api.onDidLayoutChange(() => {
      if (isRestoringRef.current) return;
      saveActiveLayout();
    });
    return () => disposable.dispose();
  }, [api, saveActiveLayout]);

  const value = useMemo(
    () => ({
      api,
      setApi: setApiInstance,
      openPanel,
      closePanel,
      maximizeActivePanel,
      exitMaximizedGroup,
      layouts,
      activeLayoutId,
      loadLayout,
      saveActiveLayout,
      createLayoutFromActive,
      resetToDefaultLayout,
      renameLayout,
      removeLayout,
      eventBus,
      dispatchAction,
    }),
    [
      activeLayoutId,
      api,
      closePanel,
      createLayoutFromActive,
      dispatchAction,
      eventBus,
      exitMaximizedGroup,
      setApiInstance,
      layouts,
      loadLayout,
      maximizeActivePanel,
      openPanel,
      renameLayout,
      removeLayout,
      resetToDefaultLayout,
      saveActiveLayout,
    ]
  );

  return (
    <WorkspaceContext.Provider value={value}>{children}</WorkspaceContext.Provider>
  );
}
