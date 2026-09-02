import { useEffect, useState } from 'react';

import { useAuth } from '@/auth/useAuth';
import { isPortalAdmin } from '@/lib/roleUtils';
import { identityService } from '@/services/identityService';

/**
 * Whether the signed-in user is a host (platform) administrator.
 *
 * Mirrors the sidebar's role hydration: claims-sourced roles are trusted
 * as-is, otherwise roles are fetched once from the identity service.
 */
export function useIsHostAdmin(): { isHostAdmin: boolean; loading: boolean } {
  const { user } = useAuth();
  const [roles, setRoles] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;

    const hydrate = async () => {
      if (!user) {
        setRoles([]);
        return;
      }
      if (user.roleSource !== 'api' && user.roles && user.roles.length > 0) {
        setRoles(user.roles);
        return;
      }
      setLoading(true);
      try {
        const info = await identityService.getUserInfo();
        if (!cancelled) setRoles(info.roles);
      } catch {
        if (!cancelled) setRoles([]);
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    void hydrate();
    return () => {
      cancelled = true;
    };
  }, [user]);

  return { isHostAdmin: isPortalAdmin(roles), loading };
}
