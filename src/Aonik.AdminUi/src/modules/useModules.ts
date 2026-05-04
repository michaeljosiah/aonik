import { useState, useEffect, useMemo } from 'react';
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
import { apiConfig } from '@/auth';

const MANIFEST_CACHE_TTL_MS = 30_000;

let manifestCache: { data: RuntimeModuleManifest; timestamp: number } | null = null;
let manifestInFlight: Promise<RuntimeModuleManifest | null> | null = null;

const buildManifestUrl = (): string => {
  const baseUrl = apiConfig.baseUrl.endsWith('/')
    ? apiConfig.baseUrl.slice(0, -1)
    : apiConfig.baseUrl;

  if (baseUrl === '/api') {
    return '/api/admin/manifest';
  }

  return `${baseUrl}/admin/manifest`;
};

const fetchManifestOnce = async (): Promise<RuntimeModuleManifest | null> => {
  if (manifestCache && Date.now() - manifestCache.timestamp < MANIFEST_CACHE_TTL_MS) {
    return manifestCache.data;
  }

  if (manifestInFlight) {
    return manifestInFlight;
  }

  manifestInFlight = fetch(buildManifestUrl())
    .then(async (res) => {
      if (!res.ok) {
        return null;
      }

      const data = await res.json() as RuntimeModuleManifest;
      manifestCache = { data, timestamp: Date.now() };
      return data;
    })
    .catch(() => null)
    .finally(() => {
      manifestInFlight = null;
    });

  return manifestInFlight;
};

/**
 * Hook that merges build-time module definitions with the runtime manifest
 * fetched from the API. The manifest controls which modules and features
 * are enabled per tenant/user/feature-flag.
 *
 * On fetch failure, falls back to all modules enabled (graceful degradation).
 */
export function useModules() {
  const [manifest, setManifest] = useState<RuntimeModuleManifest | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function fetchManifest() {
      try {
        const data = await fetchManifestOnce();
        if (!cancelled && data) {
          setManifest(data);
        }
      } catch {
        // Graceful degradation: all modules enabled if manifest unreachable
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    fetchManifest();
    return () => { cancelled = true; };
  }, []);

  const enabledModuleIds = useMemo(() => {
    if (!manifest) return undefined; // undefined = all enabled
    return manifest.enabledModules;
  }, [manifest]);

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
  };
}
