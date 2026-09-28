import type { NavigationSection, NavItem } from '@/types';
import type { AdminModule, AdminScreenDefinition, ModuleRouteConfig, RuntimeModuleManifest } from './types';

export interface AdminProfileScreen {
  label?: string;
}

export type AdminProfileNavItem =
  | { screen: string }
  | { id: string; label: string; items: { screen: string }[] };

export interface AdminProfile {
  id: string;
  landingPage: string;
  screens: Record<string, AdminProfileScreen>;
  navigation: { id: string; label: string; items: AdminProfileNavItem[] }[];
}

interface RegisteredScreen {
  route: ModuleRouteConfig;
  definition: AdminScreenDefinition;
  requires: string[];
}

export interface ResolvedAdminProfile {
  routes: ModuleRouteConfig[];
  navigation: NavigationSection[];
  labels: ReadonlyMap<string, string>;
  landingPath: string | null;
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function assertFields(value: unknown, allowed: string[], required: string[], context: string): asserts value is Record<string, unknown> {
  if (!isObject(value) || Object.keys(value).some((key) => !allowed.includes(key)) ||
      required.some((key) => !Object.hasOwn(value, key))) {
    throw new Error(`Invalid ${context} fields`);
  }
}

function assertText(value: unknown, context: string): asserts value is string {
  if (typeof value !== 'string' || !value.trim()) throw new Error(`Invalid ${context}`);
}

/** Validate imported JSON without trusting a TypeScript cast to enforce its schema. */
export function parseAdminProfile(value: unknown): AdminProfile {
  assertFields(value, ['id', 'landingPage', 'screens', 'navigation'], ['id', 'landingPage', 'screens', 'navigation'], 'profile');
  assertText(value.id, 'profile id');
  assertText(value.landingPage, 'landing page');
  if (!isObject(value.screens)) throw new Error('Invalid profile screens');
  for (const [id, screen] of Object.entries(value.screens)) {
    assertText(id, 'screen id');
    assertFields(screen, ['label'], [], `screen ${id}`);
    if (screen.label !== undefined) assertText(screen.label, `screen ${id} label`);
  }
  if (!Array.isArray(value.navigation)) throw new Error('Invalid profile navigation');
  const navIds = new Set<string>();
  const addNavId = (id: unknown) => {
    assertText(id, 'navigation id');
    if (navIds.has(id)) throw new Error(`Duplicate navigation id: ${id}`);
    navIds.add(id);
  };
  const checkReference = (item: unknown) => {
    assertFields(item, ['screen'], ['screen'], 'navigation screen');
    assertText(item.screen, 'navigation screen id');
    addNavId(item.screen);
  };
  for (const section of value.navigation) {
    assertFields(section, ['id', 'label', 'items'], ['id', 'label', 'items'], 'navigation section');
    addNavId(section.id);
    assertText(section.label, 'section label');
    if (!Array.isArray(section.items)) throw new Error('Invalid section items');
    for (const item of section.items) {
      if (isObject(item) && Object.hasOwn(item, 'screen')) {
        checkReference(item);
      } else {
        assertFields(item, ['id', 'label', 'items'], ['id', 'label', 'items'], 'navigation group');
        addNavId(item.id);
        assertText(item.label, 'group label');
        if (!Array.isArray(item.items)) throw new Error('Invalid group items');
        item.items.forEach(checkReference);
      }
    }
  }
  return value as unknown as AdminProfile;
}

function getScreens(modules: AdminModule[]): Map<string, RegisteredScreen> {
  const screens = new Map<string, RegisteredScreen>();
  for (const module of modules) {
    for (const route of module.routes) {
      if (!route.screen) continue; // Unclassified routes cannot participate in a profile.
      const definition = route.screen;
      assertText(definition.id, 'registered screen id');
      assertText(definition.label, `screen ${definition.id} label`);
      if (screens.has(definition.id)) throw new Error(`Duplicate screen id: ${definition.id}`);
      const rule = definition.permissions;
      if (!rule || ('authenticatedAdmin' in rule
        ? rule.authenticatedAdmin !== true || Object.keys(rule).length !== 1
        : !(rule.allOf?.length || rule.anyOf?.length) ||
          [rule.allOf, rule.anyOf].some((keys) => keys !== undefined &&
            (!Array.isArray(keys) || !keys.length || keys.some((key) => typeof key !== 'string' || !key.trim()))))) {
        throw new Error(`Missing permission rule: ${definition.id}`);
      }
      screens.set(definition.id, {
        route,
        definition,
        requires: [...(module.requires ?? []), ...(route.requires ?? [])],
      });
    }
  }
  return screens;
}

function isNavigable(route: ModuleRouteConfig): boolean {
  return !route.isDynamic && !/[:*]/.test(route.path);
}

/** Call during configuration tests as well as resolution: no unknown or implicit destinations. */
export function validateAdminProfile(profile: AdminProfile, modules: AdminModule[]): void {
  parseAdminProfile(profile);
  const screens = getScreens(modules);
  for (const id of Object.keys(profile.screens)) {
    if (!screens.has(id)) throw new Error(`Unknown or unclassified screen: ${id}`);
  }
  const requireNavigable = (id: string) => {
    const screen = screens.get(id);
    if (!Object.hasOwn(profile.screens, id) || !screen || !isNavigable(screen.route)) {
      throw new Error(`Screen is not a profile navigation destination: ${id}`);
    }
  };
  requireNavigable(profile.landingPage);
  for (const section of profile.navigation) {
    for (const item of section.items) {
      if ('screen' in item) requireNavigable(item.screen);
      else item.items.forEach((child) => requireNavigable(child.screen));
    }
  }
}

/** Pure presentation resolution. A null result means unavailable, never all screens enabled. */
export function resolveAdminProfile(
  profile: AdminProfile,
  modules: AdminModule[],
  manifest: RuntimeModuleManifest | null,
  audience: 'host' | 'tenant',
): ResolvedAdminProfile | null {
  if (!manifest || typeof manifest.businessType !== 'string' || !manifest.businessType.trim() ||
      !Array.isArray(manifest.permissions) || !manifest.permissions.every((key) => typeof key === 'string') ||
      !Array.isArray(manifest.enabledModules)) return null;

  validateAdminProfile(profile, modules);
  const registered = getScreens(modules);
  const permissions = new Set(manifest.permissions);
  const modulesEnabled = new Set(manifest.enabledModules);
  const visible = new Map<string, RegisteredScreen>();
  const labels = new Map<string, string>();

  for (const [id, presentation] of Object.entries(profile.screens)) {
    const screen = registered.get(id)!;
    const { definition, route, requires } = screen;
    const rule = definition.permissions;
    if (definition.audience && definition.audience !== 'all' && definition.audience !== audience) continue;
    if (definition.policy && !manifest.allowedPolicies?.includes(definition.policy)) continue;
    if (!requires.every((required) => modulesEnabled.has(required))) continue;
    if (manifest.disabledRoutes?.includes(route.path)) continue;
    if (!('authenticatedAdmin' in rule) &&
        (!(rule.allOf ?? []).every((key) => permissions.has(key)) ||
         (rule.anyOf !== undefined && !rule.anyOf.some((key) => permissions.has(key))))) continue;
    visible.set(id, screen);
    labels.set(id, presentation.label ?? definition.label);
  }

  const navItem = (id: string): NavItem[] => {
    const screen = visible.get(id);
    if (!screen || manifest.disabledNavItems?.includes(id)) return [];
    return [{ id, label: labels.get(id)!, href: screen.route.path, icon: screen.definition.icon ?? 'dashboard' }];
  };
  const navigation: NavigationSection[] = profile.navigation.flatMap((section) => {
    if (manifest.disabledNavItems?.includes(section.id)) return [];
    const items = section.items.flatMap((item): NavItem[] => {
      if ('screen' in item) return navItem(item.screen);
      if (manifest.disabledNavItems?.includes(item.id)) return [];
      const children = item.items.flatMap((child) => navItem(child.screen));
      return children.length ? [{ id: item.id, label: item.label, icon: 'stack', children }] : [];
    });
    return items.length ? [{ id: section.id, label: section.label, items }] : [];
  });
  const firstItem = navigation[0]?.items[0];
  const firstPath = firstItem?.href ?? firstItem?.children?.[0]?.href ?? null;
  return {
    routes: [...visible.values()].map((screen) => screen.route),
    navigation,
    labels,
    landingPath: visible.get(profile.landingPage)?.route.path ?? firstPath,
  };
}

/** Missing context is an error; only a known, nonempty business type may use the general fallback. */
export function selectAdminProfile(
  businessType: string | undefined,
  profiles: readonly AdminProfile[],
  businessTypes: Readonly<Record<string, string>>,
): AdminProfile | null {
  if (!businessType?.trim()) return null;
  const id = Object.hasOwn(businessTypes, businessType) ? businessTypes[businessType] : 'general';
  const profile = profiles.find((candidate) => candidate.id === id);
  if (!profile) throw new Error(`Missing admin profile: ${id}`);
  return profile;
}
