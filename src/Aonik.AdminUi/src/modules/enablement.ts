import type { NavItem, NavigationSection } from '@/types';
import type { AdminModule, RuntimeModuleManifest } from './types';

// ---------------------------------------------------------------------------
// Module enablement — pure helpers (Spec 097 §8).
//
// Everything here is side-effect free so it can be unit-tested in a node
// environment and shared by the sidebar, the router fallback and the
// settings surfaces. The single rule lives here; callers never reimplement
// it.
// ---------------------------------------------------------------------------

/**
 * Resolve which build-time UI modules are enabled given the backend module
 * ids the manifest reports as enabled.
 *
 *  - `undefined` in → `undefined` out. No manifest means fail-open: every
 *    registered UI module stays enabled.
 *  - A UI module is enabled when every id in its `requires` list is present
 *    in `enabledBackendIds`. An empty or absent `requires` always passes.
 */
export function resolveEnabledUiModules(
  modules: AdminModule[],
  enabledBackendIds: string[] | undefined,
): string[] | undefined {
  if (enabledBackendIds === undefined) return undefined;
  const enabled = new Set(enabledBackendIds);
  return modules
    .filter((m) => (m.requires ?? []).every((id) => enabled.has(id)))
    .map((m) => m.id);
}

export interface NavFilterOptions {
  /**
   * Extra per-item predicate applied alongside the module rule (audience,
   * disabled nav ids, disabled routes). An item failing it is dropped with
   * its whole subtree, exactly like a disabled module.
   */
  isItemVisible?: (item: NavItem) => boolean;
}

function isNavItemVisible(
  item: NavItem,
  enabledSet: ReadonlySet<string> | null,
  options: NavFilterOptions | undefined,
): boolean {
  if (options?.isItemVisible && !options.isItemVisible(item)) return false;
  if (item.moduleId && enabledSet && !enabledSet.has(item.moduleId)) return false;
  return true;
}

function filterNavItems(
  items: NavItem[],
  enabledSet: ReadonlySet<string> | null,
  options: NavFilterOptions | undefined,
): NavItem[] {
  return items.reduce<NavItem[]>((acc, item) => {
    if (!isNavItemVisible(item, enabledSet, options)) return acc;

    const children = item.children ? filterNavItems(item.children, enabledSet, options) : undefined;
    const childGroups = item.childGroups
      ?.map((group) => ({ ...group, items: filterNavItems(group.items, enabledSet, options) }))
      .filter((group) => group.items.length > 0);

    const hasVisibleChildren = (children?.length ?? 0) > 0 || (childGroups?.length ?? 0) > 0;

    // A parent whose every child was filtered away and that has no href of
    // its own collapses — an empty flyout is worse than no entry at all.
    if (!hasVisibleChildren && !item.href) return acc;

    acc.push({
      ...item,
      children: item.children ? children : undefined,
      childGroups: item.childGroups ? childGroups : undefined,
    });
    return acc;
  }, []);
}

/**
 * Apply the sidebar's module rule to a navigation tree.
 *
 *  - `enabledSet === null` → no manifest, render everything (fail-open).
 *  - Otherwise an item carrying `moduleId` renders only when that id is in
 *    the set. Items without `moduleId` are unaffected.
 *  - Parents left with no visible children and no `href` collapse, and
 *    sections left with no items are dropped.
 */
export function filterNavByModules(
  sections: NavigationSection[],
  enabledSet: ReadonlySet<string> | null,
  options?: NavFilterOptions,
): NavigationSection[] {
  return sections
    .map((section) => ({ ...section, items: filterNavItems(section.items, enabledSet, options) }))
    .filter((section) => section.items.length > 0);
}

function normalisePath(path: string): string[] {
  const withoutQuery = path.split(/[?#]/, 1)[0] ?? '';
  return withoutQuery.split('/').filter((segment) => segment.length > 0);
}

/**
 * Match a concrete pathname against a route pattern using react-router
 * conventions: `:param` segments match any single segment, a trailing `*`
 * matches the rest of the path. Query strings and hashes are ignored.
 */
export function matchesRoutePath(pattern: string, path: string): boolean {
  const patternSegments = normalisePath(pattern);
  const pathSegments = normalisePath(path);

  for (let i = 0; i < patternSegments.length; i += 1) {
    const expected = patternSegments[i];
    if (expected === '*') return true;
    const actual = pathSegments[i];
    if (actual === undefined) return false;
    if (expected.startsWith(':')) continue;
    if (expected !== actual) return false;
  }

  return patternSegments.length === pathSegments.length;
}

export interface DisabledModuleMatch {
  /** The registered UI module whose route matched */
  uiModuleId: string;
  /** The backend module id that is missing from the manifest */
  backendModuleId: string;
  /** Human-readable name of the backend module (falls back to the UI module name) */
  name: string;
}

/**
 * Find the registered UI module whose routes match `path` when that module
 * is disabled by the manifest. Returns `null` when there is no manifest,
 * when no route matches, or when the owning module is enabled.
 */
export function resolveDisabledModuleForPath(
  modules: AdminModule[],
  manifest: RuntimeModuleManifest | null | undefined,
  path: string,
): DisabledModuleMatch | null {
  if (!manifest) return null;

  const enabledBackend = new Set(manifest.enabledModules ?? []);
  const enabledUi = new Set(resolveEnabledUiModules(modules, manifest.enabledModules ?? []) ?? []);

  for (const mod of modules) {
    if (enabledUi.has(mod.id)) continue;
    if (!mod.routes.some((route) => matchesRoutePath(route.path, path))) continue;

    const backendModuleId = (mod.requires ?? []).find((id) => !enabledBackend.has(id)) ?? mod.id;
    const described = (manifest.modules ?? []).find((m) => m.id === backendModuleId);
    return {
      uiModuleId: mod.id,
      backendModuleId,
      name: described?.name ?? mod.name,
    };
  }

  return null;
}

/**
 * Whether `path` is owned by a registered UI module that requires
 * `backendModuleId`. Registry-only (no manifest), so the API client can
 * decide, on a 403 `module.disabled`, whether the CURRENT page belongs to
 * the module the server just refused (navigate to the explanation page) or
 * whether a shared page merely called a gated endpoint (let the page render
 * its own error; never bounce it, or the home page would loop).
 */
export function pathRequiresBackendModule(
  modules: AdminModule[],
  backendModuleId: string,
  path: string,
): boolean {
  return modules.some((mod) =>
    (mod.requires ?? []).includes(backendModuleId)
    && mod.routes.some((route) => matchesRoutePath(route.path, path)));
}

/**
 * Whether a backend module id is enabled according to the manifest.
 * A missing manifest is fail-open (true).
 */
export function isBackendModuleEnabled(
  manifest: RuntimeModuleManifest | null | undefined,
  moduleId: string,
): boolean {
  if (!manifest) return true;
  return manifest.enabledModules.includes(moduleId);
}
