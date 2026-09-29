import type { ReactNode } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { useAuth } from '@/auth';
import { useModules } from './useModules';
import { invalidateModuleManifest } from './manifestCache';
import { findScreenRoute } from './profileCatalog';
import { ScreenLabelContext } from './ScreenLabelContext';

export function AdminUnavailablePage({ loading = false, unavailable = false }: { loading?: boolean; unavailable?: boolean }) {
  const { logout } = useAuth();
  const { landingPath } = useModules();
  return <div className="flex min-h-[60vh] flex-col items-center justify-center gap-4 p-8 text-center" role="status">
    <h1 className="text-xl font-semibold">{loading ? 'Loading your workspace…' : unavailable ? 'Your workspace could not be loaded' : 'This page is not available'}</h1>
    {!loading && <>
      <p className="text-sm text-muted-foreground">{unavailable ? 'Please retry to load your organisation’s screens.' : 'Choose an available page or contact your administrator.'}</p>
      {unavailable ? <button className="underline" onClick={invalidateModuleManifest}>Retry</button>
        : landingPath && <Link className="underline" to={landingPath}>Go to your workspace</Link>}
      <button className="underline" onClick={() => void logout()}>Sign out</button>
    </>}
  </div>;
}

/** Registered but disallowed routes remain matched so a dynamic route cannot swallow them. */
export function AdminScreenGate({ children }: { children: ReactNode }) {
  const { pathname } = useLocation();
  const { modules, profile, isPathVisible } = useModules();
  if (!isPathVisible(pathname)) return <AdminUnavailablePage />;
  const definition = findScreenRoute(modules, pathname)?.screen;
  const label = definition && profile?.labels.get(definition.id);
  return <ScreenLabelContext.Provider value={label}>{children}</ScreenLabelContext.Provider>;
}
