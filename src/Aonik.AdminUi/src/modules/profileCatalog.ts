import generalJson from '@/config/admin-profiles/general.json';
import foodCommerceJson from '@/config/admin-profiles/food-commerce.json';
import arkeKidsJson from '@/config/admin-profiles/arke-kids.json';
import { parseAdminProfile, resolveAdminProfile, selectAdminProfile } from './adminProfiles';
import type { ResolvedAdminProfile } from './adminProfiles';
import type { AdminModule, RuntimeModuleManifest } from './types';
import { matchesRoutePath } from './enablement';

export const adminProfiles = [generalJson, foodCommerceJson, arkeKidsJson].map(parseAdminProfile);
export const adminProfileBusinessTypes: Readonly<Record<string, string>> = {
  base: 'general', simi: 'general', 'food-commerce': 'food-commerce', 'arke-kids': 'arke-kids',
};

export function resolveCurrentAdminProfile(modules: AdminModule[], manifest: RuntimeModuleManifest | null) {
  if (!manifest || !Array.isArray(manifest.allowedPolicies) ||
      !manifest.allowedPolicies.includes('AdminUserPolicy')) return null;
  const isHost = manifest.allowedPolicies.includes('PlatformAdmin');
  // Host administration retains its own reviewed inventory, regardless of selected tenant vertical.
  const profile = selectAdminProfile(isHost ? 'base' : manifest.businessType, adminProfiles, adminProfileBusinessTypes);
  return profile ? resolveAdminProfile(profile, modules, manifest, isHost ? 'host' : 'tenant') : null;
}

/** Match against ALL routes first: /invoices/new must not fall through to an allowed /invoices/:id. */
export function findScreenRoute(modules: AdminModule[], path: string) {
  const pathname = path.split(/[?#]/, 1)[0];
  const score = (pattern: string) => pattern.split('/').reduce((total, part) => total + (part === '*' ? -10 : part.startsWith(':') ? 3 : 10), 0);
  return modules.flatMap((module) => module.routes)
    .filter((route) => matchesRoutePath(route.path, pathname))
    .sort((a, b) => score(b.path) - score(a.path))[0];
}

export function isProfilePathVisible(modules: AdminModule[], profile: ResolvedAdminProfile | null, path: string): boolean {
  const route = findScreenRoute(modules, path);
  return !!route && !!profile?.routes.some((allowed) => allowed.path === route.path);
}

export function profilePathLabel(modules: AdminModule[], profile: ResolvedAdminProfile | null, path: string, fallback: string): string {
  const route = findScreenRoute(modules, path);
  return (route?.screen && profile?.labels.get(route.screen.id)) ?? fallback;
}
