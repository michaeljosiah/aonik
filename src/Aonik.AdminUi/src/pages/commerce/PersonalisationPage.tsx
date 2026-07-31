// Personalisation (Spec 074) — the tenant's option catalogue, its recommended defaults, and
// each product's narrowing of it.
//
// Two rules the design review hardened, and everything here follows from them:
//
//   PRICES ARE ABSOLUTE, DELTAS ARE DERIVED (Spec 066 §8). Nothing on this page stores a
//   "+£1.50". Every delta comes from `choiceDelta` against the effective default, so the
//   table and the narrowing Sheet cannot disagree about what a choice costs extra.
//
//   A DEFAULT MOVE IS A CONSEQUENCE SURFACE. Promoting a choice changes the standard
//   preparation of every product that inherits it and flags their content for review, so it
//   opens a dialog reporting the blast radius the API returned rather than closing on OK.
//
// The default's badge text is the tenant's configured label, fetched live — product identity
// is configuration, never a literal in platform code (ADR-013).

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { AlertCircle, Plus, RefreshCw, Star } from 'lucide-react';
import { toast } from 'sonner';

import { Card as AonikCard, KpiTile, PageHeader, Pill } from '@/components/layout/aonik';
import { DataTable, type ColumnDef } from '@/components/ui/data-table';
import { Button } from '@/components/ui/button';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { commerceCatalogService } from '@/services/commerceCatalogService';
import { commerceStorefrontService } from '@/services/commerceStorefrontService';
import { formatCurrency } from '@/lib/format';
import type { OptionChoiceDto, OptionGroupDto, ProductSummaryDto } from '@/types/commerce';

import { DefaultMoveDialog } from './components/DefaultMoveDialog';
import { NarrowingSheet } from './components/NarrowingSheet';
import { SignedAmount } from './components/SignedAmount';
import { choiceDelta, effectiveDefaultChoice, hasNoActiveChoices } from './lib/optionPricing';

/** Products listed for narrowing. Named in the caption; this table is not paged. */
const PRODUCT_WINDOW = 100;

