// Product editor — Storefront (Spec 082 §2, the PR #268 design).
//
// The deep-surface status rows describe THIS product and nothing else: they are fetched per
// opened product, keyed to it, and a response that arrives after the operator has switched
// products is discarded. That leakage — one product showing another's personalisation or
// content state — was the round-1 review finding that shaped this tab.

import { useCallback, useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';

import { Card as AonikCard, Pill } from '@/components/layout/aonik';
import { commerceCatalogService } from '@/services/commerceCatalogService';
import { commerceContentService } from '@/services/commerceContentService';
import { commerceStorefrontService } from '@/services/commerceStorefrontService';
import type { AdminProductDetailDto } from '@/types/commerce';

import type { ProductEditorForm } from '../../lib/productForm';
import { ChipEditor, Field, inputClass } from './DetailsTab';

/** A deep-surface row is loading, unavailable, or has an answer — never silently blank. */
type SurfaceState =
  | { kind: 'loading' }
  | { kind: 'unavailable' }
  | { kind: 'ready'; summary: string; tone: 'info' | 'muted' | 'warning' };

interface StorefrontTabProps {
  product: AdminProductDetailDto;
  form: ProductEditorForm;
  onChange: (patch: Partial<ProductEditorForm>) => void;
  /** Surcharge is its own endpoint, not part of the product PATCH. */
  surchargeAmount: string;
  surchargeCurrency: string;
  onSurchargeChange: (next: { amount?: string; currency?: string }) => void;
}

export function StorefrontTab({
  product,
  form,
  onChange,
  surchargeAmount,
  surchargeCurrency,
  onSurchargeChange,
}: StorefrontTabProps) {
  const isBundle = product.kind === 'Bundle';
  const productId = product.id;

  const [personalisation, setPersonalisation] = useState<SurfaceState>({ kind: 'loading' });
  const [content, setContent] = useState<SurfaceState>({ kind: 'loading' });
  // "Not a bundle" is a fact about the product, not a fetch result — derived at mount so no
  // effect has to write it.
  const [sizePlan, setSizePlan] = useState<SurfaceState>(() =>
    isBundle ? { kind: 'loading' } : { kind: 'ready', summary: 'Not a bundle', tone: 'muted' },
  );

  // Keyed to the product: a late response for a product the operator has navigated away
  // from must never paint over the current one's status.
  const activeProductRef = useRef(productId);

  const loadSurfaces = useCallback(() => {
    activeProductRef.current = productId;
    const isStale = () => activeProductRef.current !== productId;

    // Three independent calls — one failing must degrade its own row, not the tab.
    commerceCatalogService
      .getProductNarrowing(productId)
      .then((groups) => {
        if (isStale()) return;
        setPersonalisation(
          groups.length === 0
            ? { kind: 'ready', summary: 'Not personalisable', tone: 'muted' }
            : {
                // "configured", not "offered": these are the stored narrowing rows. A group
                // that has since been deactivated still has a row here but is dropped from
                // the effective composition the storefront shows, so claiming it is offered
                // would overstate what a shopper actually sees.
                kind: 'ready',
                summary: `${groups.length} group${groups.length === 1 ? '' : 's'} configured`,
                tone: 'info',
              },
        );
      })
      .catch(() => !isStale() && setPersonalisation({ kind: 'unavailable' }));

    commerceContentService
      .getAdminContent(productId)
      .then((admin) => {
        if (isStale()) return;
        if (!admin.block) {
          setContent({ kind: 'ready', summary: 'Not authored', tone: 'muted' });
          return;
        }
        const variants = admin.variants.length;
        setContent({
          kind: 'ready',
          summary: `Authored · ${variants} variant${variants === 1 ? '' : 's'}${
            admin.isStale ? ' · needs review' : ''
          }`,
          tone: admin.isStale ? 'warning' : 'info',
        });
      })
      .catch(() => !isStale() && setContent({ kind: 'unavailable' }));

    if (isBundle) {
      commerceStorefrontService
        .getSizePlan(productId)
        .then((plan) => {
          if (isStale()) return;
          setSizePlan({
            kind: 'ready',
            summary: `${plan.minSize}–${plan.maxSize} · ${plan.presets.length} preset${
              plan.presets.length === 1 ? '' : 's'
            }`,
            tone: 'info',
          });
        })
        // ONLY a 404 means "no plan authored" — the endpoint says so by absence. A 403,
        // timeout or 500 must degrade to unavailable instead, or an outage would be reported
        // to the operator as an authoring gap they need to go and fix.
        .catch((err: unknown) => {
          if (isStale()) return;
          setSizePlan(
            httpStatus(err) === 404
              ? { kind: 'ready', summary: 'No plan authored', tone: 'warning' }
              : { kind: 'unavailable' },
          );
        });
    }
  }, [productId, isBundle]);

  useEffect(() => {
    loadSurfaces();
  }, [loadSurfaces]);

  const attributes = parseAttributes(form.attributesJson);

  return (
    <div className="flex flex-col gap-4">
      <Field label="Search keywords">
        <ChipEditor
          values={form.searchKeywords}
          placeholder="Add a keyword"
          onChange={(searchKeywords) => onChange({ searchKeywords })}
        />
        <p className="mt-1 text-[11px] text-[var(--color-text-tertiary)]">
          Matched by storefront search — never serialized publicly; admin-eyes-only by API design.
        </p>
      </Field>

      <AonikCard title="Storefront attributes" padding={12}>
        {attributes === null ? (
          <p className="text-[12px] text-[var(--color-error)]">
            Attributes JSON is invalid — fix it on the Details tab.
          </p>
        ) : attributes.length === 0 ? (
          <p className="text-[12px] text-[var(--color-text-secondary)]">
            No storefront attributes authored — matches no attribute facet.
          </p>
        ) : (
          <dl className="grid grid-cols-2 gap-x-6 gap-y-1.5">
            {attributes.map(([path, value]) => (
              <div key={path} className="flex items-baseline justify-between gap-3">
                <dt className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">
                  {path}
                </dt>
                <dd className="text-[12px] text-[var(--color-text-primary)]">{value}</dd>
              </div>
            ))}
          </dl>
        )}
      </AonikCard>

      <AonikCard title="Unit surcharge" padding={12}>
        <div className="flex gap-3">
          <Field label="Amount" className="flex-1">
            <input
              value={surchargeAmount}
              onChange={(e) => onSurchargeChange({ amount: e.target.value })}
              inputMode="decimal"
              placeholder="None"
              className={inputClass}
            />
          </Field>
          <Field label="Currency" className="w-[140px]">
            <input
              value={surchargeCurrency}
              onChange={(e) => onSurchargeChange({ currency: e.target.value.toUpperCase() })}
              maxLength={3}
              className={`${inputClass} font-[family-name:var(--font-mono)]`}
            />
          </Field>
        </div>
        <p className="mt-1.5 text-[11px] text-[var(--color-text-tertiary)]">
          The one price-like field a product card may show. An amount requires a currency — the
          server rejects the pair otherwise, so a stored amount can never be re-denominated by
          accident. Clear the amount to remove the surcharge.
        </p>
      </AonikCard>

      <AonikCard title="Deep surfaces" subtitle="The state of this product on the storefront authoring pages" padding={12}>
        <div className="flex flex-col divide-y divide-[var(--color-border-light)]">
          <SurfaceRow label="Personalisation" state={personalisation} to="/commerce/personalisation" />
          <SurfaceRow label="Product content" state={content} to="/commerce/content" />
          <SurfaceRow label="Size plan" state={sizePlan} to="/commerce/box-plans" />
        </div>
      </AonikCard>
    </div>
  );
}

function SurfaceRow({ label, state, to }: { label: string; state: SurfaceState; to: string }) {
  return (
    <div className="flex items-center justify-between gap-3 py-2">
      <span className="text-[12.5px] text-[var(--color-text-primary)]">{label}</span>
      <span className="flex items-center gap-2.5">
        {state.kind === 'loading' && (
          <span className="text-[11px] text-[var(--color-text-tertiary)]">Checking…</span>
        )}
        {state.kind === 'unavailable' && (
          <span className="text-[11px] text-[var(--color-text-tertiary)]">Unavailable</span>
        )}
        {state.kind === 'ready' && (
          <Pill tone={state.tone} size="sm">
            {state.summary}
          </Pill>
        )}
        <Link to={to} className="text-[11px] text-[var(--color-brand-primary)] hover:underline">
          Open
        </Link>
      </span>
    </div>
  );
}

/** The HTTP status of a rejected api call, or undefined for a transport-level failure. */
function httpStatus(err: unknown): number | undefined {
  if (!err || typeof err !== 'object' || !('response' in err)) return undefined;
  const response = (err as { response?: { status?: number } }).response;
  return typeof response?.status === 'number' ? response.status : undefined;
}

/** Flattened `path → value` pairs; null when the JSON is not a usable object. */
function parseAttributes(json: string): Array<[string, string]> | null {
  const trimmed = json.trim();
  if (!trimmed) return [];
  let parsed: unknown;
  try {
    parsed = JSON.parse(trimmed);
  } catch {
    return null;
  }
  if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) return null;

  const rows: Array<[string, string]> = [];
  const walk = (value: Record<string, unknown>, prefix: string) => {
    for (const [key, entry] of Object.entries(value)) {
      const path = prefix ? `${prefix}.${key}` : key;
      // Nested objects are walked because facet source paths are dot-separated
      // (e.g. "nutrition.kcal") — showing "[object Object]" would hide the real path.
      if (entry !== null && typeof entry === 'object' && !Array.isArray(entry)) {
        walk(entry as Record<string, unknown>, path);
      } else {
        rows.push([path, Array.isArray(entry) ? entry.join(', ') : String(entry)]);
      }
    }
  };
  walk(parsed as Record<string, unknown>, '');
  return rows;
}
