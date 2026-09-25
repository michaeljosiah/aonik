import { useCallback, useEffect, useMemo, useState } from 'react';

import { useAuth } from '@/auth/useAuth';
import { isPortalAdmin as resolvePortalAdmin } from '@/lib/roleUtils';
import { useModules } from '@/modules/useModules';
import { filterNavByModules } from '@/modules/enablement';
import { identityService } from '@/services/identityService';
import type { NavItem, NavigationSection } from '@/types';

import { SIDEBAR_NAV } from './sidebarNav';

/**
 * The sidebar nav as this user may see it: audience (host vs tenant) from
 * hydrated roles, runtime nav/route overrides, and module enablement
 * (Spec 097 §8). Shared by AonikSidebar and the command palette so both
 * show exactly the same destinations.
 */
export function useVisibleNav(): { sections: NavigationSection[]; isPortalAdmin: boolean } {
  const { user } = useAuth();
  const { manifest } = useModules();
  const [navRoles, setNavRoles] = useState<string[]>([]);
  const [isLoadingNavRoles, setIsLoadingNavRoles] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const hydrate = async () => {
      if (!user) {
        setNavRoles([]);
        return;
      }
      if (user.roleSource !== 'api' && user.roles && user.roles.length > 0) {
        setNavRoles(user.roles);
        return;
      }
      setIsLoadingNavRoles(true);
      try {
        const info = await identityService.getUserInfo();
        if (!cancelled) setNavRoles(info.roles);
      } catch {
        if (!cancelled) setNavRoles([]);
      } finally {
        if (!cancelled) setIsLoadingNavRoles(false);
      }
    };
    void hydrate();
    return () => {
      cancelled = true;
    };
  }, [user]);

  const isPortalAdmin = resolvePortalAdmin(navRoles);
  const disabledNavIds = useMemo(() => new Set(manifest?.disabledNavItems ?? []), [manifest]);
  const disabledRoutes = useMemo(() => new Set(manifest?.disabledRoutes ?? []), [manifest]);
  // null = no manifest (fail-open, render everything); otherwise only items
  // whose moduleId is enabled render. Rule lives in modules/enablement.ts.
  const enabledModules = useMemo(() => (manifest ? new Set(manifest.enabledModules) : null), [manifest]);

  const isItemVisible = useCallback(
    (it: NavItem) => {
      if (disabledNavIds.has(it.id)) return false;
      if (it.href && disabledRoutes.has(it.href)) return false;
      if (it.audience === 'host') return isPortalAdmin;
      if (it.audience === 'tenant') return !isPortalAdmin && !isLoadingNavRoles;
      return true;
    },
    [disabledNavIds, disabledRoutes, isPortalAdmin, isLoadingNavRoles],
  );

  const sections = useMemo(() => {
    const audienceSections = SIDEBAR_NAV.filter((s: NavigationSection) => {
      if (s.audience === 'host') return isPortalAdmin;
      if (s.audience === 'tenant') return !isPortalAdmin && !isLoadingNavRoles;
      return true;
    });
    return filterNavByModules(audienceSections, enabledModules, { isItemVisible });
  }, [enabledModules, isItemVisible, isPortalAdmin, isLoadingNavRoles]);

  return { sections, isPortalAdmin };
}
