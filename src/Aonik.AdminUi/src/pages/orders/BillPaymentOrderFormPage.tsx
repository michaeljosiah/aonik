// New-order builder — visual port of `ScreenCreateOrder` from
// templates/aonik-admin-starterkit/screens/orders.jsx.
//
// Shape (template, 1:1):
//   • Outer: 2-column grid `1fr 380px`, full height, no separate header bar.
//   • Left pane owns the eyebrow / title / subtitle / Draft pill / mode tabs
//     in a top section (padding 18 24 0), then a scrolling form body
//     (padding 0 24 24).
//   • Right pane is the cart, flush with `surface-inset` background.
//
// Service wiring is preserved from the previous form — catalog, pricing,
// validation, party, and reference-data services. The page owns the
// `BillPaymentFormState` and `BillPaymentOrderResponse` and threads them
// into the sub-components (BillPaymentForm, OrderCart) as props.

import { useCallback, useEffect, useState } from 'react';
import { AlertCircle, Receipt, Send } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';

import { Pill } from '@/components/layout/aonik';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { orderService } from '@/services/orderService';
import { pricingService } from '@/services/pricingService';
import type {
  BillPaymentOrderResponse,
  CreateBillPaymentItemRequest,
  UpdateBillPaymentItemRequest,
} from '@/types';

import { BillPaymentForm, createEmptyFormState, type BillPaymentFormState } from './BillPaymentForm';
import { MoneyTransferForm } from './MoneyTransferForm';
import { OrderCart } from './OrderCart';

type Mode = 'bill' | 'transfer';

function buildItemPayload(state: BillPaymentFormState): CreateBillPaymentItemRequest {
  if (!state.pricingQuote) {
    throw new Error('Pricing quote is required.');
  }
  const sameAsPayer = !state.receiverPartyId || state.receiverPartyId === state.payerPartyId;
  return {
    billerId: state.selectedBillerId,
    serviceId: state.selectedServiceId,
    serviceCode: state.serviceCode,
    serviceFieldValues: state.serviceFieldValues,
    receiverPartyId: state.receiverPartyId || state.payerPartyId,
    relationshipTypeCode: sameAsPayer ? 'Self' : undefined,
    originAmount: state.pricingQuote.originAmount,
    destinationAmount: state.pricingQuote.destinationAmount,
    destinationCurrency: state.destinationCurrency.toUpperCase(),
    destinationCountry: state.destinationCountry.toUpperCase(),
    pricingQuoteId: state.pricingQuote.pricingQuoteId,
    purposeCode: state.purposeCode || undefined,
  };
}

function buildItemUpdate(state: BillPaymentFormState): UpdateBillPaymentItemRequest {
  if (!state.pricingQuote) {
    throw new Error('Pricing quote is required.');
  }
  return {
    serviceFieldValues: state.serviceFieldValues,
    receiverPartyId: state.receiverPartyId || undefined,
    pricingQuoteId: state.pricingQuote.pricingQuoteId,
    originAmount: state.pricingQuote.originAmount,
    destinationAmount: state.pricingQuote.destinationAmount,
    purposeCode: state.purposeCode || undefined,
  };
}

