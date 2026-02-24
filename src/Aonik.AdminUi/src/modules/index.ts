export type { AdminModule, ModuleRouteConfig, ModuleBreadcrumb, RuntimeModuleManifest } from './types';
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
