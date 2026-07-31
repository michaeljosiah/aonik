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
import { DataTable, DataTablePagination, type ColumnDef } from '@/components/ui/data-table';
import { Button } from '@/components/ui/button';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { commerceCatalogService } from '@/services/commerceCatalogService';
import { commerceStorefrontService } from '@/services/commerceStorefrontService';
import { formatCurrency } from '@/lib/format';
import type { OptionChoiceDto, OptionGroupDto, ProductSummaryDto } from '@/types/commerce';

import { ChoiceEditorSheet } from './components/ChoiceEditorSheet';
import { CreateGroupDialog } from './components/CreateGroupDialog';
import { GroupEditorSheet } from './components/GroupEditorSheet';
import { DefaultMoveDialog } from './components/DefaultMoveDialog';
import { NarrowingSheet } from './components/NarrowingSheet';
import { SignedAmount } from './components/SignedAmount';
import { choiceDelta, effectiveDefaultChoice, hasNoActiveChoices } from './lib/optionPricing';

/** What one row's detail read tells us: the EFFECTIVE offer and the denominated surcharge. */
interface RowFacts {
  groupLabels: string[];
  surcharge: number | null;
  currency: string | null;
}

/** Page sizes the table offers. The offers column costs one read per row, so the ceiling is
 *  deliberate: a page of 100 is 100 requests, and the operator chooses to pay it. */
const PAGE_SIZES = [10, 25, 50];

