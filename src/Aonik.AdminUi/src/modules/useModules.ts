import { createContext, createElement, useContext, useEffect, useMemo, useSyncExternalStore, type ReactNode } from 'react';
import { useAuth } from '@/auth';
import { getModules, getAggregatedPanelComponents, getDefaultWorkspacePanels, resolveBreadcrumb } from './registry';
import { resolveEnabledUiModules } from './enablement';
import { resolveCurrentAdminProfile, isProfilePathVisible, profilePathLabel, findScreenRoute } from './profileCatalog';
import { resolveProfilePanels } from './profilePanels';
import { fetchManifestOnce, getCachedManifest, getManifestContextKey, getManifestStatus, getManifestVersion, getManifestTenantKey, setManifestIdentity, subscribeManifest } from './manifestCache';
import type { ModuleBreadcrumbItem } from './types';

export { invalidateModuleManifest } from './manifestCache';

function useResolvedModules() {
  const { isAuthenticated, user, provider } = useAuth();
  const version = useSyncExternalStore(subscribeManifest, getManifestVersion);
  const identity = isAuthenticated && user ? `${provider}:${user.id}` : '';
  const tenantKey = getManifestTenantKey();
  const contextKey = JSON.stringify([identity, tenantKey]);
  const contextMatches = contextKey === getManifestContextKey();

  useEffect(() => {
    setManifestIdentity(identity);
    if (identity && tenantKey && getManifestStatus() === 'idle') void fetchManifestOnce();
  }, [identity, tenantKey, version]);

  const manifest = isAuthenticated && contextMatches ? getCachedManifest() : null;
  const status = contextMatches ? getManifestStatus() : 'idle';
  const loading = isAuthenticated && !manifest && (status === 'idle' || status === 'loading');
  const modules = getModules();
  const profile = useMemo(() => resolveCurrentAdminProfile(modules, manifest), [modules, manifest]);
  const enabledModules = manifest?.enabledModules ?? [];
  const enabledUiModuleIds = resolveEnabledUiModules(modules, enabledModules) ?? [];
  const panels = useMemo(() => resolveProfilePanels(modules, profile, manifest), [modules, profile, manifest]);
  const panelIds = new Set(panels.map((panel) => panel.id));
  const components = getAggregatedPanelComponents(enabledUiModuleIds);
  const panelComponents = Object.fromEntries(Object.entries(components).filter(([key]) => panels.some((panel) => panel.componentKey === key)));

  return {
    modules, manifest, loading, contextKey, profile,
    unavailable: isAuthenticated && !loading && !profile,
    navigation: profile?.navigation ?? [],
    routes: profile?.routes ?? [],
    panels, panelComponents,
    defaultWorkspacePanels: getDefaultWorkspacePanels(enabledUiModuleIds).filter((id) => panelIds.has(id)),
    featureFlags: manifest?.featureFlags ?? {},
    enabledModules, enabledUiModuleIds,
    landingPath: profile?.landingPath ?? null,
    isModuleEnabled: (id: string) => enabledModules.includes(id),
    isScreenVisible: (id: string) => !!profile?.routes.some((route) => route.screen?.id === id),
    isPathVisible: (path: string) => isProfilePathVisible(modules, profile, path),
    labelForPath: (path: string, fallback: string) => profilePathLabel(modules, profile, path, fallback),
    hasPermission: (permission: string) => manifest?.permissions?.includes(permission) ?? false,
    allowsPolicy: (policy: string) => manifest?.allowedPolicies?.includes(policy) ?? false,
    getBreadcrumb: (path: string): ModuleBreadcrumbItem[] => {
      const trail = resolveBreadcrumb(path).map((item) => {
        if (typeof item === 'string') return item;
        const label = profilePathLabel(modules, profile, item.href, item.label);
        return isProfilePathVisible(modules, profile, item.href) ? { ...item, label } : label;
      });
      if (findScreenRoute(modules, path)) {
        const label = profilePathLabel(modules, profile, path, '');
        if (label) return [...trail.slice(0, -1), label];
      }
      return trail;
    },
  };
}

const ModulesContext = createContext<ReturnType<typeof useResolvedModules> | null>(null);

export function AdminModulesProvider({ children }: { children: ReactNode }) {
  const value = useResolvedModules();
  return createElement(ModulesContext.Provider, { value }, children);
}

export function useModules() {
  const context = useContext(ModulesContext);
  if (!context) throw new Error('useModules requires AdminModulesProvider');
  return context;
}
