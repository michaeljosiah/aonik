export type { AdminModule, ModuleRouteConfig, ModuleBreadcrumb, RuntimeModuleManifest, ManifestModule } from './types';
export {
  resolveEnabledUiModules,
  filterNavByModules,
  resolveDisabledModuleForPath,
  matchesRoutePath,
  isBackendModuleEnabled,
  pathRequiresBackendModule,
} from './enablement';
export type { DisabledModuleMatch, NavFilterOptions } from './enablement';
export { invalidateModuleManifest } from './manifestCache';
export { useModuleEnabled } from './useModuleEnabled';
export { financeModule } from './finance';
export { platformModule } from './platform';
export { coreModule } from './core';
export {
  getModules,
  getModule,
  getAggregatedNavigation,
  getAggregatedRoutes,
  getAggregatedPanels,
  getAggregatedPanelComponents,
  getDefaultWorkspacePanels,
  getAggregatedBreadcrumbs,
  resolveBreadcrumb,
} from './registry';
export { useModules } from './useModules';
