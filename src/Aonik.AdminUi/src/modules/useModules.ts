import { useState, useEffect, useMemo, useCallback } from 'react';
import { useAuth } from '@/auth';
import type { RuntimeModuleManifest } from './types';
import {
  getModules,
  getAggregatedNavigation,
  getAggregatedRoutes,
  getAggregatedPanels,
  getAggregatedPanelComponents,
  getDefaultWorkspacePanels,
  resolveBreadcrumb,
} from './registry';
import type { NavigationSection } from '@/types';
import { isBackendModuleEnabled, resolveEnabledUiModules } from './enablement';
import {
  fetchManifestOnce,
  getManifestTenantKey,
  getManifestVersion,
  subscribeManifest,
} from './manifestCache';

export { invalidateModuleManifest } from './manifestCache';

/**
 * Hook that merges build-time module definitions with the runtime manifest
 * fetched from the API. The manifest controls which modules and features
 * are enabled per tenant/user/feature-flag.
 *
 * The manifest is fetched through the shared API client (bearer token and
 * X-Tenant-Id) and cached per selected tenant; `invalidateModuleManifest()`
 * forces every mounted instance to re-fetch.
 *
 * On fetch failure (including 401/403 from the manifest itself), falls back
 * to all modules enabled (graceful degradation).
 *
 * The manifest is never requested while the user is signed out: the endpoint
 * requires an admin user, and the 401 would bounce a first-run visitor on a
 * public route (the setup guides) to /login. Public routes render fail-open.
 */
export function useModules() {
  const { isAuthenticated } = useAuth();
  const [fetchedManifest, setFetchedManifest] = useState<RuntimeModuleManifest | null>(null);
  const [fetchSettled, setFetchSettled] = useState(false);
  const [version, setVersion] = useState(() => getManifestVersion());
  const tenantKey = getManifestTenantKey();

  // Re-fetch whenever the cache is invalidated (tenant switch, logout,
  // successful module toggle).
  useEffect(() => subscribeManifest(() => setVersion(getManifestVersion())), []);

  useEffect(() => {
    if (!isAuthenticated) return undefined;

    let cancelled = false;

    // The first fetch settles `loading`; a re-fetch after invalidation keeps
    // serving the current manifest (fail-open) rather than flashing a
    // loading state.
    fetchManifestOnce()
      .then((data) => {
        if (cancelled) return;
        // null = unreachable or unauthorised → fail-open (all modules on).
        // Always assign so a manifest from a previous tenant never lingers.
        setFetchedManifest(data);
      })
      .catch(() => {
        if (!cancelled) setFetchedManifest(null);
      })
      .finally(() => {
        if (!cancelled) setFetchSettled(true);
      });

    return () => { cancelled = true; };
  }, [tenantKey, version, isAuthenticated]);

  // Signed out: no manifest (fail-open) and nothing to wait for.
  const manifest = isAuthenticated ? fetchedManifest : null;
  const loading = isAuthenticated && !fetchSettled;

  const enabledModules = manifest?.enabledModules;

  // UI module ids enabled for this tenant (undefined = all enabled). A UI
  // module is on when every backend id in its `requires` is enabled.
  const enabledModuleIds = useMemo(
    () => resolveEnabledUiModules(getModules(), enabledModules),
    [enabledModules],
  );

  const isModuleEnabled = useCallback(
    (moduleId: string) => isBackendModuleEnabled(manifest, moduleId),
    [manifest],
  );

  const navigation = useMemo((): NavigationSection[] => {
    const sections = getAggregatedNavigation(enabledModuleIds);

    // Apply runtime overrides: remove disabled nav items
    if (manifest?.disabledNavItems?.length) {
      const disabled = new Set(manifest.disabledNavItems);
      return sections.map((section) => ({
        ...section,
        items: section.items.filter((item) => !disabled.has(item.id)),
      })).filter((section) => section.items.length > 0);
    }

    return sections;
  }, [enabledModuleIds, manifest]);

  const routes = useMemo(() => {
    const allRoutes = getAggregatedRoutes(enabledModuleIds);

    // Apply runtime overrides: remove disabled routes
    if (manifest?.disabledRoutes?.length) {
      const disabled = new Set(manifest.disabledRoutes);
      return allRoutes.filter((r) => !disabled.has(r.path));
    }

    return allRoutes;
  }, [enabledModuleIds, manifest]);

  const panels = useMemo(() => getAggregatedPanels(enabledModuleIds), [enabledModuleIds]);
  const panelComponents = useMemo(() => getAggregatedPanelComponents(enabledModuleIds), [enabledModuleIds]);
  const defaultWorkspacePanels = useMemo(() => getDefaultWorkspacePanels(enabledModuleIds), [enabledModuleIds]);

  const getBreadcrumb = useMemo(() => {
    return (path: string) => resolveBreadcrumb(path);
  }, []);

  return {
    modules: getModules(),
    manifest,
    loading,
    navigation,
    routes,
    panels,
    panelComponents,
    defaultWorkspacePanels,
    getBreadcrumb,
    featureFlags: manifest?.featureFlags ?? {},
    /** Backend module ids enabled for the tenant; undefined = no manifest (fail-open) */
    enabledModules,
    /** UI module ids resolved from `enabledModules`; undefined = all enabled */
    enabledUiModuleIds: enabledModuleIds,
    /** Whether a backend module id is enabled (fail-open without a manifest) */
    isModuleEnabled,
  };
}
