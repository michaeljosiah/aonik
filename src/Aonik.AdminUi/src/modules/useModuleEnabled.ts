import { useModules } from './useModules';

/**
 * Whether a backend module (e.g. "finance", "voice") is enabled for the
 * selected tenant. Without a current manifest modules are unavailable.
 */
export function useModuleEnabled(moduleId: string): boolean {
  const { isModuleEnabled } = useModules();
  return isModuleEnabled(moduleId);
}