export function BillPaymentOrderFormPage() {
  const navigate = useNavigate();
  const { orderId } = useParams();

  const [order, setOrder] = useState<BillPaymentOrderResponse | null>(null);
  const [orderLoading, setOrderLoading] = useState(false);
  const [orderError, setOrderError] = useState<string | null>(null);

  const [mode, setMode] = useState<Mode>('bill');
  const [formState, setFormState] = useState<BillPaymentFormState>(() => createEmptyFormState());
  const [editingItemId, setEditingItemId] = useState<string | null>(null);
  const [isSavingItem, setIsSavingItem] = useState(false);
  const [isSubmittingOrder, setIsSubmittingOrder] = useState(false);

  const loadOrder = useCallback(async () => {
    if (!orderId) return;
    setOrderLoading(true);
    setOrderError(null);
    try {
      const result = await orderService.getOrder(orderId);
      setOrder(result);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setOrderError(message || 'Failed to load order.');
    } finally {
      setOrderLoading(false);
    }
  }, [orderId]);

  useEffect(() => {
    if (orderId) void loadOrder();
  }, [orderId, loadOrder]);

  // When an order is loaded, sync the order-level fields into the form state.
  useEffect(() => {
    if (!order) return;
    setFormState((prev) => ({
      ...prev,
      payerPartyId: prev.payerPartyId || order.payerPartyId,
      payerOption:
        prev.payerOption && prev.payerOption.partyId === order.payerPartyId
          ? prev.payerOption
          : { partyId: order.payerPartyId, displayName: order.payerName, partyType: '' },
      originCountry: order.originCountry || prev.originCountry,
      originCurrency: order.originCurrency || prev.originCurrency,
      purposeCode: prev.purposeCode || order.purposeCode || '',
    }));
  }, [order]);

  const ensureOrder = useCallback(
    async (state: BillPaymentFormState): Promise<BillPaymentOrderResponse> => {
      if (order) return order;
      if (!state.payerPartyId) {
        throw new Error('Payer is required.');
      }
      const created = await orderService.createBillPaymentOrder({
        payerPartyId: state.payerPartyId,
        originCountry: (state.originCountry || 'GB').toUpperCase(),
        originCurrency: (state.originCurrency || 'GBP').toUpperCase(),
        purposeCode: state.purposeCode || null,
        notes: null,
        items: null,
      });
      setOrder(created);
      if (!orderId) navigate(`/orders/bill-payments/${created.orderId}`);
      return created;
    },
    [order, orderId, navigate],
  );

  const handleFormChange = (next: Partial<BillPaymentFormState>) => {
    setFormState((prev) => ({ ...prev, ...next }));
  };

  const handleAddItem = async () => {
    setIsSavingItem(true);
    setOrderError(null);
    try {
      const activeOrder = await ensureOrder(formState);
      if (editingItemId) {
        await orderService.updateBillPaymentItem(
          activeOrder.orderId,
          editingItemId,
          buildItemUpdate(formState),
        );
      } else {
        await orderService.addBillPaymentItem(activeOrder.orderId, buildItemPayload(formState));
      }
      const refreshed = await orderService.getOrder(activeOrder.orderId);
      setOrder(refreshed);
      setEditingItemId(null);
      setFormState((prev) => ({
        ...prev,
        receiverPartyId: '',
        receiverOption: null,
        selectedBillerId: '',
        selectedServiceId: '',
        serviceCode: '',
        serviceFieldValues: {},
        validationResult: null,
        amountValue: '',
        pricingQuote: null,
      }));
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : (err as Error)?.message ?? '';
      setOrderError(message || 'Unable to save the item.');
    } finally {
      setIsSavingItem(false);
    }
  };

  const handleEditItem = (itemId: string) => {
    const item = order?.items.find((i) => i.orderItemId === itemId);
    if (!item) return;
    setEditingItemId(itemId);
    setFormState((prev) => ({
      ...prev,
      receiverPartyId: item.receiverPartyId,
      receiverOption: { partyId: item.receiverPartyId, displayName: item.receiverName, partyType: '' },
      selectedBillerId: item.billerId,
      selectedServiceId: item.serviceId,
      serviceCode: item.serviceCode,
      destinationCurrency: item.currencyOut,
      serviceFieldValues: item.serviceFieldValues ?? {},
      validationResult: null,
      amountValue: item.amountOut.toString(),
      pricingQuote: null,
    }));
  };

  const handleCancelEdit = () => {
    setEditingItemId(null);
  };

  const handleRemoveItem = async (itemId: string) => {
    if (!order) return;
    try {
      await orderService.removeBillPaymentItem(order.orderId, itemId);
      const refreshed = await orderService.getOrder(order.orderId);
      setOrder(refreshed);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setOrderError(message || 'Unable to remove item.');
    }
  };

  const handleRefreshQuote = async (itemId: string) => {
    if (!order) return;
    const item = order.items.find((i) => i.orderItemId === itemId);
    if (!item) return;
    try {
      const quote = await pricingService.getQuote({
        originCurrency: order.originCurrency,
        destinationCurrency: item.currencyOut,
        originCountry: order.originCountry,
        destinationCountry: order.originCountry,
        serviceCode: item.serviceCode,
        destinationAmount: item.amountOut,
        originAmount: item.amountIn,
        customerId: order.payerPartyId,
        quoteContext: 'BillPayment',
      });
      await orderService.updateBillPaymentItem(order.orderId, itemId, {
        pricingQuoteId: quote.pricingQuoteId,
      });
      const refreshed = await orderService.getOrder(order.orderId);
      setOrder(refreshed);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setOrderError(message || 'Unable to refresh quote.');
    }
  };

  const handleSubmitOrder = async () => {
    if (!order) return;
    setIsSubmittingOrder(true);
    try {
      const result = await orderService.submitOrder(order.orderId);
      setOrder(result);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setOrderError(message || 'Unable to submit order.');
    } finally {
      setIsSubmittingOrder(false);
    }
  };

  const isEditable = !order || order.status === 'Draft';
  const hasExpiredItems = (order?.items ?? []).some((item) => item.isQuoteExpired);
  const canSubmit = !!order && (order.items.length ?? 0) > 0 && !hasExpiredItems && isEditable;

  if (orderLoading) {
    return (
      <div className="flex h-full items-center justify-center text-[13px] text-[var(--color-text-secondary)]">
        Loading order…
      </div>
    );
  }

  return (
    <div className="grid h-full min-h-0 grid-cols-1 lg:grid-cols-[minmax(0,1fr)_380px]">
      {/* ── Left: builder ── */}
      <div className="flex min-h-0 flex-col overflow-auto border-b border-[var(--color-border-light)] lg:border-b-0 lg:border-r">
        {/* Top section: eyebrow / title / subtitle / Draft pill / mode tabs */}
        <div className="flex-none px-6 pt-[18px]">
          <div className="text-[10px] font-semibold uppercase tracking-[0.1em] text-[var(--color-text-tertiary)]">
            Orders · New order
          </div>

          <div className="mt-1 mb-4 flex items-end justify-between gap-4">
            <div className="min-w-0">
              <div className="text-[22px] font-bold leading-tight tracking-[-0.01em] text-[var(--color-text-primary)]">
                Create order
              </div>
              <div className="mt-0.5 text-[13px] text-[var(--color-text-secondary)]">
                Build a multi-item order — mix bill payments and money transfers in one submission.
              </div>
            </div>
            <Pill tone="pending" dot>
              {order ? `Draft · ${order.orderId.slice(0, 8).toUpperCase()}` : 'Draft'}
            </Pill>
          </div>

          {/* Mode tabs — fit-content, surface-inset bg, padding 4, radius 10 */}
          <div className="mb-5 flex w-fit items-center gap-0 rounded-[10px] bg-[var(--color-surface-inset)] p-1">
            {([
              { value: 'bill' as Mode, label: 'Bill payment', icon: Receipt },
              { value: 'transfer' as Mode, label: 'Money transfer', icon: Send },
            ]).map((tab) => {
              const active = mode === tab.value;
              return (
                <button
                  key={tab.value}
                  type="button"
                  onClick={() => setMode(tab.value)}
                  className={cn(
                    'flex items-center gap-1.5 rounded-[7px] px-4 py-[7px] text-[13px] font-medium transition-all',
                    active
                      ? 'bg-[var(--color-surface)] text-[var(--color-text-primary)] shadow-[0_1px_3px_rgb(0_0_0/_0.08)]'
                      : 'bg-transparent text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]',
                  )}
                >
                  <tab.icon
                    className="h-3.5 w-3.5"
                    style={{ color: active ? 'var(--color-brand-primary)' : 'currentColor' }}
                  />
                  {tab.label}
                </button>
              );
            })}
          </div>
        </div>

        {/* Page-level error (no inline banner inside form) */}
        {orderError && (
          <div className="mx-6 mb-3 flex items-center gap-2 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-[12.5px] text-[var(--color-error)]">
            <AlertCircle className="h-3.5 w-3.5 flex-none" />
            <span className="flex-1">{orderError}</span>
            <Button variant="outline" size="sm" onClick={() => void loadOrder()}>
              Retry
            </Button>
          </div>
        )}

        {/* Form body — overflow-auto, pad 0 24 24 */}
        <div className="flex-1 overflow-auto px-6 pb-6">
          {mode === 'bill' ? (
            <BillPaymentForm
              state={formState}
              onChange={handleFormChange}
              onAddItem={handleAddItem}
              onCancelEdit={editingItemId ? handleCancelEdit : undefined}
              isEditing={!!editingItemId}
              isSavingItem={isSavingItem}
              disabled={!isEditable}
            />
          ) : (
            <MoneyTransferForm />
          )}
        </div>
      </div>

      {/* ── Right: cart ── */}
      <div className="min-h-0">
        <OrderCart
          order={order}
          payerName={formState.payerOption?.displayName ?? order?.payerName}
          canSubmit={canSubmit}
          isSubmitting={isSubmittingOrder}
          onSubmit={handleSubmitOrder}
          onEditItem={handleEditItem}
          onRemoveItem={handleRemoveItem}
          onRefreshQuote={handleRefreshQuote}
          isEditable={isEditable}
        />
      </div>
    </div>
  );
}
