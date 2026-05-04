import type { ComponentType } from 'react';
import type { NavigationSection } from '@/types';
import type { WorkspacePanelConfig, WorkspacePanelRenderProps, WorkspaceTemplate } from '@/workspace/types';

/**
 * Route configuration contributed by a module.
 */
export interface ModuleRouteConfig {
  /** Route path (e.g. "/ledger/accounts") */
  path: string;
  /** The React component to render */
  element: ComponentType;
  /** If true, this route has dynamic segments (e.g. ":id") — useful for breadcrumb generation */
  isDynamic?: boolean;
}

/**
 * A single breadcrumb item — either a plain label or a label paired
 * with a navigable href. Items with `href` render as clickable links
 * in the top bar; plain strings render as static text (typically the
 * current page).
 */
export type ModuleBreadcrumbItem = string | { label: string; href: string };

/**
 * Breadcrumb mapping contributed by a module.
 * Maps a path prefix to the breadcrumb trail displayed in the header.
 */
export interface ModuleBreadcrumb {
  /** Path prefix to match (matched with startsWith) */
  pathPrefix: string;
  /**
   * Breadcrumb trail. Each item is either a string (plain text, no
   * navigation) or { label, href } (renders as a Link in the top bar).
   * Use href for parent items so users can navigate back up the tree.
   */
  trail: ModuleBreadcrumbItem[];
}

/**
 * A module definition — the build-time contract for each domain module.
 * Each module contributes routes, navigation, workspace panels, and breadcrumbs.
 */
export interface AdminModule {
  /** Unique module identifier (e.g. "finance", "platform") */
  id: string;
  /** Display name */
  name: string;
  /** Navigation sections contributed by this module */
  navigation: NavigationSection[];
  /** Route definitions contributed by this module */
  routes: ModuleRouteConfig[];
  /** Workspace panel configs contributed by this module */
  panels: WorkspacePanelConfig[];
  /** Workspace panel component map (componentKey -> component) */
  panelComponents: Record<string, ComponentType<WorkspacePanelRenderProps>>;
  /** Default workspace panels to open for this module */
  defaultWorkspacePanels?: string[];
  /** Pre-built workspace templates contributed by this module */
  workspaceTemplates?: WorkspaceTemplate[];
  /** Breadcrumb mappings */
  breadcrumbs: ModuleBreadcrumb[];
}

/**
 * Runtime module manifest returned by the admin manifest endpoint.
 * Controls which modules/features are visible per tenant/user/feature-flag.
 */
export interface RuntimeModuleManifest {
  /** Module IDs that are enabled for the current tenant/user */
  enabledModules: string[];
  /** Feature flags — key is "moduleId:featureId", value is enabled */
  featureFlags: Record<string, boolean>;
  /** Disabled route paths (override build-time routes) */
  disabledRoutes?: string[];
  /** Disabled navigation item IDs */
  disabledNavItems?: string[];
}
