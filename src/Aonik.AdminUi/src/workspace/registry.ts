import type { WorkspacePanelConfig, WorkspaceTemplate } from './types';
import { getAggregatedPanels, getDefaultWorkspacePanels, getAggregatedWorkspaceTemplates, getWorkspaceTemplate as getWorkspaceTemplateFromRegistry } from '@/modules/registry';

/**
 * Workspace panel registry — now aggregated from module definitions.
 * Uses lazy initialization to avoid circular dependency issues during
 * module evaluation. Values are computed on first access and cached.
 *
 * For backward compatibility, all existing callsites that import
 * `workspacePanelRegistry`, `getWorkspacePanelConfig`, `getWorkspacePanelForApp`,
 * `getWorkspacePanelForRoute`, and `defaultWorkspaceLayoutPanels` continue to work.
 */

let _panelRegistryCache: WorkspacePanelConfig[] | null = null;
let _defaultPanelsCache: string[] | null = null;

function getPanelRegistry(): WorkspacePanelConfig[] {
  if (!_panelRegistryCache) {
    _panelRegistryCache = getAggregatedPanels();
  }
  return _panelRegistryCache;
}

function getDefaultPanels(): string[] {
  if (!_defaultPanelsCache) {
    _defaultPanelsCache = getDefaultWorkspacePanels();
  }
  return _defaultPanelsCache;
}

/**
 * @deprecated Prefer using `getWorkspacePanelConfig` / `getWorkspacePanelForApp` /
 * `getWorkspacePanelForRoute` instead of reading this array directly.
 */
export const workspacePanelRegistry: WorkspacePanelConfig[] = new Proxy([] as WorkspacePanelConfig[], {
  get(_target, prop, receiver) {
    const registry = getPanelRegistry();
    return Reflect.get(registry, prop, receiver);
  },
});

/**
 * @deprecated Prefer calling `getDefaultWorkspacePanels()` from `@/modules/registry` instead.
 */
export const defaultWorkspaceLayoutPanels: string[] = new Proxy([] as string[], {
  get(_target, prop, receiver) {
    const panels = getDefaultPanels();
    return Reflect.get(panels, prop, receiver);
  },
});

export function getWorkspacePanelConfig(panelId: string) {
  return getPanelRegistry().find((panel) => panel.id === panelId);
}

export function getWorkspacePanelForApp(appCardId: string) {
  return getPanelRegistry().find((panel) => panel.appCardId === appCardId);
}

/**
 * Find a workspace panel config that matches the given route.
 * Only returns **micro-app** panels — pages (`category: 'page'` or unset)
 * are never redirected into the workspace dock. They render as normal
 * full-page routes.
 */
export function getWorkspacePanelForRoute(route?: string) {
  if (!route) return undefined;
  return getPanelRegistry().find(
    (panel) => panel.route === route && panel.category === 'micro-app',
  );
}

// ---------------------------------------------------------------------------
// Workspace templates
// ---------------------------------------------------------------------------

let _templatesCache: WorkspaceTemplate[] | null = null;

function getTemplateRegistry(): WorkspaceTemplate[] {
  if (!_templatesCache) {
    _templatesCache = getAggregatedWorkspaceTemplates();
  }
  return _templatesCache;
}

export function getWorkspaceTemplates(): WorkspaceTemplate[] {
  return getTemplateRegistry();
}

export function getWorkspaceTemplateById(templateId: string): WorkspaceTemplate | undefined {
  return getWorkspaceTemplateFromRegistry(templateId);
}
