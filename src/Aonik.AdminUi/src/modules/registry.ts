import type { AdminModule } from './types';
import type { NavigationSection } from '@/types';
import type { WorkspacePanelConfig, WorkspacePanelRenderProps, WorkspaceTemplate } from '@/workspace/types';
import type { ComponentType } from 'react';
import type { ModuleBreadcrumb, ModuleRouteConfig } from './types';

import { coreModule } from './core';
import { financeModule } from './finance';
import { platformModule } from './platform';
import { agentCommandCenterModule } from './agent-command-center';
import { agentExtensionsModule } from './agent-extensions';

// ---------------------------------------------------------------------------
// Build-time module registry
// All modules are registered here. To add a new module, import it and add
// it to this array. Order matters for navigation rendering.
// ---------------------------------------------------------------------------
const allModules: AdminModule[] = [
  coreModule,
  agentCommandCenterModule,
  agentExtensionsModule,
  financeModule,
  platformModule,
];

/**
 * Get all registered modules.
 */
export function getModules(): AdminModule[] {
  return allModules;
}

/**
 * Get a module by its ID.
 */
export function getModule(id: string): AdminModule | undefined {
  return allModules.find((m) => m.id === id);
}

/**
 * Aggregate navigation sections from all enabled modules.
 * Merges sections with the same ID (e.g. both platform and finance contribute
 * to the "Finance" nav section).
 */
export function getAggregatedNavigation(enabledModuleIds?: string[]): NavigationSection[] {
  const modules = enabledModuleIds
    ? allModules.filter((m) => enabledModuleIds.includes(m.id))
    : allModules;

  const sectionMap = new Map<string, NavigationSection>();

  for (const mod of modules) {
    for (const section of mod.navigation) {
      const existing = sectionMap.get(section.id);
      if (existing) {
        // Merge items from multiple modules contributing to the same section
        existing.items = [...existing.items, ...section.items];
      } else {
        sectionMap.set(section.id, { ...section, items: [...section.items] });
      }
    }
  }

  return Array.from(sectionMap.values());
}

/**
 * Aggregate all routes from all enabled modules.
 */
export function getAggregatedRoutes(enabledModuleIds?: string[]): ModuleRouteConfig[] {
  const modules = enabledModuleIds
    ? allModules.filter((m) => enabledModuleIds.includes(m.id))
    : allModules;

  return modules.flatMap((m) => m.routes);
}

/**
 * Aggregate all workspace panels from all enabled modules.
 */
export function getAggregatedPanels(enabledModuleIds?: string[]): WorkspacePanelConfig[] {
  const modules = enabledModuleIds
    ? allModules.filter((m) => enabledModuleIds.includes(m.id))
    : allModules;

  return modules.flatMap((m) => m.panels);
}

/**
 * Aggregate all workspace panel components from all enabled modules.
 */
export function getAggregatedPanelComponents(enabledModuleIds?: string[]): Record<string, ComponentType<WorkspacePanelRenderProps>> {
  const modules = enabledModuleIds
    ? allModules.filter((m) => enabledModuleIds.includes(m.id))
    : allModules;

  const result: Record<string, ComponentType<WorkspacePanelRenderProps>> = {};
  for (const mod of modules) {
    Object.assign(result, mod.panelComponents);
  }
  return result;
}

/**
 * Get default workspace panels from all enabled modules.
 */
export function getDefaultWorkspacePanels(enabledModuleIds?: string[]): string[] {
  const modules = enabledModuleIds
    ? allModules.filter((m) => enabledModuleIds.includes(m.id))
    : allModules;

  return modules.flatMap((m) => m.defaultWorkspacePanels ?? []);
}

/**
 * Aggregate all workspace templates from all enabled modules.
 */
export function getAggregatedWorkspaceTemplates(enabledModuleIds?: string[]): WorkspaceTemplate[] {
  const modules = enabledModuleIds
    ? allModules.filter((m) => enabledModuleIds.includes(m.id))
    : allModules;

  return modules.flatMap((m) => m.workspaceTemplates ?? []);
}

/**
 * Get a workspace template by its ID.
 */
export function getWorkspaceTemplate(templateId: string): WorkspaceTemplate | undefined {
  return allModules.flatMap((m) => m.workspaceTemplates ?? []).find((t) => t.id === templateId);
}

/**
 * Aggregate all breadcrumb mappings from all enabled modules.
 * Returns them sorted by path length (longest first) for correct matching.
 */
export function getAggregatedBreadcrumbs(enabledModuleIds?: string[]): ModuleBreadcrumb[] {
  const modules = enabledModuleIds
    ? allModules.filter((m) => enabledModuleIds.includes(m.id))
    : allModules;

  return modules
    .flatMap((m) => m.breadcrumbs)
    .sort((a, b) => b.pathPrefix.length - a.pathPrefix.length);
}

/**
 * Resolve breadcrumb trail for a given path using module-contributed breadcrumbs.
 * Returns the typed trail (strings + {label, href} entries) so the top
 * bar can render the parent items as navigable links.
 */
export function resolveBreadcrumb(path: string): import('./types').ModuleBreadcrumbItem[] {
  const breadcrumbs = getAggregatedBreadcrumbs();
  for (const bc of breadcrumbs) {
    if (path.startsWith(bc.pathPrefix)) {
      return bc.trail;
    }
  }
  return ['Dashboard'];
}
