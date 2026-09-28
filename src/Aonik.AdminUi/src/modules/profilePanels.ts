import type { AdminModule, RuntimeModuleManifest } from './types';
import type { ResolvedAdminProfile } from './adminProfiles';
import { findScreenRoute } from './profileCatalog';

export function resolveProfilePanels(modules: AdminModule[], profile: ResolvedAdminProfile | null, manifest: RuntimeModuleManifest | null) {
  const screenIds = new Set(profile?.routes.map((route) => route.screen?.id));
  if (!screenIds.has('workspace')) return [];
  return modules.flatMap((module) => module.panels)
    .filter((panel) => {
      const screenId = panel.screenId ?? (panel.route ? findScreenRoute(modules, panel.route)?.screen?.id : undefined);
      return !!screenId && screenIds.has(screenId) &&
        (panel.requiredPolicies ?? []).every((policy) => manifest?.allowedPolicies?.includes(policy));
    })
    .map((panel) => ({ ...panel, title: panel.category === 'page'
      ? profile?.labels.get(panel.screenId ?? findScreenRoute(modules, panel.route ?? '')?.screen?.id ?? '') ?? panel.title
      : panel.title }));
}
