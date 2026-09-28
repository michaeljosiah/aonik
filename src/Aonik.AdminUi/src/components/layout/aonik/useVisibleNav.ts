import { useModules } from '@/modules/useModules';
import type { NavigationSection } from '@/types';

/** The same profile-filtered destinations power the sidebar and command palette. */
export function useVisibleNav(): { sections: NavigationSection[]; isPortalAdmin: boolean } {
  const { navigation, allowsPolicy } = useModules();
  return { sections: navigation, isPortalAdmin: allowsPolicy('PlatformAdmin') };
}
