// Right-rail order cart — port of the cart half of `ScreenCreateOrder`
// from templates/aonik-admin-starterkit/screens/orders.jsx.
//
// Shows pre-existing items (with branded logo, payer→receiver row, mono
// amount, fees) plus a per-currency totals block, an auto-apply policy
// banner, and the Save draft + Submit actions. The template's "preview" of
// the in-progress item is intentionally omitted — the form stays in the
// left pane until it's added.

import { ArrowRight, RefreshCw, ShieldCheck, Trash2 } from 'lucide-react';
import { Pill } from '@/components/layout/aonik';
import { PartyAvatar } from './PartyAvatar';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import type { BillPaymentOrderResponse, OrderItemResponse } from '@/types';

const BRAND_PALETTE = [
  'var(--chart-1)',
  'var(--chart-2)',
  'var(--chart-3)',
  'var(--chart-4)',
  'var(--chart-5)',
];

function hash(value: string): number {
  let h = 0;
  for (let i = 0; i < value.length; i += 1) {
    h = (h * 31 + value.charCodeAt(i)) >>> 0;
  }
  return h;
}

function deriveSymbol(name: string): string {
  return (name || '?')
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0])
    .join('')
    .toUpperCase()
    .slice(0, 2);
}

function brandColor(name: string): string {
  return BRAND_PALETTE[hash(name) % BRAND_PALETTE.length];
}

