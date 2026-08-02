// Box plans (Spec 076) — the container pricing behind the storefront's size step.
//
// Two rules decide everything this page shows, and the design exists to make them legible
// rather than to be believed:
//
//   PRESETS WIN AT THEIR SIZE. Every other size prices as base + (size − baseSize) × perSpace.
//   The formula is not a floor or a cap, so the dashed line can pass either side of a dot.
//
//   GROWING A BOX CHARGES effective(target) − effective(current). Never perSpace × spaces —
//   they differ wherever a preset sits between the two sizes, which is exactly where the
//   intuition fails. The marginal strip shows the subtraction rather than asserting the rule.
//
// The whole page renders from a DRAFT, not from the saved plan: the curve, the KPIs and the
// marginal tiles all move as fields are typed, so an operator sees the pricing consequence
// before committing to it. Save is a full replace of formula plus presets.
//
// Savings are AUTHORED display values. The page shows a formula-at-size column purely for
// comparison and never lets that comparison become a saving in the payload — the storefront
// would then advertise a discount nobody agreed to.

import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { AlertCircle, Plus, RefreshCw, Trash2 } from 'lucide-react';
import { toast } from 'sonner';

import { Card as AonikCard, KpiTile, PageHeader, Pill } from '@/components/layout/aonik';
import { Button } from '@/components/ui/button';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { commerceCatalogService } from '@/services/commerceCatalogService';
import { commerceStorefrontService } from '@/services/commerceStorefrontService';
import type { BoxPlanDto, BoxPlanPresetDto, ProductSummaryDto } from '@/types/commerce';

import { PriceCurve } from './components/PriceCurve';
import { formatUnsignedAmount } from './components/signedAmountFormat';
import {
  BADGE_MAX,
  BLURB_MAX,
  curveModel,
  draftFromPlan,
  effectivePrice,
  emptyDraft,
  formulaPrice,
  isDirty,
  marginalJumps,
  validatePlan,
  type PlanDraft,
} from './lib/planCurve';

const inputClass =
  'w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-2.5 py-1.5 text-[13px] text-[var(--color-text-primary)] outline-none focus:border-[var(--color-brand-primary)]';
const numberClass = `${inputClass} font-[family-name:var(--font-mono)]`;

