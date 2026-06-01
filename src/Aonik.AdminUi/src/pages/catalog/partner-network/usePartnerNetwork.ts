// Partner Network hub — shared data layer.
//
// The hub upgrades /catalog/partners into a 6-tab operator surface for
// Spec 031 ("partners (B2B / cross-border money plumbing)"). It is wired to
// the real /admin/partners endpoint via partnerService. Where no aggregate
// endpoint exists yet (cross-partner Activity, Routing, webhook Updates), the
// tabs source real per-partner detail or show an honest "awaiting backend"
// state rather than inventing telemetry — matching the precedent already set
// in CatalogPartnersPage.

import { useCallback, useEffect, useRef, useState } from 'react';
import type { PillTone } from '@/components/layout/aonik';
import { partnerService } from '@/services/partnerService';
import type { PartnerDetail, PartnerListItem } from '@/types/partners';

// The list endpoint caps page size at 100 (CommonValidationRules.PageSize).
// The hub loads a single page and aggregates client-side; if a tenant ever
// exceeds this, the toolbar surfaces a "showing first N of M" note.
export const PARTNER_LOAD_CAP = 100;

// Routing/Activity have no cross-partner endpoint, so we fan out to
// partnerService.get() for the loaded partners. Bound the fan-out to keep the
// tab responsive; truncation is surfaced honestly in the UI.
export const DETAIL_FETCH_CAP = 25;

export function extractMessage(err: unknown): string {
  if (err && typeof err === 'object' && 'userMessage' in err) {
    return String((err as { userMessage?: string }).userMessage ?? '');
  }
  return err instanceof Error ? err.message : '';
}

// ─── Tone maps ─────────────────────────────────────────────────────────────

const PARTNER_STATUS_TONE: Record<string, PillTone> = {
  Active: 'success',
  Healthy: 'success',
  Pending: 'warning',
  Degraded: 'warning',
  Suspended: 'danger',
  Incident: 'danger',
  Inactive: 'muted',
};

export function partnerStatusTone(status: string): PillTone {
  return PARTNER_STATUS_TONE[status] ?? 'default';
}

const SUCCESS_HINTS = ['succeed', 'success', 'settled', 'complete', 'paid', 'delivered', 'reversed'];
const FAIL_HINTS = ['fail', 'error', 'declin', 'reject', 'cancel'];
const PENDING_HINTS = ['pending', 'process', 'queue', 'retry', 'submit', 'await', 'progress'];

// Normalised tone for the freeform Transmission.Status string. The entity has
// no enum yet (gap X3 in the Spec 031 entity-model analysis), so we classify
// by keyword rather than an exhaustive switch.
export function transmissionTone(status: string | null | undefined): PillTone {
  const s = (status ?? '').toLowerCase();
  if (!s) return 'default';
  if (FAIL_HINTS.some((h) => s.includes(h))) return 'danger';
  if (SUCCESS_HINTS.some((h) => s.includes(h))) return 'success';
  if (PENDING_HINTS.some((h) => s.includes(h))) return 'warning';
  return 'default';
}

// ─── Formatters ────────────────────────────────────────────────────────────

export function formatDate(value?: string | null): string {
  if (!value) return '—';
  return new Date(value).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

export function formatDateTime(value?: string | null): string {
  if (!value) return '—';
  return new Date(value).toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function formatRelative(value?: string | null): string {
  if (!value) return '—';
  const diff = Date.now() - new Date(value).getTime();
  if (Number.isNaN(diff)) return '—';
  const minutes = Math.round(diff / 60_000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.round(hours / 24);
  if (days < 30) return `${days}d ago`;
  const months = Math.round(days / 30);
  if (months < 12) return `${months}mo ago`;
  return `${Math.round(months / 12)}y ago`;
}

// ─── Primary list hook ─────────────────────────────────────────────────────

export interface PartnerNetworkData {
  partners: PartnerListItem[];
  totalCount: number;
  loading: boolean;
  error: string | null;
  reload: () => void;
}

export function usePartnerNetwork(): PartnerNetworkData {
  const [partners, setPartners] = useState<PartnerListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const requestIdRef = useRef(0);

  const reload = useCallback(async () => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);
    try {
      const result = await partnerService.list({ pageNumber: 1, pageSize: PARTNER_LOAD_CAP });
      if (requestIdRef.current !== requestId) return;
      setPartners(result.items);
      setTotalCount(result.totalCount);
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) return;
      setError(extractMessage(err) || 'Failed to load partners.');
    } finally {
      if (requestIdRef.current === requestId) setLoading(false);
    }
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

  return { partners, totalCount, loading, error, reload };
}

// ─── Per-partner detail aggregation (Routing / Activity) ───────────────────

export interface PartnerDetailsState {
  details: PartnerDetail[];
  loading: boolean;
  error: string | null;
  /** True when more partners exist than were fetched (DETAIL_FETCH_CAP). */
  truncated: boolean;
}

/**
 * Lazily fan out to partnerService.get() for the loaded partners and collect
 * their details. Only runs while `enabled` is true (i.e. the consuming tab is
 * mounted) so the Routing/Activity fan-out is never paid on tabs that don't
 * need it. Failed individual fetches are dropped rather than failing the set.
 */
export function usePartnerDetails(
  partners: PartnerListItem[],
  enabled: boolean,
): PartnerDetailsState {
  const [details, setDetails] = useState<PartnerDetail[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [truncated, setTruncated] = useState(false);
  const requestIdRef = useRef(0);

  useEffect(() => {
    if (!enabled) return;
    if (partners.length === 0) {
      setDetails([]);
      setTruncated(false);
      return;
    }

    const requestId = ++requestIdRef.current;
    const slice = partners.slice(0, DETAIL_FETCH_CAP);
    setLoading(true);
    setError(null);

    void Promise.allSettled(slice.map((p) => partnerService.get(p.partnerId)))
      .then((settled) => {
        if (requestIdRef.current !== requestId) return;
        const ok = settled
          .filter((s): s is PromiseFulfilledResult<PartnerDetail> => s.status === 'fulfilled')
          .map((s) => s.value);
        setDetails(ok);
        setTruncated(partners.length > slice.length);
        if (ok.length === 0 && slice.length > 0) {
          setError('Could not load partner detail.');
        }
      })
      .finally(() => {
        if (requestIdRef.current === requestId) setLoading(false);
      });
  }, [partners, enabled]);

  return { details, loading, error, truncated };
}