function formatMoney(value: number, currency: string): string {
  const symbols: Record<string, string> = { GBP: '£', NGN: '₦', USD: '$', EUR: '€' };
  const prefix = symbols[currency] ?? `${currency} `;
  return `${prefix}${value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

function formatRef(orderId?: string | null): string {
  if (!orderId) {
    return `ORD-${new Date().toISOString().slice(0, 10).replace(/-/g, '')}-DRAFT`;
  }
  const compact = orderId.replace(/-/g, '').slice(0, 8).toUpperCase();
  return `ORD-${compact}`;
}

export interface OrderCartProps {
  order: BillPaymentOrderResponse | null;
  payerName?: string;
  /** Disabled until a draft has been opened or items exist. */
  canSubmit: boolean;
  isSubmitting: boolean;
  onSubmit: () => void;
  onSaveDraft?: () => void;
  onEditItem: (itemId: string) => void;
  onRemoveItem: (itemId: string) => void;
  onRefreshQuote: (itemId: string) => void;
  /** Disable cart actions when the order is no longer editable. */
  isEditable: boolean;
}

function CartItem({
  item,
  payerName,
  onEdit,
  onRemove,
  onRefresh,
  disabled,
}: {
  item: OrderItemResponse;
  payerName: string;
  onEdit: () => void;
  onRemove: () => void;
  onRefresh: () => void;
  disabled: boolean;
}) {
  return (
    <div className="flex flex-col gap-2 rounded-lg border border-border bg-card p-3">
      <div className="flex items-start gap-2.5">
        <div
          className="flex h-[34px] w-[34px] flex-none items-center justify-center rounded-md font-extrabold text-white"
          style={{
            background: brandColor(item.billerName),
            fontSize: 11,
            letterSpacing: '-0.01em',
          }}
        >
          {deriveSymbol(item.billerName)}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-1.5">
            <span className="truncate text-[13px] font-semibold text-foreground">
              {item.billerName}
            </span>
            <Pill tone="info">
              Bill payment
            </Pill>
            {item.isQuoteExpired && (
              <Pill tone="danger">
                Quote expired
              </Pill>
            )}
          </div>
          <div className="mt-0.5 truncate text-[11.5px] text-muted-foreground">
            {item.serviceName}
          </div>
        </div>
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              onClick={onRemove}
              disabled={disabled}
              aria-label="Remove item"
              className="size-6 flex-none text-muted-foreground hover:text-destructive"
            >
              <Trash2 className="h-3 w-3" />
            </Button>
          </TooltipTrigger>
          <TooltipContent>Remove item</TooltipContent>
        </Tooltip>
      </div>

      <div className="flex flex-wrap items-center gap-1.5 text-[11.5px] text-muted-foreground">
        <PartyAvatar name={payerName || '—'} size={20} />
        <span className="truncate">{payerName || '—'}</span>
        <ArrowRight className="h-2.5 w-2.5 text-muted-foreground" />
        <PartyAvatar name={item.receiverName || '—'} size={20} />
        <span className="truncate">{item.receiverName || '—'}</span>
      </div>

      <div className="flex items-center justify-between gap-3 border-t border-border pt-2">
        <div>
          <div className="text-[11px] text-muted-foreground">Amount</div>
          <div className="font-mono tabular-nums text-[14px] font-bold text-foreground">
            {formatMoney(item.amountIn, item.currencyIn)}
          </div>
        </div>
        {item.currencyOut !== item.currencyIn && (
          <div className="text-right">
            <div className="text-[11px] text-muted-foreground">Receive</div>
            <div className="font-mono tabular-nums text-[13px] font-semibold text-primary">
              {formatMoney(item.amountOut, item.currencyOut)}
            </div>
          </div>
        )}
        <div className="text-right">
          <div className="text-[11px] text-muted-foreground">Fee</div>
          <div className="font-mono tabular-nums text-[12px] text-muted-foreground">
            {formatMoney(item.feesTotal, item.currencyIn)}
          </div>
        </div>
      </div>

      <div className="flex items-center justify-end gap-1.5">
        {item.isQuoteExpired && (
          <Button size="sm" variant="outline" onClick={onRefresh} disabled={disabled}>
            <RefreshCw className="h-3 w-3" />
            Refresh
          </Button>
        )}
        <Button size="sm" variant="outline" onClick={onEdit} disabled={disabled}>
          Edit
        </Button>
      </div>
    </div>
  );
}

export function OrderCart({
  order,
  payerName,
  canSubmit,
  isSubmitting,
  onSubmit,
  onSaveDraft,
  onEditItem,
  onRemoveItem,
  onRefreshQuote,
  isEditable,
}: OrderCartProps) {
  const items = order?.items ?? [];

  // Per-currency totals (matches the template's per-currency rows).
  const totalsByCurrency = items.reduce<Record<string, { amount: number; fee: number }>>((acc, item) => {
    const cur = item.currencyIn;
    if (!acc[cur]) acc[cur] = { amount: 0, fee: 0 };
    acc[cur].amount += item.amountIn;
    acc[cur].fee += item.feesTotal;
    return acc;
  }, {});
  const currencies = Object.entries(totalsByCurrency);

  // Policy banner: single-currency drafts under 50 000 in their unit pass.
  const totalAmount = order?.totalAmountIn ?? 0;
  const withinPolicy = totalAmount > 0 && totalAmount < 50_000;

  return (
    <div className="flex h-full flex-col overflow-hidden bg-muted">
      <div className="flex-none border-b border-border px-5 py-4">
        <div className="flex items-center justify-between">
          <div className="text-[14px] font-semibold text-foreground">Order</div>
          <div className="flex items-center gap-1.5">
            <span
              className={
                'grid h-[22px] w-[22px] place-items-center rounded-full font-mono text-[11px] font-bold tabular-nums ' +
                (items.length > 0
                  ? 'bg-primary text-primary-foreground'
                  : 'bg-muted-foreground text-background')
              }
            >
              {items.length}
            </span>
            <span className="text-[12.5px] text-muted-foreground">items</span>
          </div>
        </div>
        <div className="mt-1 font-mono tabular-nums text-[10.5px] text-muted-foreground">
          {formatRef(order?.orderId)}
        </div>
      </div>

      <div className="flex flex-1 flex-col gap-2.5 overflow-auto px-4 py-3.5">
        {items.length === 0 && (
          <div className="flex flex-1 flex-col items-center justify-center gap-3 px-8 py-10 text-center">
            <div className="grid h-12 w-12 place-items-center rounded-xl border border-dashed border-border bg-card">
              <ShieldCheck className="h-5 w-5 text-muted-foreground" />
            </div>
            <div>
              <div className="text-[13px] font-medium text-muted-foreground">No items yet</div>
              <div className="mt-1 text-[12px] text-muted-foreground">
                Configure an item on the left, then click <span className="font-medium">Add to order</span>.
              </div>
            </div>
          </div>
        )}
        {items.map((item) => (
          <CartItem
            key={item.orderItemId}
            item={item}
            payerName={payerName ?? order?.payerName ?? ''}
            onEdit={() => onEditItem(item.orderItemId)}
            onRemove={() => onRemoveItem(item.orderItemId)}
            onRefresh={() => onRefreshQuote(item.orderItemId)}
            disabled={!isEditable}
          />
        ))}
      </div>

      {items.length > 0 && (
        <div className="flex-none space-y-3 border-t border-border bg-card px-4 py-3.5">
          <div className="flex flex-col gap-1.5">
            {currencies.map(([cur, totals]) => (
              <div key={cur} className="flex items-center justify-between text-[12.5px]">
                <span className="text-muted-foreground">{cur} total</span>
                <span className="font-mono tabular-nums font-semibold text-foreground">
                  {formatMoney(totals.amount, cur)}
                </span>
              </div>
            ))}
            {currencies.map(([cur, totals]) => (
              <div key={`${cur}-fee`} className="flex items-center justify-between text-[12px]">
                <span className="text-muted-foreground">Est. fees ({cur})</span>
                <span className="font-mono tabular-nums text-muted-foreground">
                  {formatMoney(totals.fee, cur)}
                </span>
              </div>
            ))}
          </div>

          <Alert
            variant={withinPolicy ? 'default' : 'warning'}
            className={'px-3 py-2 ' + (withinPolicy ? 'border-primary bg-primary/10 text-primary' : '')}
          >
            <ShieldCheck />
            <AlertDescription
              className={'text-[11.5px] leading-tight ' + (withinPolicy ? 'text-primary' : '')}
            >
              {withinPolicy
                ? 'Within auto-apply policy ceiling.'
                : 'Exceeds policy threshold — manual approval may be required.'}
            </AlertDescription>
          </Alert>

          <div className="flex gap-2">
            {onSaveDraft && (
              <Button variant="outline" size="sm" className="flex-none" onClick={onSaveDraft}>
                Save draft
              </Button>
            )}
            <Button
              className="flex-1 justify-center"
              onClick={onSubmit}
              disabled={!canSubmit || isSubmitting}
            >
              <ShieldCheck className="h-3.5 w-3.5" />
              {isSubmitting ? 'Submitting…' : 'Submit order'}
            </Button>
          </div>

          <div className="text-center text-[11px] text-muted-foreground">
            <span className="font-mono tabular-nums">{items.length}</span> item{items.length === 1 ? '' : 's'} · compliance checks run on submit
          </div>
        </div>
      )}
    </div>
  );
}
