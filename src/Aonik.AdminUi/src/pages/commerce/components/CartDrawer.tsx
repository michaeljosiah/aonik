// Cart drawer (Spec 083 §3). The footer actions are the point: the UI must never offer an
// operation the Spec 068 rules block, so "Resume checkout" is rendered as a DISABLED
// "Checkout blocked" carrying the reason whenever the box is drifted or under-filled — the
// same verdict, from the same pure function, that drives the list column and the banner.

import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertTriangle } from 'lucide-react';
import { toast } from 'sonner';

import { Card as AonikCard, Pill } from '@/components/layout/aonik';
import { Button } from '@/components/ui/button';
import { Sheet, SheetBody, SheetContent, SheetFooter, SheetHeader } from '@/components/ui/sheet';
import { commerceStorefrontService } from '@/services/commerceStorefrontService';
import { formatCurrency, formatDateTime } from '@/lib/format';
import type { AdminCartDetailDto, AdminCartLineDto } from '@/types/commerce';

import { BuyerLabel } from './BuyerLabel';
import { cartAction, cartBlocked } from '../lib/cartState';
import { cartStatusTone } from '../lib/statusTone';

export function CartDrawer({ cartId, onClose }: { cartId: string; onClose: () => void }) {
  const navigate = useNavigate();
  const [cart, setCart] = useState<AdminCartDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setCart(await commerceStorefrontService.getCart(cartId));
    } catch (err: unknown) {
      setCart(null);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'This cart could not be loaded.');
    } finally {
      setLoading(false);
    }
  }, [cartId]);

  useEffect(() => {
    void load();
  }, [load]);

  const verdict = cartBlocked(cart?.boxMeta);
  const action = cart
    ? cartAction({ status: cart.status, orderId: cart.orderId, boxMeta: cart.boxMeta })
    : ({ kind: 'none' } as const);

  return (
    <Sheet open onOpenChange={(open) => !open && onClose()}>
      <SheetContent size="md">
        <SheetHeader title="Cart" subtitle={cart ? cart.cartId : cartId} />

        <SheetBody>
          {loading ? (
            <p className="py-8 text-center text-sm text-[var(--color-text-secondary)]">Loading…</p>
          ) : !cart ? (
            <div className="flex flex-col items-center gap-3 py-10">
              <p className="text-sm text-[var(--color-text-secondary)]">
                {error ?? 'This cart could not be loaded.'}
              </p>
              <Button variant="outline" onClick={() => void load()}>
                Try again
              </Button>
            </div>
          ) : (
            <div className="flex flex-col gap-4">
              {action.kind === 'view-order' && action.note && (
                // Why the cart cannot be resumed even though it looks complete. Without this
                // the disappearance of the resume action would read as a bug.
                <p className="rounded-md border border-[var(--color-border)] bg-[var(--color-surface-inset)] px-3 py-2 text-[12px] text-[var(--color-text-secondary)]">
                  {action.note}
                </p>
              )}

              {verdict.blocked && (
                <div className="flex items-start gap-2 rounded-md border border-[var(--color-warning)] bg-[var(--color-warning-light)] px-3 py-2">
                  <AlertTriangle className="mt-px h-4 w-4 shrink-0 text-[var(--color-warning)]" />
                  <p className="text-[12px] text-[var(--color-warning)]">
                    <span className="font-semibold">Checkout blocked.</span> {verdict.reason} The
                    customer resolves this on their next visit — a full box with every line
                    available is what checkout requires.
                  </p>
                </div>
              )}

              <AonikCard padding={12}>
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <BuyerLabel buyerKind={cart.buyerKind} buyerPartyId={cart.buyerPartyId} />
                  <Pill tone={cartStatusTone(cart.status)}>{cart.status}</Pill>
                </div>
                <div className="mt-2 flex flex-wrap gap-x-5 gap-y-1 text-[11.5px] text-[var(--color-text-tertiary)]">
                  {cart.boxMeta && (
                    <span>
                      Box{' '}
                      <span className="font-[family-name:var(--font-mono)] text-[var(--color-text-secondary)]">
                        {cart.boxMeta.filled}/{cart.boxMeta.size}
                      </span>
                    </span>
                  )}
                  <span>Last activity {formatDateTime(cart.updatedAtUtc)}</span>
                  <span>
                    Total{' '}
                    <span className="font-[family-name:var(--font-mono)] text-[var(--color-text-primary)]">
                      {formatCurrency(cart.total, cart.currency)}
                    </span>
                  </span>
                </div>
              </AonikCard>

              <AonikCard
                title="Lines"
                subtitle="Add-on prices are the retail base; personalisation and surcharges are applied to the cart total, not carried per line by this read"
                padding={12}
              >
                {cart.lines.length === 0 ? (
                  <p className="py-2 text-[12.5px] text-[var(--color-text-secondary)]">
                    This cart is empty.
                  </p>
                ) : (
                  <div className="flex flex-col divide-y divide-[var(--color-border-light)]">
                    {cart.lines.map((line) => (
                      <CartLineRow key={line.lineId} line={line} currency={cart.currency} />
                    ))}
                  </div>
                )}
              </AonikCard>
            </div>
          )}
        </SheetBody>

        {/* ONE action, from one derivation — see `cartAction`. Deciding it here from status
            alone is what let a claimed-but-Open cart offer a resume the server refuses. */}
        <SheetFooter>
          {action.kind === 'recover' && (
            // Recovery has no backend flow for commerce yet; the action renders and says so
            // rather than pretending to send something.
            <Button
              variant="outline"
              onClick={() => toast.info('Cart recovery is not wired yet — no message was sent.')}
            >
              Send recovery link
            </Button>
          )}

          {action.kind === 'blocked' && (
            <Button variant="outline" disabled title={action.reason}>
              Checkout blocked
            </Button>
          )}

          {action.kind === 'resume' && (
            <Button
              variant="outline"
              onClick={() =>
                toast.info('Checkout is the customer’s action — this cart is ready for it.')
              }
            >
              Resume checkout
            </Button>
          )}

          {action.kind === 'view-order' && (
            <Button
              variant="outline"
              onClick={() => navigate(`/commerce/orders/${action.orderId}`)}
            >
              View order
            </Button>
          )}

          <Button onClick={onClose}>Close</Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

function CartLineRow({ line, currency }: { line: AdminCartLineDto; currency: string }) {
  return (
    <div className="py-2.5">
      <div className="flex items-start justify-between gap-3">
        <div className="flex min-w-0 flex-col gap-0.5">
          <span className="flex flex-wrap items-center gap-1.5">
            <span className="text-[13px] text-[var(--color-text-primary)]">{line.name}</span>
            {line.kind === 'AddOn' && (
              <Pill tone="info" size="sm">
                ADD-ON
              </Pill>
            )}
            {line.isUnavailable && (
              <Pill tone="warning" size="sm">
                Unavailable
              </Pill>
            )}
            {line.priceChanged && (
              <Pill tone="warning" size="sm">
                Repriced
              </Pill>
            )}
          </span>
          <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">
            {line.sku}
          </span>
          {line.personalisationSummary && (
            <span className="text-[11.5px] text-[var(--color-brand-primary)]">
              {line.personalisationSummary}
            </span>
          )}
          {line.selectionDrift.length > 0 && (
            <ul className="mt-0.5 flex flex-col gap-0.5">
              {line.selectionDrift.map((drift, index) => (
                <li key={`${drift.groupKey}-${index}`} className="text-[11px] text-[var(--color-warning)]">
                  {drift.groupKey}: {drift.reason}
                </li>
              ))}
            </ul>
          )}
        </div>

        {/* NO computed line total. Checkout charges
            (snapshot + personalisationAdjustment + unitSurcharge) × quantity, and this read
            carries only the snapshot — multiplying it out would print a confident number
            lower than the cart actually charges. A BoxDish snapshot is 0 by design (the box
            is priced as a container), so even the basis is meaningless there. The cart's own
            total, which IS authoritative, is shown on the card above. */}
        <div className="flex shrink-0 flex-col items-end">
          <span className="font-[family-name:var(--font-mono)] text-[12.5px] tabular-nums text-[var(--color-text-secondary)]">
            ×{line.quantity}
          </span>
          {line.kind === 'AddOn' && (
            <span className="font-[family-name:var(--font-mono)] text-[11px] tabular-nums text-[var(--color-text-tertiary)]">
              {formatCurrency(line.unitPriceSnapshot, currency)} base
            </span>
          )}
          {line.kind === 'BoxDish' && (
            <span className="text-[11px] text-[var(--color-text-tertiary)]">priced by the box</span>
          )}
        </div>
      </div>

      {line.components.length > 0 && (
        <ul className="mt-2 flex flex-col gap-1 border-l border-[var(--color-border-light)] pl-3">
          {line.components.map((component, index) => (
            <li key={`${component.sku}-${index}`} className="flex items-center gap-2">
              <span className="font-[family-name:var(--font-mono)] text-[11px] tabular-nums text-[var(--color-text-tertiary)]">
                {component.quantity}×
              </span>
              <span className="text-[12px] text-[var(--color-text-primary)]">{component.name}</span>
              {component.isUnavailable && (
                <Pill tone="warning" size="sm">
                  Unavailable
                </Pill>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
