// Commerce overview (Spec 084) — the section landing, and the page the live app never had.
// Its job is triage: four numbers, recent orders, and an attention list where every row is a
// real state read from the surface that owns it, with a link there.
//
// SOURCES SETTLE INDEPENDENTLY. Each request owns one state slot and updates it in its own
// then/catch — deliberately not a joint await, which would hold every card hostage to the
// slowest source and turn one failure into an empty page. A source that fails degrades to its
// own "could not be read" row and nothing else on the page notices.

import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  AlertTriangle,
  ChevronRight,
  Info,
  Minus,
  RefreshCw,
} from 'lucide-react';

import { Card as AonikCard, KpiTile, PageHeader, Pill } from '@/components/layout/aonik';
import { commerceContentService } from '@/services/commerceContentService';
import { commerceStorefrontService } from '@/services/commerceStorefrontService';
import { formatCurrency, formatDate } from '@/lib/format';
import type { AdminStorefrontOrderRowDto } from '@/types/commerce';

import { BuyerLabel } from './components/BuyerLabel';
import {
  allSettled,
  buildAttentionRows,
  type AttentionRowModel,
  type AttentionSources,
  type SourceState,
} from './lib/attention';
import { cartBlocked } from './lib/cartState';
import { summariseOrderWindow } from './lib/orderWindow';
import { paymentTone } from './lib/statusTone';

/** The order window every tile and the recent list describe. Named in the captions. */
const ORDER_WINDOW = 25;
/** Open carts scanned for the blocked count — the flag is per-cart and cannot be counted server-side. */
const CART_WINDOW = 25;
/** Products scanned for the content-review floor. Bounded, and disclosed when it bites. */
const CONTENT_SCAN = 200;
/** Collections inspected for staged drafts. Bounded, and disclosed when it bites. */
const COLLECTION_LIMIT = 20;

const LOADING = { kind: 'loading' } as const;