export function BoxPlansPage() {
  const [bundles, setBundles] = useState<ProductSummaryDto[]>([]);
  const [bundlesError, setBundlesError] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [tenantCurrency, setTenantCurrency] = useState<string | null>(null);

  const [saved, setSaved] = useState<BoxPlanDto | null>(null);
  const [draft, setDraft] = useState<PlanDraft | null>(null);
  /** The bundle has no plan at all — an authoring state, not a failure. */
  const [unauthored, setUnauthored] = useState(false);
  const [planError, setPlanError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [planLoading, setPlanLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      // The tenant's canonical currency seeds a NEW plan. Read alongside the bundle list so a
      // config failure costs the default rather than the page.
      const [list, currency] = await Promise.all([
        // Every bundle, DRAFT INCLUDED — see `loadPlan`. The spec's v1 mitigation (active
        // bundles only) existed because the admin read did not, and it does.
        commerceCatalogService.listProducts({ kind: 'Bundle', pageSize: 100 }).then(
          (r) => ({ ok: true as const, items: r.items }),
          () => ({ ok: false as const, items: [] as ProductSummaryDto[] }),
        ),
        // The PUBLIC config read — the canonical tenant currency, which is what a new plan
        // should be denominated in. Only used to seed a blank form; an existing plan always
        // keeps its own code.
        commerceStorefrontService.getPublicStorefrontConfig().then(
          (config) => config.currency,
          () => null,
        ),
      ]);
      if (cancelled) return;
      setBundles(list.items);
      setBundlesError(!list.ok);
      setTenantCurrency(currency);
      setSelectedId((current) => current ?? list.items[0]?.id ?? null);
      setLoading(false);
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const loadPlan = useCallback(async (productId: string) => {
    setPlanLoading(true);
    setPlanError(null);
    setError(null);
    try {
      // The ADMIN read by product id, which is status-agnostic. The public box-plan route 404s
      // for a draft bundle before it looks for a plan, so reading that would make a hidden
      // existing plan indistinguishable from a missing one — and the save is a full replace, so
      // the page would overwrite it blind.
      const plan = await commerceStorefrontService.getSizePlan(productId);
      setSaved(plan);
      setDraft(draftFromPlan(plan));
      setUnauthored(false);
    } catch (err: unknown) {
      if (httpStatus(err) === 404) {
        // A 404 from THIS route means no plan authored, whatever the product's status.
        setSaved(null);
        setDraft(null);
        setUnauthored(true);
        return;
      }
      setSaved(null);
      setDraft(null);
      setUnauthored(false);
      setPlanError(readMessage(err) || 'This bundle’s size plan could not be read.');
    } finally {
      setPlanLoading(false);
    }
  }, []);

  useEffect(() => {
    if (selectedId) void loadPlan(selectedId);
  }, [selectedId, loadPlan]);

  const selectedBundle = bundles.find((b) => b.id === selectedId) ?? null;
  const model = useMemo(() => (draft ? curveModel(draft) : null), [draft]);
  const jumps = useMemo(() => (draft ? marginalJumps(draft) : []), [draft]);
  const invalid = draft ? validatePlan(draft) : null;
  const dirty = draft ? isDirty(draft, saved) : false;

  if (loading) return <PageLoadingScreen message="Loading box plans" />;

  const money = (amount: number) => formatUnsignedAmount(amount, draft?.currency ?? 'GBP');

  return (
    <div className="flex flex-col gap-5 p-6">
      <PageHeader
        eyebrow="Commerce"
        title="Box plans"
        subtitle="Presets win at their size; every other size prices as base + (size − base) × per-space. Growing a box charges the difference between the two box prices, never per-space × spaces."
        actions={
          <div className="flex items-center gap-2">
            <select
              value={selectedId ?? ''}
              onChange={(e) => setSelectedId(e.target.value || null)}
              className={`${inputClass} w-[240px]`}
              aria-label="Bundle"
            >
              {bundles.length === 0 && <option value="">No bundles</option>}
              {bundles.map((bundle) => (
                <option key={bundle.id} value={bundle.id}>
                  {bundle.name}
                  {bundle.status === 'Active' ? '' : ` (${bundle.status})`}
                </option>
              ))}
            </select>
            <Button
              variant="outline"
              size="sm"
              onClick={() => selectedId && void loadPlan(selectedId)}
              disabled={!selectedId || planLoading}
            >
              <RefreshCw className={`mr-1 h-3.5 w-3.5 ${planLoading ? 'animate-spin' : ''}`} />
              Reload
            </Button>
            <Button onClick={() => void save()} disabled={!draft || saving || !!invalid || !dirty}>
              {saving ? 'Saving…' : dirty ? 'Save plan' : 'Saved'}
            </Button>
          </div>
        }
      />

      {bundlesError && (
        <Banner>
          The bundle list could not be read, so this page may be missing bundles. Nothing here is
          proof that a bundle has no plan.
        </Banner>
      )}

      {bundles.length === 0 && !bundlesError && (
        <AonikCard>
          <p className="py-6 text-center text-[13px] text-[var(--color-text-secondary)]">
            No bundle products exist yet. A box plan prices a bundle, so create one in Products
            first.
          </p>
        </AonikCard>
      )}

      {planError && (
        <Banner>
          <span className="flex-1">{planError}</span>
          <button
            type="button"
            onClick={() => selectedId && void loadPlan(selectedId)}
            className="shrink-0 underline"
          >
            Retry
          </button>
        </Banner>
      )}

      {error && <Banner>{error}</Banner>}

      {invalid && draft && (
        <div className="flex items-start gap-2 rounded border border-[var(--color-warning)] bg-[var(--color-warning-light)] px-3 py-2 text-xs text-[var(--color-text-secondary)]">
          <AlertCircle className="mt-px h-4 w-4 shrink-0 text-[var(--color-warning)]" aria-hidden />
          <span>{invalid}</span>
        </div>
      )}

      {unauthored && selectedBundle && (
        <AonikCard className="border-dashed">
          <div className="flex flex-col items-center gap-3 py-8 text-center">
            <p className="text-[13px] text-[var(--color-text-secondary)]">
              No size plan authored for this bundle — the storefront shows no box section until
              one exists.
            </p>
            <Button
              onClick={() => setDraft(emptyDraft(selectedBundle.id, tenantCurrency ?? 'GBP'))}
            >
              Author a size plan
            </Button>
            {!tenantCurrency && (
              <p className="text-[11px] text-[var(--color-text-tertiary)]">
                The storefront currency could not be read, so the form starts at GBP — check it
                before saving.
              </p>
            )}
          </div>
        </AonikCard>
      )}

      {draft && (
        <>
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
            <KpiTile
              label="Size range"
              value={`${draft.minSize}–${draft.maxSize}`}
              delta="spaces"
              deltaTone="neutral"
            />
            <KpiTile
              label={`Base price at ${draft.baseSize}`}
              value={money(draft.basePrice)}
              delta="the formula anchor"
              deltaTone="neutral"
            />
            <KpiTile
              label="Per space"
              value={money(draft.perSpacePrice)}
              delta="beyond the base size"
              deltaTone="neutral"
            />
            <KpiTile
              label="Presets"
              value={draft.presets.length.toLocaleString()}
              delta={draft.presets.length === 0 ? 'formula only' : 'override their size'}
              deltaTone="neutral"
            />
          </div>

          <AonikCard
            title="Price by size"
            subtitle="Dashed: the formula alone. Solid: what is charged. Dots: presets, annotated where a saving was authored."
          >
            {model && model.sizes.length > 0 ? (
              <>
                <PriceCurve
                  min={draft.minSize}
                  max={draft.maxSize}
                  formula={(size) => formulaPrice(draft, size)}
                  effective={(size) => effectivePrice(draft, size)}
                  presets={model.presetMarkers}
                  currency={draft.currency}
                />
                <div className="mt-4 grid grid-cols-1 gap-2 sm:grid-cols-3">
                  {jumps.map((jump) => (
                    <div
                      key={`${jump.from}-${jump.to}`}
                      className="rounded-md border border-[var(--color-border-light)] px-3 py-2"
                    >
                      <p className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                        Grow {jump.from} → {jump.to}
                      </p>
                      <p className="mt-0.5 font-[family-name:var(--font-mono)] text-[15px] text-[var(--color-text-primary)]">
                        {money(jump.delta)}
                      </p>
                      {/* The subtraction, shown. The rule is only believable if the arithmetic
                          is visible — asserting it in prose is what lets per-space × spaces
                          survive in someone's head. */}
                      <p className="mt-0.5 font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">
                        {money(jump.toPrice)} − {money(jump.fromPrice)}
                      </p>
                      <p className="text-[11px] text-[var(--color-text-tertiary)]">{jump.note}</p>
                    </div>
                  ))}
                  {jumps.length === 0 && (
                    <p className="text-[12px] text-[var(--color-text-secondary)]">
                      This plan sells one size, so there is no growing to price.
                    </p>
                  )}
                </div>
              </>
            ) : (
              <p className="py-6 text-center text-[12.5px] text-[var(--color-text-secondary)]">
                Set a size range to see the price curve.
              </p>
            )}
          </AonikCard>

          <AonikCard title="Formula" subtitle="Applies at every size without a preset.">
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
              <NumberField
                label="Smallest"
                value={draft.minSize}
                onChange={(v) => setDraft({ ...draft, minSize: v })}
                integer
              />
              <NumberField
                label="Largest"
                value={draft.maxSize}
                onChange={(v) => setDraft({ ...draft, maxSize: v })}
                integer
              />
              <NumberField
                label="Base size"
                value={draft.baseSize}
                onChange={(v) => setDraft({ ...draft, baseSize: v })}
                integer
              />
              <NumberField
                label="Base price"
                value={draft.basePrice}
                onChange={(v) => setDraft({ ...draft, basePrice: v })}
              />
              <NumberField
                label="Per space"
                value={draft.perSpacePrice}
                onChange={(v) => setDraft({ ...draft, perSpacePrice: v })}
              />
              <label className="flex flex-col gap-1">
                <span className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                  Currency
                </span>
                <input
                  value={draft.currency}
                  onChange={(e) =>
                    setDraft({ ...draft, currency: e.target.value.toUpperCase().slice(0, 3) })
                  }
                  className={numberClass}
                />
              </label>
            </div>
            <p className="mt-2 text-[11px] text-[var(--color-text-tertiary)]">
              Saving is a full replace of the formula and every preset. Changing the currency is
              refused while open box sessions reference this plan — the server counts them.
            </p>
          </AonikCard>

          <AonikCard
            title="Presets"
            subtitle="A preset overrides the formula at its exact size."
            padding={0}
            action={
              <Button variant="outline" size="sm" onClick={addPreset}>
                <Plus className="mr-1 h-3.5 w-3.5" /> Add a preset
              </Button>
            }
          >
            <div className="overflow-x-auto">
              <table className="w-full min-w-[820px] text-[12.5px]">
                <thead>
                  <tr className="border-b border-[var(--color-border-light)] text-left text-[10px] uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                    <Th>Size</Th>
                    <Th>Price</Th>
                    <Th>Formula at size</Th>
                    <Th>Saving (authored)</Th>
                    <Th>Badge</Th>
                    <Th>Blurb</Th>
                    <Th> </Th>
                  </tr>
                </thead>
                <tbody>
                  {draft.presets.length === 0 && (
                    <tr>
                      <td
                        colSpan={7}
                        className="px-4 py-6 text-center text-[var(--color-text-secondary)]"
                      >
                        No presets — every size prices from the formula.
                      </td>
                    </tr>
                  )}
                  {draft.presets.map((preset, index) => (
                    <tr key={index} className="border-b border-[var(--color-border-light)]">
                      <Td>
                        <input
                          type="number"
                          value={preset.size}
                          onChange={(e) => patchPreset(index, { size: toInt(e.target.value) })}
                          className={`${numberClass} w-[72px]`}
                          aria-label="Preset size"
                        />
                      </Td>
                      <Td>
                        <input
                          type="number"
                          step="0.01"
                          value={preset.price}
                          onChange={(e) => patchPreset(index, { price: toNumber(e.target.value) })}
                          className={`${numberClass} w-[100px]`}
                          aria-label="Preset price"
                        />
                      </Td>
                      {/* COMPARISON ONLY. Never written into the payload — see the footer. */}
                      <Td className="font-[family-name:var(--font-mono)] text-[var(--color-text-tertiary)]">
                        {money(formulaPrice(draft, preset.size))}
                      </Td>
                      <Td>
                        <input
                          type="number"
                          step="0.01"
                          value={preset.savingAmount ?? ''}
                          placeholder="—"
                          onChange={(e) =>
                            patchPreset(index, {
                              savingAmount: e.target.value === '' ? null : toNumber(e.target.value),
                            })
                          }
                          className={`${numberClass} w-[100px] ${
                            preset.savingAmount != null ? 'text-[var(--color-success)]' : ''
                          }`}
                          aria-label="Authored saving"
                        />
                      </Td>
                      <Td>
                        <input
                          value={preset.badge ?? ''}
                          maxLength={BADGE_MAX}
                          onChange={(e) => patchPreset(index, { badge: e.target.value || null })}
                          className={`${inputClass} w-[120px]`}
                          aria-label="Badge"
                        />
                      </Td>
                      <Td>
                        <input
                          value={preset.blurb ?? ''}
                          maxLength={BLURB_MAX}
                          onChange={(e) => patchPreset(index, { blurb: e.target.value || null })}
                          className={`${inputClass} w-[200px]`}
                          aria-label="Blurb"
                        />
                      </Td>
                      <Td>
                        <button
                          type="button"
                          aria-label={`Remove the ${preset.size}-space preset`}
                          onClick={() =>
                            setDraft({
                              ...draft,
                              presets: draft.presets.filter((_, i) => i !== index),
                            })
                          }
                          className="rounded p-1 text-[var(--color-text-tertiary)] hover:text-[var(--color-error)]"
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      </Td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <p className="border-t border-[var(--color-border-light)] px-4 py-2 text-[11px] text-[var(--color-text-tertiary)]">
              Savings are display values authored here — the storefront never computes one. The
              formula column is for comparison and is not sent.
            </p>
          </AonikCard>

          {selectedBundle && selectedBundle.status !== 'Active' && (
            <p className="flex items-center gap-2 text-[11.5px] text-[var(--color-text-tertiary)]">
              <Pill tone="muted">{selectedBundle.status}</Pill>
              This bundle is not live, so the storefront serves none of this yet. The plan is
              still authored and saved normally.
            </p>
          )}
        </>
      )}
    </div>
  );

  function addPreset() {
    if (!draft) return;
    // Seeded at the base size with the formula's OWN price: a starting point that changes
    // nothing until the operator moves it, rather than a discount nobody chose.
    const preset: BoxPlanPresetDto = {
      size: draft.baseSize,
      price: formulaPrice(draft, draft.baseSize),
      badge: null,
      blurb: null,
      savingAmount: null,
      sortOrder: draft.presets.length,
    };
    setDraft({ ...draft, presets: [...draft.presets, preset] });
  }

  function patchPreset(index: number, patch: Partial<BoxPlanPresetDto>) {
    if (!draft) return;
    setDraft({
      ...draft,
      presets: draft.presets.map((p, i) => (i === index ? { ...p, ...patch } : p)),
    });
  }

  async function save() {
    if (!draft || !selectedId) return;
    const problem = validatePlan(draft);
    if (problem) {
      setError(problem);
      return;
    }
    setSaving(true);
    setError(null);
    try {
      const result = await commerceStorefrontService.upsertSizePlan(selectedId, {
        minSize: draft.minSize,
        maxSize: draft.maxSize,
        baseSize: draft.baseSize,
        basePrice: draft.basePrice,
        perSpacePrice: draft.perSpacePrice,
        currency: draft.currency.trim().toUpperCase(),
        presets: draft.presets.map((p, index) => ({
          size: p.size,
          price: p.price,
          badge: p.badge,
          blurb: p.blurb,
          // The AUTHORED value or nothing. The formula-vs-price gap is displayed beside it and
          // is never promoted into the payload.
          savingAmount: p.savingAmount,
          sortOrder: index,
        })),
      });
      setSaved(result);
      setDraft(draftFromPlan(result));
      setUnauthored(false);
      toast.success('Box plan saved');
    } catch (err: unknown) {
      // A1/A2/A4/A5 name the offending size or the open sessions, so the message is shown
      // verbatim — A4 in particular reports a count this page cannot know.
      setError(readMessage(err) || 'The box plan could not be saved.');
    } finally {
      setSaving(false);
    }
  }
}

function NumberField({
  label,
  value,
  onChange,
  integer = false,
}: {
  label: string;
  value: number;
  onChange: (value: number) => void;
  integer?: boolean;
}) {
  return (
    <label className="flex flex-col gap-1">
      <span className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
        {label}
      </span>
      <input
        type="number"
        step={integer ? 1 : 0.01}
        value={value}
        onChange={(e) => onChange(integer ? toInt(e.target.value) : toNumber(e.target.value))}
        className={numberClass}
      />
    </label>
  );
}

function Banner({ children }: { children: ReactNode }) {
  return (
    <div className="flex items-center gap-2 rounded border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
      <AlertCircle className="h-4 w-4 shrink-0" aria-hidden />
      {children}
    </div>
  );
}

function Th({ children }: { children: ReactNode }) {
  return <th className="px-4 py-2 font-semibold">{children}</th>;
}

function Td({ children, className = '' }: { children: ReactNode; className?: string }) {
  return <td className={`px-4 py-1.5 ${className}`}>{children}</td>;
}

/** An empty box is 0, not NaN — NaN would propagate through the curve and blank the chart. */
function toNumber(text: string): number {
  const value = Number(text);
  return Number.isFinite(value) ? value : 0;
}

function toInt(text: string): number {
  const value = Number.parseInt(text, 10);
  return Number.isFinite(value) ? value : 0;
}

/** The HTTP status of a rejected api call, or undefined for a transport-level failure. */
function httpStatus(err: unknown): number | undefined {
  if (!err || typeof err !== 'object' || !('response' in err)) return undefined;
  const response = (err as { response?: { status?: number } }).response;
  return typeof response?.status === 'number' ? response.status : undefined;
}

function readMessage(err: unknown): string {
  return err && typeof err === 'object' && 'userMessage' in err
    ? String((err as { userMessage?: string }).userMessage ?? '')
    : '';
}