export function PersonalisationPage() {
  const [groups, setGroups] = useState<OptionGroupDto[]>([]);
  const [products, setProducts] = useState<ProductSummaryDto[]>([]);
  const [productTotal, setProductTotal] = useState(0);
  const [productPage, setProductPage] = useState(1);
  const [productPageSize, setProductPageSize] = useState(25);
  /** What each row actually offers and charges. Absent id = unread; never "offers nothing". */
  const [rowFacts, setRowFacts] = useState<Map<string, RowFacts>>(new Map());
  const [recommendedLabel, setRecommendedLabel] = useState<string | null>(null);
  const [storefrontCurrency, setStorefrontCurrency] = useState<string | null>(null);
  const [selectedGroupKey, setSelectedGroupKey] = useState<string | null>(null);
  const [narrowing, setNarrowing] = useState<ProductSummaryDto | null>(null);
  const [creatingGroup, setCreatingGroup] = useState(false);
  const [editingGroup, setEditingGroup] = useState<OptionGroupDto | null>(null);
  const [editingChoice, setEditingChoice] = useState<{
    group: OptionGroupDto;
    choice: OptionChoiceDto;
  } | null>(null);
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
      const [groupList, page, config] = await Promise.all([
        commerceCatalogService.listOptionGroups(),
        commerceCatalogService.listProducts({
          page: productPage,
          pageSize: productPageSize,
          status: 'Active',
        }),
        // The label is presentation config, so a failure must not sink the page — it degrades
        // to no badge rather than a hardcoded word standing in for the tenant's own.
        commerceStorefrontService.getPublicStorefrontConfig().catch(() => null),
      ]);
      if (requestIdRef.current !== requestId) return;
      setGroups(groupList);
      // The requested page can stop existing under us when products are deactivated.
      const lastPage = Math.max(1, Math.ceil(page.totalCount / productPageSize));
      if (productPage > lastPage) {
        setProductTotal(page.totalCount);
        setProductPage(lastPage);
        return;
      }
      setProducts(page.items);
      setProductTotal(page.totalCount);
      setRecommendedLabel(config?.recommendedChoiceLabel ?? null);
      setStorefrontCurrency(config?.currency ?? null);
      setSelectedGroupKey((current) =>
        current && groupList.some((g) => g.key === current) ? current : (groupList[0]?.key ?? null),
      );

      // One DETAIL read per row. There is no batch endpoint and the summary carries neither
      // option data nor the surcharge currency, so the column is bounded by the page size.
      //
      // The detail's EFFECTIVE groups are what the storefront actually composes — the raw
      // authoring lines are not: a line for a retired group, or one whose pinned choices have
      // all gone inactive, is still stored but dropped by ComposeEffective, so counting raw
      // lines reported products as personalisable whose panel is in fact hidden.
      const entries = await Promise.all(
        page.items.map(async (product) => {
          try {
            const detail = await commerceCatalogService.getProduct(product.id);
            return [
              product.id,
              {
                groupLabels: detail.effectiveOptionGroups.map((g) => g.label ?? g.key),
                surcharge: detail.unitSurcharge,
                currency: detail.unitSurchargeCurrency,
              },
            ] as const;
          } catch {
            return [product.id, null] as const;
          }
        }),
      );
      if (requestIdRef.current !== requestId) return;
      setRowFacts(
        new Map(
          entries.filter((e): e is readonly [string, RowFacts] => e[1] !== null) as Iterable<
            [string, RowFacts]
          >,
        ),
      );
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) return;
      // The last good catalogue is KEPT. Clearing it let an open narrowing sheet re-read an
      // empty group list, build an empty draft map, and save `groups: []` — erasing the
      // product's whole offer because a refresh happened to fail. A failed refresh means the
      // data is unknown, not gone; the banner says so and the page keeps what it had.
      setError(readMessage(err) || 'Personalisation could not be refreshed — showing the last data loaded.');
    } finally {
      if (requestIdRef.current === requestId) {
        setLoading(false);
        setInitialLoad(false);
      }
    }
  }, [productPage, productPageSize]);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  const selectedGroup = groups.find((group) => group.key === selectedGroupKey) ?? null;

  /** Rows whose per-product detail actually came back. */
  const readRows = useMemo(() => products.filter((p) => rowFacts.has(p.id)), [products, rowFacts]);

  const kpis = useMemo(
    () => ({
      totalChoices: groups.reduce((sum, group) => sum + group.choices.length, 0),
      // Same freshness rule as the cell: a successful detail read wins, the summary is used
      // only for unread rows. Counting the summary here made the tile contradict the column
      // it sits above when a surcharge changed between the two reads.
      surcharged: products.filter((p) => {
        const facts = rowFacts.get(p.id);
        return (facts ? facts.surcharge : p.unitSurcharge) != null;
      }).length,
      // Counted over the rows whose detail read SUCCEEDED, and the denominator says so. A
      // missing entry means unread, not "offers nothing" — treating it as zero let a throttled
      // fan-out report "0 of 25 narrowed" for a catalogue that is entirely narrowed.
      narrowed: readRows.filter((p) => (rowFacts.get(p.id)?.groupLabels.length ?? 0) > 0).length,
      readRows: readRows.length,
    }),
    [groups, products, rowFacts, readRows],
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
      id: 'offers',
      header: 'Offers',
      accessorFn: (row) => rowFacts.get(row.id)?.groupLabels.length ?? -1,
      cell: (row) => {
        const groupLabels = rowFacts.get(row.id)?.groupLabels;
        if (!groupLabels) {
          // Unread or failed — NOT "not personalisable". Claiming a product offers nothing
          // because a request failed would send an operator to fix something that is fine.
          return <span className="text-[11px] text-[var(--color-text-tertiary)]">unknown</span>;
        }
        if (groupLabels.length === 0) {
          return (
            <span className="text-[11.5px] text-[var(--color-text-tertiary)]">
              Not personalisable — panel hidden
            </span>
          );
        }
        return (
          <span className="flex flex-wrap gap-1">
            {groupLabels.slice(0, 3).map((label) => (
              <Pill key={label} tone="muted" size="sm">
                {label}
              </Pill>
            ))}
            {groupLabels.length > 3 && (
              <span className="text-[11px] text-[var(--color-text-tertiary)]">
                +{groupLabels.length - 3}
              </span>
            )}
          </span>
        );
      },
      className: 'w-[260px]',
    },
    {
      id: 'surcharge',
      header: 'Unit surcharge',
      accessorFn: (row) => rowFacts.get(row.id)?.surcharge ?? row.unitSurcharge ?? -1,
      // The AMOUNT, now that the row read carries its currency. A bare number without its
      // denomination would read as a price in whatever currency the operator assumed, so a
      // row whose detail failed shows the marker instead of guessing.
      cell: (row) => {
        const facts = rowFacts.get(row.id);
        // A successful detail read WINS over the list summary, including when it reports null.
        // The list is read first, so a surcharge cleared by another operator in between leaves
        // the summary holding the old amount — and falling back to it rendered "Set" over a
        // fresher read that proves it is gone.
        if (facts) {
          if (facts.surcharge == null) {
            return <span className="block text-right text-[var(--color-text-tertiary)]">—</span>;
          }
          return facts.currency ? (
            <span className="block text-right font-[family-name:var(--font-mono)] text-[12.5px] tabular-nums text-[var(--color-text-primary)]">
              {formatCurrency(facts.surcharge, facts.currency)}
            </span>
          ) : (
            // An amount with no denomination is the thing the marker exists to avoid.
            <Pill tone="info" size="sm" dot>
              Set
            </Pill>
          );
        }
        // Unread: the summary is all there is, and it carries no currency.
        return row.unitSurcharge != null ? (
          <Pill tone="info" size="sm" dot>
            Set
          </Pill>
        ) : (
          <span className="block text-right text-[var(--color-text-tertiary)]">—</span>
        );
      },
      className: 'w-[150px] text-right',
      headerClassName: 'text-right',
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
        {/* Both of these are PAGE-scoped and say so. The reads that produce them are per-row,
            so a tenant-wide figure would mean fetching every active product on page load —
            and a caption-less number here would be quoted as a whole-catalogue fact. */}
        <KpiTile
          label="Products narrowed"
          value={kpis.narrowed.toLocaleString()}
          delta={
            kpis.readRows === products.length
              ? `of ${products.length} on this page`
              : `of ${kpis.readRows} read on this page`
          }
          deltaTone="neutral"
        />
        <KpiTile
          label="Unit surcharges set"
          value={kpis.surcharged.toLocaleString()}
          delta={`of ${products.length} on this page`}
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
            <AonikCard
              title="Groups"
              padding={0}
              action={
                // Available whether or not groups exist. The catalogue is deliberately
                // multi-group — portion, spice, side — so hiding this after the first one
                // made every axis beyond it unreachable from the admin surface.
                <Button variant="outline" size="sm" onClick={() => setCreatingGroup(true)}>
                  <Plus className="mr-1 h-3.5 w-3.5" /> New group
                </Button>
              }
            >
              {groups.length === 0 ? (
                <div className="flex flex-col items-center gap-2 px-4 py-8 text-center">
                  <p className="text-[12.5px] text-[var(--color-text-secondary)]">
                    No option groups yet — products cannot be personalised until one exists.
                  </p>
                  <Button variant="outline" size="sm" onClick={() => setCreatingGroup(true)}>
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
                              {group.choices.length === 0 ? 'no choices yet' : 'every choice retired'}
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
                onEditGroup={() => setEditingGroup(selectedGroup)}
                recommendedLabel={recommendedLabel}
                onMoveDefault={(target) => setDefaultMove({ group: selectedGroup, target })}
                onEditChoice={(choice) => setEditingChoice({ group: selectedGroup, choice })}
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
            subtitle={`Active products — ${productTotal} in total`}
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
            <DataTablePagination
              pageNumber={productPage}
              pageSize={productPageSize}
              totalCount={productTotal}
              pageSizeOptions={PAGE_SIZES}
              onPageChange={setProductPage}
              onPageSizeChange={(size) => {
                setProductPageSize(size);
                setProductPage(1);
              }}
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

      {editingGroup && (
        <GroupEditorSheet
          key={editingGroup.id}
          group={editingGroup}
          storefrontCurrency={storefrontCurrency}
          onClose={() => setEditingGroup(null)}
          onSaved={() => void loadData()}
        />
      )}

      {creatingGroup && (
        <CreateGroupDialog
          defaultCurrency={storefrontCurrency}
          onClose={() => setCreatingGroup(false)}
          onCreated={() => void loadData()}
        />
      )}

      {editingChoice && (
        <ChoiceEditorSheet
          key={editingChoice.choice.id}
          group={editingChoice.group}
          choice={editingChoice.choice}
          onClose={() => setEditingChoice(null)}
          onSaved={() => void loadData()}
        />
      )}

      {narrowing && (
        <NarrowingSheet
          key={narrowing.id}
          product={narrowing}
          groups={groups}
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
  onEditChoice,
  onEditGroup,
  onChanged,
}: {
  group: OptionGroupDto;
  recommendedLabel: string | null;
  onMoveDefault: (choice: OptionChoiceDto) => void;
  onEditChoice: (choice: OptionChoiceDto) => void;
  onEditGroup: () => void;
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
    <AonikCard
      title={group.label}
      subtitle={group.helpText ?? undefined}
      padding={0}
      action={
        <Button variant="outline" size="sm" onClick={onEditGroup}>
          Edit group
        </Button>
      }
    >
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
                <td colSpan={5} className="px-4 py-6 text-center">
                  <span className="flex flex-col items-center gap-2">
                    <span className="text-[12.5px] text-[var(--color-text-secondary)]">
                      This group has no choices yet, so the storefront shows it to nobody.
                    </span>
                    <Button variant="outline" size="sm" onClick={onEditGroup}>
                      <Plus className="mr-1 h-3.5 w-3.5" /> Add the first choice
                    </Button>
                  </span>
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
                      {/* Not offered while the GROUP is inactive: the move succeeds but stages
                          no content review, because the group is absent from every effective
                          selection — while the API can still return inheriting slugs, so the
                          consequence dialog would claim those products' standard preparations
                          changed when nothing of the kind happened. */}
                      {!isDefault && choice.isActive && group.isActive && (
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
                        onClick={() => onEditChoice(choice)}
                        className="text-[11.5px] text-[var(--color-brand-primary)] hover:underline"
                      >
                        Edit
                      </button>
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

      {!group.isActive && (
        <p className="border-t border-[var(--color-border-light)] px-3 py-2 text-[11px] text-[var(--color-warning)]">
          This group is retired, so it appears on no storefront. Defaults cannot be moved while
          it is inactive — the move would change nothing customers can see.
        </p>
      )}
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