export function CommerceOverviewPage() {
  const navigate = useNavigate();

  const [orders, setOrders] = useState<SourceState<AdminStorefrontOrderRowDto[]>>(LOADING);
  const [contentReview, setContentReview] = useState<AttentionSources['contentReview']>(LOADING);
  const [deliveryPromise, setDeliveryPromise] =
    useState<AttentionSources['deliveryPromise']>(LOADING);
  const [stagedDrafts, setStagedDrafts] = useState<AttentionSources['stagedDrafts']>(LOADING);
  const [skippedExtras, setSkippedExtras] = useState<AttentionSources['skippedExtras']>(LOADING);
  const [abandonedCarts, setAbandonedCarts] = useState<AttentionSources['abandonedCarts']>(LOADING);
  const [blockedCarts, setBlockedCarts] = useState<AttentionSources['blockedCarts']>(LOADING);

  // Fires the requests and nothing else. The slots already START as `loading`, so resetting
  // them here would be a synchronous write during the mount effect for no gain — the retry
  // handler below does the resetting, where it is an event and actually needed.
  const load = useCallback(() => {
    commerceStorefrontService
      .listStorefrontOrders({ page: 1, pageSize: ORDER_WINDOW })
      .then((result) => setOrders({ kind: 'ready', value: result.items }))
      .catch(() => setOrders({ kind: 'unavailable' }));

    // The status list is paged and carries no server-side "requiresReview" filter, so this
    // scans one page and reports a FLOOR when there are more — the row says "at least N"
    // rather than presenting a partial scan as a total.
    commerceContentService
      .listContentStatus(1, CONTENT_SCAN)
      .then((result) =>
        setContentReview({
          kind: 'ready',
          value: {
            count: result.items.filter((row) => row.requiresReview).length,
            inspected: result.items.length,
            complete: result.items.length >= result.totalCount,
          },
        }),
      )
      .catch(() => setContentReview({ kind: 'unavailable' }));

    commerceStorefrontService
      .getPublicDelivery()
      .then((promise) =>
        setDeliveryPromise({ kind: 'ready', value: promise.earliestDeliveryDate ?? null }),
      )
      // 404 is the DESIGNED answer for "no promise configured", not a failure — Spec 069 is
      // explicit that unconfigured is a state. Any other error is genuinely unavailable.
      .catch((err: unknown) =>
        httpStatus(err) === 404
          ? setDeliveryPromise({ kind: 'ready', value: null })
          : setDeliveryPromise({ kind: 'unavailable' }),
      );

    commerceStorefrontService
      .listCollections()
      .then(async (collections) => {
        const inspected = collections.slice(0, COLLECTION_LIMIT);
        const details = await Promise.all(
          inspected.map((collection) => commerceStorefrontService.getCollection(collection.id)),
        );
        // DISTINCT products: one draft staged in three collections is one product to activate,
        // not three.
        const drafts = new Set<string>();
        for (const detail of details) {
          for (const item of detail.items) {
            if (item.status === 'Draft') drafts.add(item.productId);
          }
        }
        setStagedDrafts({
          kind: 'ready',
          value: {
            count: drafts.size,
            collectionsInspected: inspected.length,
            complete: inspected.length === collections.length,
          },
        });
      })
      .catch(() => setStagedDrafts({ kind: 'unavailable' }));

    commerceStorefrontService
      .getPublicExtras()
      .then((extras) => setSkippedExtras({ kind: 'ready', value: extras.skipped }))
      .catch(() => setSkippedExtras({ kind: 'unavailable' }));

    commerceStorefrontService
      .listCarts({ page: 1, pageSize: 1, status: 'Abandoned' })
      .then((result) => setAbandonedCarts({ kind: 'ready', value: result.totalCount }))
      .catch(() => setAbandonedCarts({ kind: 'unavailable' }));

    commerceStorefrontService
      .listCarts({ page: 1, pageSize: CART_WINDOW, status: 'Open' })
      .then((result) =>
        setBlockedCarts({
          kind: 'ready',
          value: {
            count: result.items.filter((cart) => cartBlocked(cart.boxMeta).blocked).length,
            window: Math.min(CART_WINDOW, result.totalCount),
          },
        }),
      )
      .catch(() => setBlockedCarts({ kind: 'unavailable' }));
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const retry = useCallback(() => {
    setOrders(LOADING);
    setContentReview(LOADING);
    setDeliveryPromise(LOADING);
    setStagedDrafts(LOADING);
    setSkippedExtras(LOADING);
    setAbandonedCarts(LOADING);
    setBlockedCarts(LOADING);
    load();
  }, [load]);

  const sources: AttentionSources = useMemo(
    () => ({
      contentReview,
      deliveryPromise,
      stagedDrafts,
      skippedExtras,
      abandonedCarts,
      blockedCarts,
    }),
    [contentReview, deliveryPromise, stagedDrafts, skippedExtras, abandonedCarts, blockedCarts],
  );

  const rows = useMemo(() => buildAttentionRows(sources), [sources]);
  const settled = allSettled(sources);

  const orderRows = useMemo(() => (orders.kind === 'ready' ? orders.value : []), [orders]);
  const summary = useMemo(() => summariseOrderWindow(orderRows), [orderRows]);
  const windowCaption = `latest ${orderRows.length} orders`;

  return (
    <div className="flex flex-col gap-5 p-6 md:px-8">
      <PageHeader
        eyebrow="Commerce"
        title="Overview"
        subtitle="The storefront's pulse — what sold, and what needs a human"
      />

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {orders.kind === 'unavailable' ? (
          <div className="col-span-2 rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-5 text-[12.5px] text-[var(--color-text-secondary)] lg:col-span-4">
            Order figures could not be read.{' '}
            <button type="button" onClick={retry} className="underline">
              Retry
            </button>
          </div>
        ) : (
          <>
            <KpiTile
              label="Orders"
              value={orders.kind === 'loading' ? '…' : orderRows.length.toLocaleString()}
              delta={windowCaption}
              deltaTone="neutral"
            />
            <KpiTile
              label="Paid revenue"
              value={orders.kind === 'loading' ? '…' : summary.paidRevenue}
              delta={summary.moneyCaption}
              deltaTone="neutral"
            />
            <KpiTile
              label="Average paid order"
              value={orders.kind === 'loading' ? '…' : summary.averageOrder}
              delta={summary.moneyCaption}
              deltaTone="neutral"
            />
            {/* Awaiting PAYMENT, not fulfilment: fulfilment is underived server-side, so an
                "awaiting fulfilment" tile would equal the order count. Same call as Spec 083. */}
            <KpiTile
              label="Awaiting payment"
              value={orders.kind === 'loading' ? '…' : summary.awaitingPayment.toLocaleString()}
              delta={windowCaption}
              deltaTone={summary.awaitingPayment > 0 ? 'down' : 'neutral'}
            />
          </>
        )}
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <AonikCard
          title="Recent orders"
          subtitle={orders.kind === 'ready' ? windowCaption : undefined}
          padding={0}
        >
          {orders.kind === 'loading' ? (
            <div className="flex items-center justify-center py-10">
              <RefreshCw className="h-5 w-5 animate-spin text-[var(--color-brand-primary)]" />
            </div>
          ) : orders.kind === 'unavailable' ? (
            <p className="px-4 py-8 text-center text-[12.5px] text-[var(--color-text-secondary)]">
              Orders could not be read.
            </p>
          ) : orderRows.length === 0 ? (
            <p className="px-4 py-8 text-center text-[12.5px] text-[var(--color-text-secondary)]">
              No storefront orders yet.
            </p>
          ) : (
            <ul className="flex flex-col divide-y divide-[var(--color-border-light)]">
              {orderRows.slice(0, 8).map((order) => (
                <li key={order.orderId}>
                  <button
                    type="button"
                    onClick={() => navigate(`/commerce/orders/${order.orderId}`)}
                    className="flex w-full items-center gap-3 px-4 py-2.5 text-left hover:bg-[var(--color-surface-inset)]"
                  >
                    <span className="w-[70px] shrink-0 font-[family-name:var(--font-mono)] text-[11.5px] text-[var(--color-text-primary)]">
                      {order.orderId.slice(0, 8)}
                    </span>
                    <span className="min-w-0 flex-1">
                      <BuyerLabel buyerKind={order.buyerKind} buyerPartyId={order.buyerPartyId} />
                    </span>
                    <span className="shrink-0 font-[family-name:var(--font-mono)] text-[12px] tabular-nums text-[var(--color-text-primary)]">
                      {formatCurrency(order.total, order.currency)}
                    </span>
                    <Pill tone={paymentTone(order.paymentStatus)} size="sm">
                      {order.paymentStatus}
                    </Pill>
                    <span className="w-[80px] shrink-0 text-right text-[11px] text-[var(--color-text-tertiary)]">
                      {formatDate(order.placedAtUtc)}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </AonikCard>

        <AonikCard title="Needs attention" padding={0}>
          {rows.length === 0 ? (
            <p className="px-4 py-8 text-center text-[12.5px] text-[var(--color-text-secondary)]">
              {settled ? 'All quiet on the storefront.' : 'Checking the storefront…'}
            </p>
          ) : (
            <ul className="flex flex-col divide-y divide-[var(--color-border-light)]">
              {rows.map((row) => (
                <AttentionRow key={row.key} row={row} onOpen={() => navigate(row.href)} />
              ))}
            </ul>
          )}
        </AonikCard>
      </div>
    </div>
  );
}

const TONE_ICON = {
  warn: AlertTriangle,
  info: Info,
  muted: Minus,
};

const TONE_CLASS = {
  warn: 'text-[var(--color-warning)]',
  info: 'text-[var(--color-brand-primary)]',
  muted: 'text-[var(--color-text-tertiary)]',
};

function AttentionRow({ row, onOpen }: { row: AttentionRowModel; onOpen: () => void }) {
  const Icon = TONE_ICON[row.tone];
  return (
    <li>
      <button
        type="button"
        onClick={onOpen}
        className="flex w-full items-start gap-2.5 px-4 py-3 text-left hover:bg-[var(--color-surface-inset)]"
      >
        <Icon className={`mt-px h-4 w-4 shrink-0 ${TONE_CLASS[row.tone]}`} aria-hidden />
        <span className="flex min-w-0 flex-1 flex-col gap-0.5">
          <span className="text-[13px] text-[var(--color-text-primary)]">{row.statement}</span>
          <span className="text-[11.5px] text-[var(--color-text-tertiary)]">{row.subline}</span>
        </span>
        <ChevronRight
          className="mt-0.5 h-4 w-4 shrink-0 text-[var(--color-text-tertiary)]"
          aria-hidden
        />
      </button>
    </li>
  );
}

/** The HTTP status of a rejected api call, or undefined for a transport-level failure. */
function httpStatus(err: unknown): number | undefined {
  if (!err || typeof err !== 'object' || !('response' in err)) return undefined;
  const response = (err as { response?: { status?: number } }).response;
  return typeof response?.status === 'number' ? response.status : undefined;
}