export function PersonalisationPage() {
  const [groups, setGroups] = useState<OptionGroupDto[]>([]);
  const [products, setProducts] = useState<ProductSummaryDto[]>([]);
  const [productTotal, setProductTotal] = useState(0);
  const [recommendedLabel, setRecommendedLabel] = useState<string | null>(null);
  const [storefrontCurrency, setStorefrontCurrency] = useState<string | null>(null);
  const [selectedGroupKey, setSelectedGroupKey] = useState<string | null>(null);
  const [narrowing, setNarrowing] = useState<ProductSummaryDto | null>(null);
  const [defaultMove, setDefaultMove] = useState<{
    group: OptionGroupDto;
    target: OptionChoiceDto;
  } | null>(null);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const requestIdRef = useRef(0);

  const loadData = useCallback(async () => {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setLoading(true);
    setError(null);
    try {
      const [groupList, productPage, config] = await Promise.all([
        commerceCatalogService.listOptionGroups(),
        commerceCatalogService.listProducts({ page: 1, pageSize: PRODUCT_WINDOW, status: 'Active' }),
        // The label is presentation config, so a failure must not sink the page — it degrades
        // to no badge rather than a hardcoded word standing in for the tenant's own.
        commerceStorefrontService.getPublicStorefrontConfig().catch(() => null),
      ]);
      if (requestIdRef.current !== requestId) return;
      setGroups(groupList);
      setProducts(productPage.items);
      setProductTotal(productPage.totalCount);
      setRecommendedLabel(config?.recommendedChoiceLabel ?? null);
      setStorefrontCurrency(config?.currency ?? null);
      setSelectedGroupKey((current) =>
        current && groupList.some((g) => g.key === current) ? current : (groupList[0]?.key ?? null),
      );
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) return;
      setGroups([]);
      setProducts([]);
      setProductTotal(0);
      setError(readMessage(err) || 'Personalisation could not be loaded.');
    } finally {
      if (requestIdRef.current === requestId) {
        setLoading(false);
        setInitialLoad(false);
      }
    }
  }, []);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  const selectedGroup = groups.find((group) => group.key === selectedGroupKey) ?? null;

  const kpis = useMemo(
    () => ({
      totalChoices: groups.reduce((sum, group) => sum + group.choices.length, 0),
      surcharged: products.filter((p) => p.unitSurcharge != null).length,
    }),
    [groups, products],
  );

  const productColumns: ColumnDef<ProductSummaryDto>[] = [
    {
      id: 'product',
      header: 'Product',
      accessorFn: (row) => row.name,
      cell: (row) => (
        <span className="flex flex-col">
          <span className="text-[13px] text-[var(--color-text-primary)]">{row.name}</span>
          <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">
            {row.slug}
          </span>
        </span>
      ),
      className: 'pl-4',
      headerClassName: 'pl-4',
    },
    {
      id: 'surcharge',
      header: 'Unit surcharge',
      accessorFn: (row) => row.unitSurcharge ?? -1,
      // A MARKER, not an amount: the summary DTO carries the number but not its currency, and
      // a bare figure would read as a price in whatever currency the operator assumed. The
      // Sheet shows the amount, where the currency is known.
      cell: (row) =>
        row.unitSurcharge != null ? (
          <Pill tone="info" size="sm" dot>
            Set
          </Pill>
        ) : (
          <span className="text-[var(--color-text-tertiary)]">—</span>
        ),
      className: 'w-[140px]',
    },
    {
      id: 'edit',
      header: '',
      accessorFn: () => '',
      cell: () => (
        <span className="block text-right text-[11.5px] text-[var(--color-brand-primary)]">
          Edit offer
        </span>
      ),
      className: 'w-[110px] text-right',
    },
  ];

  if (initialLoad) return <PageLoadingScreen message="Loading personalisation" />;

  return (
    <div className="flex flex-col gap-5 p-6 md:px-8">
      <PageHeader
        eyebrow="Commerce"
        title="Personalisation"
        subtitle={`Stored prices are absolute; every “vs default” figure is derived against the group's default${
          recommendedLabel ? `, which the storefront labels “${recommendedLabel}”` : ''
        }.`}
      />

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <KpiTile
          label="Option groups"
          value={groups.length.toLocaleString()}
          delta="catalogue"
          deltaTone="neutral"
        />
        <KpiTile
          label="Choices"
          value={kpis.totalChoices.toLocaleString()}
          delta="catalogue"
          deltaTone="neutral"
        />
        <KpiTile
          label="Active products"
          value={productTotal.toLocaleString()}
          delta="all pages"
          deltaTone="neutral"
        />
        <KpiTile
          label="Unit surcharges set"
          value={kpis.surcharged.toLocaleString()}
          delta={`of the ${products.length} listed`}
          deltaTone="neutral"
        />
      </div>

      {error && (
        <div className="flex items-center gap-2 rounded border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
          <AlertCircle className="h-4 w-4" />
          {error}
          <button type="button" onClick={() => void loadData()} className="ml-auto underline">
            Retry
          </button>
        </div>
      )}

      {loading ? (
        <div className="flex items-center justify-center py-10">
          <RefreshCw className="h-5 w-5 animate-spin text-[var(--color-brand-primary)]" />
        </div>
      ) : (
        <>
          <div className="grid gap-4 lg:grid-cols-[260px_1fr]">
            <AonikCard title="Groups" padding={0}>
              {groups.length === 0 ? (
                <div className="flex flex-col items-center gap-2 px-4 py-8 text-center">
                  <p className="text-[12.5px] text-[var(--color-text-secondary)]">
                    No option groups yet — products cannot be personalised until one exists.
                  </p>
                  <Button
                    variant="outline"
                    size="sm"
                    disabled
                    title="Group authoring is not built yet — create groups with the aonik CLI"
                  >
                    <Plus className="mr-1 h-3.5 w-3.5" /> New group
                  </Button>
                </div>
              ) : (
                <ul className="flex flex-col divide-y divide-[var(--color-border-light)]">
                  {groups.map((group) => {
                    const groupDefault = effectiveDefaultChoice(group.choices);
                    return (
                      <li key={group.key}>
                        <button
                          type="button"
                          onClick={() => setSelectedGroupKey(group.key)}
                          className={`flex w-full flex-col gap-0.5 px-4 py-2.5 text-left hover:bg-[var(--color-surface-inset)] ${
                            group.key === selectedGroupKey ? 'bg-[var(--color-surface-inset)]' : ''
                          }`}
                        >
                          <span className="flex items-center gap-1.5">
                            <span className="text-[13px] text-[var(--color-text-primary)]">
                              {group.label}
                            </span>
                            {!group.isActive && (
                              <Pill tone="muted" size="sm">
                                Retired
                              </Pill>
                            )}
                          </span>
                          <span className="text-[11px] text-[var(--color-text-tertiary)]">
                            {group.choices.length} choice{group.choices.length === 1 ? '' : 's'}
                            {groupDefault ? ` — ${groupDefault.label}` : ''}
                          </span>
                          {hasNoActiveChoices(group.choices) && (
                            <span className="text-[11px] text-[var(--color-warning)]">
                              every choice retired
                            </span>
                          )}
                        </button>
                      </li>
                    );
                  })}
                </ul>
              )}
            </AonikCard>

            {selectedGroup ? (
              <ChoicesCard
                group={selectedGroup}
                recommendedLabel={recommendedLabel}
                onMoveDefault={(target) => setDefaultMove({ group: selectedGroup, target })}
                onChanged={() => void loadData()}
              />
            ) : (
              <AonikCard padding={12}>
                <p className="py-6 text-center text-[12.5px] text-[var(--color-text-secondary)]">
                  Select a group to see its choices.
                </p>
              </AonikCard>
            )}
          </div>

          <AonikCard
            title="Per-product offer"
            subtitle={`Active products — showing ${products.length} of ${productTotal}`}
            padding={0}
          >
            <DataTable
              data={products}
              columns={productColumns}
              getRowId={(row) => row.id}
              onRowClick={(row) => setNarrowing(row)}
              emptyTitle="No active products"
              emptyDescription="Only active products can be offered on the storefront."
              showCheckboxes={false}
            />
          </AonikCard>
        </>
      )}

      {defaultMove && (
        <DefaultMoveDialog
          group={defaultMove.group}
          target={defaultMove.target}
          current={effectiveDefaultChoice(defaultMove.group.choices)}
          onClose={() => setDefaultMove(null)}
          onMoved={() => void loadData()}
        />
      )}

      {narrowing && (
        <NarrowingSheet
          key={narrowing.id}
          product={narrowing}
          groups={groups}
          surcharge={{ amount: narrowing.unitSurcharge ?? null, currency: null }}
          storefrontCurrency={storefrontCurrency}
          onClose={() => setNarrowing(null)}
          onSaved={() => void loadData()}
        />
      )}
    </div>
  );
}

