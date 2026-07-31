// Order line items (Spec 083 §4) — the kitchen landing.
//
// The box aggregate is ONE order item at the goods total; what the kitchen actually prepares
// lives in the separate `selections` array, nested under its item by `orderItemIndex`. That
// nesting is the whole point: Spec 068 snapshots each selection with its personalisation so
// two differently-personalised preparations can never collapse into one line.
//
// Personalisation summaries are rendered AS THE DTO CARRIES THEM. The stored envelopes are
// opaque JSON and are never re-derived here (Spec 083 §4) — a client-side reading of them
// would drift from the snapshot the kitchen works to.

import { Pill } from '@/components/layout/aonik';
import { formatCurrency } from '@/lib/format';
import type {
  AdminOrderStorefrontItemDto,
  StorefrontOrderSelectionDto,
} from '@/types/commerce';

interface OrderLineItemsProps {
  items: AdminOrderStorefrontItemDto[];
  selections: StorefrontOrderSelectionDto[];
  currency: string;
}

export function OrderLineItems({ items, selections, currency }: OrderLineItemsProps) {
  if (items.length === 0) {
    return (
      <p className="py-4 text-center text-[12.5px] text-[var(--color-text-secondary)]">
        This order has no items.
      </p>
    );
  }

  // Selections whose index matches no item would otherwise vanish silently; they are shown
  // as unattached rather than dropped, because a kitchen line that exists must be visible.
  const attached = new Set<number>();
  items.forEach((_, index) => attached.add(index));
  const orphaned = selections.filter((s) => !attached.has(s.orderItemIndex));

  return (
    <div className="flex flex-col divide-y divide-[var(--color-border-light)]">
      {items.map((item, index) => (
        <ItemRow
          key={`${item.itemType}-${index}`}
          item={item}
          currency={currency}
          selections={selections.filter((s) => s.orderItemIndex === index)}
        />
      ))}

      {orphaned.length > 0 && (
        <div className="py-2.5">
          <p className="mb-1.5 text-[11px] text-[var(--color-warning)]">
            {orphaned.length} preparation {orphaned.length === 1 ? 'line' : 'lines'} could not be
            matched to an order item — shown so nothing the kitchen holds is hidden.
          </p>
          <SelectionList selections={orphaned} />
        </div>
      )}
    </div>
  );
}

function ItemRow({
  item,
  selections,
  currency,
}: {
  item: AdminOrderStorefrontItemDto;
  selections: StorefrontOrderSelectionDto[];
  currency: string;
}) {
  return (
    <div className="py-2.5">
      <div className="flex items-start justify-between gap-3">
        <div className="flex min-w-0 flex-col gap-0.5">
          <span className="flex flex-wrap items-center gap-1.5">
            <span className="text-[13px] text-[var(--color-text-primary)]">{item.name}</span>
            {item.isAddOn && (
              <Pill tone="info" size="sm">
                ADD-ON
              </Pill>
            )}
            {item.isDeliveryFee && (
              <Pill tone="muted" size="sm">
                DELIVERY
              </Pill>
            )}
          </span>

          {item.sku && (
            <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">
              {item.sku}
            </span>
          )}

          {item.isAddOn && (
            <span className="text-[11px] text-[var(--color-text-tertiary)]">
              retail — no box space
            </span>
          )}
        </div>

        <div className="flex shrink-0 flex-col items-end">
          <span className="font-[family-name:var(--font-mono)] text-[12.5px] tabular-nums text-[var(--color-text-primary)]">
            {formatCurrency(item.amount, currency)}
          </span>
          {item.quantity != null && item.unitPrice != null && (
            <span className="font-[family-name:var(--font-mono)] text-[11px] tabular-nums text-[var(--color-text-tertiary)]">
              {item.quantity} × {formatCurrency(item.unitPrice, currency)}
            </span>
          )}
        </div>
      </div>

      {selections.length > 0 && (
        <div className="mt-2 border-l border-[var(--color-border-light)] pl-3">
          <SelectionList selections={selections} />
        </div>
      )}
    </div>
  );
}

function SelectionList({ selections }: { selections: StorefrontOrderSelectionDto[] }) {
  return (
    <ul className="flex flex-col gap-1.5">
      {selections.map((selection, index) => (
        <li key={`${selection.sku}-${index}`} className="flex items-start gap-2">
          <span className="mt-px font-[family-name:var(--font-mono)] text-[11px] tabular-nums text-[var(--color-text-tertiary)]">
            {selection.quantity}×
          </span>
          <span className="flex min-w-0 flex-col">
            <span className="text-[12.5px] text-[var(--color-text-primary)]">
              {/* SKU is the durable identifier — a variant deleted since checkout has no name,
                  and showing the SKU alone is better than an invented placeholder. */}
              {selection.name ?? selection.sku}
            </span>
            <span className="font-[family-name:var(--font-mono)] text-[10.5px] text-[var(--color-text-tertiary)]">
              {selection.sku}
            </span>
            {selection.personalisationSummary && (
              <span className="text-[11.5px] text-[var(--color-brand-primary)]">
                {selection.personalisationSummary}
              </span>
            )}
          </span>
        </li>
      ))}
    </ul>
  );
}
