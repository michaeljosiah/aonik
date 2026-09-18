import { useModules } from './useModules';

/**
 * Whether a backend module (e.g. "finance", "voice") is enabled for the
 * selected tenant. Fail-open: with no manifest every module reads as
 * enabled, matching the sidebar and the router.
 */
export function useModuleEnabled(moduleId: string): boolean {
  const { isModuleEnabled } = useModules();
  return isModuleEnabled(moduleId);
}
