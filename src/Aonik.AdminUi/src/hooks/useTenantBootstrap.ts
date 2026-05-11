import { useEffect, useState, useCallback } from 'react';

import { tenantService } from '@/services/tenantService';
import { getSelectedTenant, setSelectedTenant } from '@/lib/tenantContext';
import type { MyTenantSummary } from '@/types';

/**
 * Resolution state for the post-auth org-discovery step.
 *
 * State machine:
 *   loading  → /host/me/tenants in flight (or not started yet).
 *   ready    → caller can render protected routes. A tenant is persisted
 *              in localStorage; it was either auto-selected (single tenant)
 *              or a previously-cached choice still valid in /me/tenants.
 *   picker   → caller must route to the org picker (multiple memberships,
 *              no valid cached selection).
 *   none     → identity has no active tenant memberships. Caller should
 *              surface a "contact your administrator" message.
 *   error    → /host/me/tenants failed in a non-2xx way. Caller can retry.
 */
export type TenantBootstrapState =
  | { kind: 'loading' }
  | { kind: 'ready'; tenants: MyTenantSummary[]; selectedTenantId: string }
  | { kind: 'picker'; tenants: MyTenantSummary[] }
  | { kind: 'none' }
  | { kind: 'error'; message: string };

/**
 * Module-level cache so multiple components (the gate and the picker page)
 * issue a single network call between them. Invalidated on logout via
 * {@link invalidateTenantBootstrap}.
 */
let cachedTenants: MyTenantSummary[] | null = null;
let inFlight: Promise<MyTenantSummary[]> | null = null;

async function fetchMyTenants(forceRefresh: boolean): Promise<MyTenantSummary[]> {
  if (!forceRefresh && cachedTenants) return cachedTenants;
  if (inFlight) return inFlight;

  inFlight = tenantService
    .listMyTenants()
    .then((response) => {
      cachedTenants = response.tenants;
      return cachedTenants;
    })
    .finally(() => {
      inFlight = null;
    });

  return inFlight;
}

/** Clear the cache (call from logout / on auth state changes). */
export function invalidateTenantBootstrap(): void {
  cachedTenants = null;
  inFlight = null;
}

/**
 * Resolves which tenant the authenticated user should land in.
 *
 * Behaviour:
 *  - Calls {@link tenantService.listMyTenants} once (single-flight cached).
 *  - 0 memberships → `none`.
 *  - 1 membership → auto-select, persist via `setSelectedTenant`, return
 *    `ready`.
 *  - 2+ memberships with a valid cached selection in localStorage → keep
 *    that selection, return `ready`.
 *  - 2+ memberships, no valid cache → return `picker`. Caller should
 *    redirect to the org picker route.
 *
 * The caller is responsible for gating this on the auth state — the hook
 * starts fetching as soon as `enabled` is true.
 */
export function useTenantBootstrap(enabled: boolean): {
  state: TenantBootstrapState;
  refetch: () => void;
} {
  const [state, setState] = useState<TenantBootstrapState>({ kind: 'loading' });
  const [refreshKey, setRefreshKey] = useState(0);

  const refetch = useCallback(() => {
    invalidateTenantBootstrap();
    setRefreshKey((k) => k + 1);
  }, []);

  useEffect(() => {
    if (!enabled) return;
    let cancelled = false;
    setState({ kind: 'loading' });

    (async () => {
      try {
        const tenants = await fetchMyTenants(false);
        if (cancelled) return;

        if (tenants.length === 0) {
          setState({ kind: 'none' });
          return;
        }

        if (tenants.length === 1) {
          const only = tenants[0];
          setSelectedTenant({
            tenantId: only.tenantId,
            name: only.name,
            subdomain: only.subdomain,
            environment: only.environment,
          });
          setState({ kind: 'ready', tenants, selectedTenantId: only.tenantId });
          return;
        }

        // Multiple memberships — honor a still-valid cached choice so we
        // don't bounce the user through the picker on every cold start.
        const cached = getSelectedTenant();
        const cachedHit = cached?.tenantId && tenants.find((t) => t.tenantId === cached.tenantId);
        if (cachedHit) {
          setSelectedTenant({
            tenantId: cachedHit.tenantId,
            name: cachedHit.name,
            subdomain: cachedHit.subdomain,
            environment: cachedHit.environment,
          });
          setState({ kind: 'ready', tenants, selectedTenantId: cachedHit.tenantId });
          return;
        }

        setState({ kind: 'picker', tenants });
      } catch (error) {
        if (cancelled) return;
        const message =
          (error && typeof error === 'object' && 'userMessage' in error
            ? String((error as { userMessage?: string }).userMessage)
            : '') || 'Unable to load your organizations. Please try again.';
        setState({ kind: 'error', message });
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [enabled, refreshKey]);

  return { state, refetch };
}
