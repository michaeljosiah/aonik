import type { WorkspacePanelConfig, WorkspaceTemplate } from './types';
import { getAggregatedPanels, getDefaultWorkspacePanels, getAggregatedWorkspaceTemplates, getModules, getWorkspaceTemplate as getWorkspaceTemplateFromRegistry } from '@/modules/registry';
import { getCachedManifest, subscribeManifest } from '@/modules/manifestCache';
import { resolveEnabledUiModules } from '@/modules/enablement';

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

/**
 * The workspace is a second front door into module surfaces: its panels and templates come from the
 * same module definitions the router uses, but they are read synchronously and cached, so without
 * this a tenant with Finance off still found the Billing Ops template and its saved Finance panels
 * waiting in the workspace. The enabled set is read from the manifest cache (absent = fail-open,
 * matching everywhere else) and every cache is evicted on invalidation so a toggle takes effect
 * without a reload.
 */
function enabledUiModuleIds(): string[] | undefined {
  return resolveEnabledUiModules(getModules(), getCachedManifest()?.enabledModules);
}

subscribeManifest(() => {
  _panelRegistryCache = null;
  _defaultPanelsCache = null;
  _templatesCache = null;
});

function getPanelRegistry(): WorkspacePanelConfig[] {
  if (!_panelRegistryCache) {
    _panelRegistryCache = getAggregatedPanels(enabledUiModuleIds());
  }
  return _panelRegistryCache;
}

function getDefaultPanels(): string[] {
  if (!_defaultPanelsCache) {
    _defaultPanelsCache = getDefaultWorkspacePanels(enabledUiModuleIds());
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
    _templatesCache = getAggregatedWorkspaceTemplates(enabledUiModuleIds());
  }
  return _templatesCache;
}

export function getWorkspaceTemplates(): WorkspaceTemplate[] {
  return getTemplateRegistry();
}

export function getWorkspaceTemplateById(templateId: string): WorkspaceTemplate | undefined {
  return getWorkspaceTemplateFromRegistry(templateId);
}