function ChoicesCard({
  group,
  recommendedLabel,
  onMoveDefault,
  onChanged,
}: {
  group: OptionGroupDto;
  recommendedLabel: string | null;
  onMoveDefault: (choice: OptionChoiceDto) => void;
  onChanged: () => void;
}) {
  const baseline = effectiveDefaultChoice(group.choices);

  const toggleRetired = async (choice: OptionChoiceDto) => {
    try {
      // The update contract splits text from value members: label and note are assigned
      // unconditionally server-side, so the CURRENT text must be resent or it is erased.
      await commerceCatalogService.updateOptionChoice(choice.id, {
        label: choice.label,
        note: choice.note,
        isActive: !choice.isActive,
      });
      toast.success(choice.isActive ? 'Choice retired' : 'Choice reactivated');
      onChanged();
    } catch (err: unknown) {
      // The backend refuses to retire a group's recommended default. Its message is surfaced
      // verbatim rather than paraphrased, because this page does not own that rule.
      toast.error(readMessage(err) || 'The choice could not be updated.');
    }
  };

  return (
    <AonikCard title={group.label} subtitle={group.helpText ?? undefined} padding={0}>
      <div className="overflow-x-auto">
        <table className="w-full">
          <thead>
            <tr className="border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)]/50 text-left">
              <th className="w-10 px-3 py-2" />
              <th className="px-2 py-2 text-[11px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                Choice
              </th>
              <th className="w-[120px] px-2 py-2 text-right text-[11px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                vs default
              </th>
              <th className="w-[100px] px-2 py-2 text-[11px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                Status
              </th>
              <th className="w-[170px] px-3 py-2" />
            </tr>
          </thead>
          <tbody>
            {group.choices.length === 0 && (
              <tr>
                <td
                  colSpan={5}
                  className="px-4 py-6 text-center text-[12.5px] text-[var(--color-text-secondary)]"
                >
                  This group has no choices yet.
                </td>
              </tr>
            )}
            {group.choices.map((choice) => {
              const delta = choiceDelta(choice, baseline);
              const isDefault = baseline?.key === choice.key;
              return (
                <tr
                  key={choice.key}
                  className="border-b border-[var(--color-border-light)] last:border-0"
                >
                  <td className="px-3 py-2">
                    {isDefault && (
                      <Star
                        className="h-3.5 w-3.5 fill-[var(--color-warning)] text-[var(--color-warning)]"
                        aria-label="Recommended default"
                      />
                    )}
                  </td>
                  <td className="px-2 py-2">
                    <span className="flex flex-col gap-0.5">
                      <span className="flex flex-wrap items-center gap-1.5">
                        <span className="text-[13px] text-[var(--color-text-primary)]">
                          {choice.label}
                        </span>
                        {isDefault && recommendedLabel && (
                          <Pill tone="info" size="sm">
                            {recommendedLabel}
                          </Pill>
                        )}
                      </span>
                      {choice.note && (
                        <span className="text-[11px] text-[var(--color-text-tertiary)]">
                          {choice.note}
                        </span>
                      )}
                    </span>
                  </td>
                  <td className="px-2 py-2 text-right">
                    {delta === null ? (
                      // No default means no baseline. Showing the absolute price under a column
                      // headed "vs default" would read as a delta and overstate every choice.
                      <span className="text-[11px] text-[var(--color-text-tertiary)]">
                        no default
                      </span>
                    ) : (
                      <SignedAmount amount={delta} currency={group.currency} />
                    )}
                  </td>
                  <td className="px-2 py-2">
                    <Pill tone={choice.isActive ? 'success' : 'muted'} size="sm">
                      {choice.isActive ? 'Active' : 'Retired'}
                    </Pill>
                  </td>
                  <td className="px-3 py-2">
                    <span className="flex justify-end gap-2.5">
                      {!isDefault && choice.isActive && (
                        <button
                          type="button"
                          onClick={() => onMoveDefault(choice)}
                          className="text-[11.5px] text-[var(--color-brand-primary)] hover:underline"
                        >
                          Make default
                        </button>
                      )}
                      <button
                        type="button"
                        onClick={() => void toggleRetired(choice)}
                        className="text-[11.5px] text-[var(--color-text-secondary)] hover:underline"
                      >
                        {choice.isActive ? 'Retire' : 'Reactivate'}
                      </button>
                    </span>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <p className="border-t border-[var(--color-border-light)] px-3 py-2 text-[11px] text-[var(--color-text-tertiary)]">
        Stored prices are absolute (Spec 066 §8) — the figures above are differences against{' '}
        {baseline ? baseline.label : 'the default'}, derived, never authored.
        {baseline &&
          ` ${baseline.label} itself costs ${formatCurrency(baseline.price, group.currency)}.`}
      </p>
    </AonikCard>
  );
}

function readMessage(err: unknown): string {
  return err && typeof err === 'object' && 'userMessage' in err
    ? String((err as { userMessage?: string }).userMessage ?? '')
    : '';
}
