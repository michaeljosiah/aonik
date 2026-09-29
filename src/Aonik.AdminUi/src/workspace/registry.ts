import type { WorkspacePanelConfig, WorkspaceTemplate } from './types';
import { getDefaultWorkspacePanels, getAggregatedWorkspaceTemplates, getModules } from '@/modules/registry';
import { getCachedManifest } from '@/modules/manifestCache';
import { resolveCurrentAdminProfile } from '@/modules/profileCatalog';
import { resolveProfilePanels } from '@/modules/profilePanels';

function getPanelRegistry(): WorkspacePanelConfig[] {
  const modules = getModules();
  const manifest = getCachedManifest();
  return resolveProfilePanels(modules, resolveCurrentAdminProfile(modules, manifest), manifest);
}

function getDefaultPanels(): string[] {
  const allowed = new Set(getPanelRegistry().map((panel) => panel.id));
  return getDefaultWorkspacePanels().filter((id) => allowed.has(id));
}

export const workspacePanelRegistry: WorkspacePanelConfig[] = new Proxy([] as WorkspacePanelConfig[], {
  get(_target, prop, receiver) { return Reflect.get(getPanelRegistry(), prop, receiver); },
});
export const defaultWorkspaceLayoutPanels: string[] = new Proxy([] as string[], {
  get(_target, prop, receiver) { return Reflect.get(getDefaultPanels(), prop, receiver); },
});

export function getWorkspacePanelConfig(panelId: string) {
  return getPanelRegistry().find((panel) => panel.id === panelId);
}
export function getWorkspacePanelForApp(appCardId: string) {
  return getPanelRegistry().find((panel) => panel.appCardId === appCardId);
}
export function getWorkspacePanelForRoute(route?: string) {
  return route ? getPanelRegistry().find((panel) => panel.route === route && panel.category === 'micro-app') : undefined;
}
export function getWorkspaceTemplates(): WorkspaceTemplate[] {
  const allowed = new Set(getPanelRegistry().map((panel) => panel.id));
  return getAggregatedWorkspaceTemplates()
    .map((template) => ({ ...template, panels: template.panels.filter((id) => allowed.has(id)) }))
    .filter((template) => template.panels.length > 0);
}
export function getWorkspaceTemplateById(templateId: string): WorkspaceTemplate | undefined {
  return getWorkspaceTemplates().find((template) => template.id === templateId);
}
