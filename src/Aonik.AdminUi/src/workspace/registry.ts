import type { WorkspacePanelConfig } from './types';
import { getAggregatedPanels, getDefaultWorkspacePanels } from '@/modules/registry';

/**
 * Workspace panel registry — now aggregated from module definitions.
 * This is a computed getter so it always reflects the current module state.
 *
 * For backward compatibility, all existing callsites that import
 * `workspacePanelRegistry`, `getWorkspacePanelConfig`, `getWorkspacePanelForApp`,
 * `getWorkspacePanelForRoute`, and `defaultWorkspaceLayoutPanels` continue to work.
 */
export const workspacePanelRegistry: WorkspacePanelConfig[] = getAggregatedPanels();

export const defaultWorkspaceLayoutPanels: string[] = getDefaultWorkspacePanels();

export function getWorkspacePanelConfig(panelId: string) {
  return workspacePanelRegistry.find((panel) => panel.id === panelId);
}

export function getWorkspacePanelForApp(appCardId: string) {
  return workspacePanelRegistry.find((panel) => panel.appCardId === appCardId);
}

export function getWorkspacePanelForRoute(route?: string) {
  if (!route) return undefined;
  return workspacePanelRegistry.find((panel) => panel.route === route);
}
