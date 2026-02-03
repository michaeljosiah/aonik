import { useCallback, useEffect, useMemo, useState } from 'react';
import { createPortal } from 'react-dom';
import { useNavigate, useParams } from 'react-router-dom';
import {
  AlertCircle,
  ArrowRight,
  CheckCircle2,
  ClipboardList,
  RefreshCw,
  ShieldCheck,
  Trash2,
  UserPlus,
  Users,
  X,
} from 'lucide-react';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { CountrySelect } from '@/components/ui/country-select';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { catalogService } from '@/services/catalogService';
import { pricingService } from '@/services/pricingService';
import { orderService } from '@/services/orderService';
import { referenceDataService } from '@/services/referenceDataService';
import { partyService } from '@/services/partyService';
import type {
  BillPaymentOrderResponse,
  CatalogBillerCategoryItem,
  CatalogBillerServiceDetailResponse,
  CatalogBillerServiceItem,
  CatalogBillerSummaryItem,
  CatalogServiceFieldValidationResponse,
  CreateBillPaymentItemRequest,
  CreatePartyRequest,
  CreateReceiverRequest,
  PartyResponse,
  PricingQuoteResponse,
  ReferenceDataItem,
} from '@/types';

const toCurrency = (value: number, currency: string) =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(value);

const emptyReceiver: CreateReceiverRequest = {
  displayName: '',
  partyType: 'Person',
  firstName: null,
  lastName: null,
  phone: null,
  email: null,
  countryCode: null,
};

const emptyParty: CreatePartyRequest = {
  displayName: '',
  partyType: 'Person',
  firstName: null,
  lastName: null,
  phone: null,
  email: null,
  countryCode: null,
};

function PartyModal({
  title,
  isOpen,
  submitLabel,
  onClose,
  onSubmit,
}: {
  title: string;
  isOpen: boolean;
  submitLabel: string;
  onClose: () => void;
  onSubmit: (payload: CreatePartyRequest) => Promise<void>;
}) {
  const [form, setForm] = useState<CreatePartyRequest>(emptyParty);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isOpen) return;
    setForm(emptyParty);
    setError(null);
  }, [isOpen]);

  if (!isOpen) return null;

  const update = (key: keyof CreatePartyRequest, value: string) => {
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  const handleSubmit = async () => {
    if (!form.displayName.trim()) {
      setError('Display name is required.');
      return;
    }

    setIsSubmitting(true);
    setError(null);
    try {
      await onSubmit({
        ...form,
        displayName: form.displayName.trim(),
        partyType: form.partyType.trim() || 'Person',
      });
      onClose();
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Unable to create the party.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return createPortal(
    <div className="fixed inset-0 z-[120] flex items-center justify-center bg-black/50 p-4">
      <div className="w-[min(92vw,32rem)] rounded-md bg-[var(--color-surface)] border border-[var(--color-border)] shadow-lg">
        <div className="flex items-center justify-between border-b border-[var(--color-border-light)] px-4 py-3">
          <h3 className="text-sm font-semibold text-[var(--color-text-primary)]">{title}</h3>
          <button
            type="button"
            className="rounded-sm p-1 text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)]"
            onClick={onClose}
          >
            <X className="w-4 h-4" />
          </button>
        </div>
        <div className="space-y-4 px-4 py-4 max-h-[70vh] overflow-auto">
          {error && (
            <div className="rounded-sm border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
              {error}
            </div>
          )}
          <label className="grid gap-2 text-xs text-[var(--color-text-secondary)]">
            <span>Display name</span>
            <input
              type="text"
              value={form.displayName}
              onChange={(event) => update('displayName', event.target.value)}
              className="h-10 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 text-sm text-[var(--color-text-primary)]"
            />
          </label>
          <div className="grid gap-4 sm:grid-cols-2">
            <label className="grid gap-2 text-xs text-[var(--color-text-secondary)]">
              <span>Party type</span>
              <Select
                value={form.partyType}
                onValueChange={(value) => update('partyType', value)}
              >
                <SelectTrigger className="h-10 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 text-sm text-[var(--color-text-primary)]">
                  <SelectValue placeholder="Select party type" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Person">Person</SelectItem>
                  <SelectItem value="Business">Business</SelectItem>
                </SelectContent>
              </Select>
            </label>
            <label className="grid gap-2 text-xs text-[var(--color-text-secondary)]">
              <span>Country code</span>
              <input
                type="text"
                value={form.countryCode ?? ''}
                onChange={(event) => update('countryCode', event.target.value)}
                className="h-10 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 text-sm text-[var(--color-text-primary)]"
              />
            </label>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <label className="grid gap-2 text-xs text-[var(--color-text-secondary)]">
              <span>First name</span>
              <input
                type="text"
                value={form.firstName ?? ''}
                onChange={(event) => update('firstName', event.target.value)}
                className="h-10 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 text-sm text-[var(--color-text-primary)]"
              />
            </label>
            <label className="grid gap-2 text-xs text-[var(--color-text-secondary)]">
              <span>Last name</span>
              <input
                type="text"
                value={form.lastName ?? ''}
                onChange={(event) => update('lastName', event.target.value)}
                className="h-10 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 text-sm text-[var(--color-text-primary)]"
              />
            </label>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <label className="grid gap-2 text-xs text-[var(--color-text-secondary)]">
              <span>Email</span>
              <input
                type="email"
                value={form.email ?? ''}
                onChange={(event) => update('email', event.target.value)}
                className="h-10 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 text-sm text-[var(--color-text-primary)]"
              />
            </label>
            <label className="grid gap-2 text-xs text-[var(--color-text-secondary)]">
              <span>Phone</span>
              <input
                type="text"
                value={form.phone ?? ''}
                onChange={(event) => update('phone', event.target.value)}
                className="h-10 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 text-sm text-[var(--color-text-primary)]"
              />
            </label>
          </div>
        </div>
        <div className="flex items-center justify-end gap-2 border-t border-[var(--color-border-light)] px-4 py-3">
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button onClick={handleSubmit} disabled={isSubmitting}>
            {isSubmitting ? 'Saving...' : submitLabel}
          </Button>
        </div>
      </div>
    </div>,
    document.body
  );
}

export function BillPaymentOrderFormPage() {
  const navigate = useNavigate();
  const { orderId } = useParams();

  const [order, setOrder] = useState<BillPaymentOrderResponse | null>(null);
  const [orderLoading, setOrderLoading] = useState(false);
  const [orderError, setOrderError] = useState<string | null>(null);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  const [categories, setCategories] = useState<CatalogBillerCategoryItem[]>([]);
  const [billers, setBillers] = useState<CatalogBillerSummaryItem[]>([]);
  const [services, setServices] = useState<CatalogBillerServiceItem[]>([]);
  const [serviceDetail, setServiceDetail] = useState<CatalogBillerServiceDetailResponse | null>(null);

  const [destinationCountry, setDestinationCountry] = useState('');
  const [categoryId, setCategoryId] = useState('');
  const [billerSearch, setBillerSearch] = useState('');
  const [selectedBillerId, setSelectedBillerId] = useState('');
  const [selectedServiceId, setSelectedServiceId] = useState('');
  const [serviceCode, setServiceCode] = useState('');
  const [destinationCurrency, setDestinationCurrency] = useState('');

  const [payerPartyId, setPayerPartyId] = useState('');
  const [payerParty, setPayerParty] = useState<PartyResponse | null>(null);
  const [customerTier, setCustomerTier] = useState('');
  const [serviceFieldValues, setServiceFieldValues] = useState<Record<string, string>>({});
  const [validationResult, setValidationResult] = useState<CatalogServiceFieldValidationResponse | null>(null);
  const [isValidating, setIsValidating] = useState(false);

  const [originCountry, setOriginCountry] = useState('');
  const [originCurrency, setOriginCurrency] = useState('');
  const [amountMode, setAmountMode] = useState<'origin' | 'destination'>('destination');
  const [amountValue, setAmountValue] = useState('');
  const [quoteContext, setQuoteContext] = useState('BillPayment');
  const [fundingSourceRef, setFundingSourceRef] = useState('');
  const [pricingQuote, setPricingQuote] = useState<PricingQuoteResponse | null>(null);
  const [isQuoting, setIsQuoting] = useState(false);

  const [receiverMode, setReceiverMode] = useState<'same' | 'existing' | 'new'>('same');
  const [receiverPartyId, setReceiverPartyId] = useState('');
  const [receiverDraft, setReceiverDraft] = useState<CreateReceiverRequest>(emptyReceiver);
  const [relationshipType, setRelationshipType] = useState('');
  const [purposeCode, setPurposeCode] = useState('');
  const [notes, setNotes] = useState('');
  const [complianceNotes, setComplianceNotes] = useState('');

  const [relationshipTypes, setRelationshipTypes] = useState<ReferenceDataItem[]>([]);
  const [purposeCodes, setPurposeCodes] = useState<ReferenceDataItem[]>([]);

  const [editingItemId, setEditingItemId] = useState<string | null>(null);
  const [isSavingItem, setIsSavingItem] = useState(false);
  const [isSubmittingOrder, setIsSubmittingOrder] = useState(false);

  const [payerModalOpen, setPayerModalOpen] = useState(false);
  const [receiverModalOpen, setReceiverModalOpen] = useState(false);

  const breadcrumbItems = useMemo(() => ([
    { label: 'Orders', href: '/orders/bill-payments/new' },
    { label: 'Bill Payments', icon: <ClipboardList className="w-3.5 h-3.5" /> },
  ]), []);

  const selectedBiller = useMemo(
    () => billers.find((biller) => biller.billerId === selectedBillerId) ?? null,
    [billers, selectedBillerId]
  );

  const selectedService = useMemo(
    () => services.find((service) => service.serviceId === selectedServiceId) ?? null,
    [services, selectedServiceId]
  );

  const requiresValidation = Boolean(serviceDetail?.requiresValidation);
  const validationPassed = !requiresValidation || validationResult?.isValid;

  const orderItems = order?.items ?? [];
  const isDraftEditable = !order || order.status === 'Draft';
  const orderTotals = useMemo(() => {
    const amountIn = orderItems.reduce((sum, item) => sum + item.amountIn, 0);
    const fees = orderItems.reduce((sum, item) => sum + item.feesTotal, 0);
    const amountOut = orderItems.reduce((sum, item) => sum + item.amountOut, 0);
    return { amountIn, fees, amountOut };
  }, [orderItems]);

  const hasExpiredItems = orderItems.some((item) => item.isQuoteExpired);

  const previewReceiverName = receiverMode === 'same'
    ? (payerParty?.displayName ?? 'Same as payer')
    : receiverMode === 'existing'
      ? receiverPartyId || 'Select receiver'
      : receiverDraft.displayName || 'New receiver';

  const previewItem = useMemo(() => ({
    billerName: selectedBiller?.name ?? 'Select biller',
    serviceName: selectedService?.name ?? 'Select service',
    receiverName: previewReceiverName,
    payerName: payerParty?.displayName ?? (payerPartyId || 'Select payer'),
    quote: pricingQuote,
    fieldsSummary: Object.entries(serviceFieldValues)
      .slice(0, 2)
      .map(([key, value]) => `${key}: ${value}`)
      .join(' · '),
  }), [selectedBiller, selectedService, previewReceiverName, pricingQuote, payerParty, payerPartyId, serviceFieldValues]);

  const loadOrder = useCallback(async () => {
    if (!orderId) return;
    setOrderLoading(true);
    setOrderError(null);
    try {
      const result = await orderService.getOrder(orderId);
      setOrder(result);
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setOrderError(message || 'Failed to load order.');
    } finally {
      setOrderLoading(false);
    }
  }, [orderId]);

  useEffect(() => {
    if (orderId) {
      loadOrder();
    }
  }, [orderId, loadOrder]);

  useEffect(() => {
    if (!order) return;
    setPayerPartyId(order.payerPartyId);
    setOriginCountry(order.originCountry);
    setOriginCurrency(order.originCurrency);
    setPurposeCode(order.purposeCode ?? '');
  }, [order]);

  useEffect(() => {
    const loadReferenceData = async () => {
      try {
        const [relationships, purposes] = await Promise.all([
          referenceDataService.getItems('RelationshipType'),
          referenceDataService.getItems('PurposeCode'),
        ]);
        setRelationshipTypes(relationships);
        setPurposeCodes(purposes);
      } catch (err) {
        console.error('Failed to load reference data:', err);
      }
    };

    loadReferenceData();
  }, []);

  useEffect(() => {
    const loadCatalog = async () => {
      try {
        const categoriesResponse = await catalogService.getTenantCategories(destinationCountry || undefined);
        setCategories(categoriesResponse.categories);
      } catch (err) {
        console.error('Failed to load catalog lists:', err);
      }
    };

    loadCatalog();
  }, [destinationCountry]);

  useEffect(() => {
    const fetchBillers = async () => {
      try {
        const response = await catalogService.getTenantBillers({
          countryCode: destinationCountry || undefined,
          categoryId: categoryId || undefined,
          search: billerSearch || undefined,
          page: 1,
          pageSize: 50,
        });
        setBillers(response.billers);
      } catch (err) {
        console.error('Failed to load billers:', err);
      }
    };

    fetchBillers();
  }, [destinationCountry, categoryId, billerSearch]);

  useEffect(() => {
    if (!selectedBillerId) {
      setServices([]);
      setSelectedServiceId('');
      return;
    }

    const fetchServices = async () => {
      try {
        const response = await catalogService.getTenantBillerServices(selectedBillerId);
        setServices(response.services);
      } catch (err) {
        console.error('Failed to load services:', err);
      }
    };

    fetchServices();
  }, [selectedBillerId]);

  useEffect(() => {
    if (!selectedBillerId || !selectedServiceId) {
      setServiceDetail(null);
      return;
    }

    const fetchServiceDetail = async () => {
      try {
        const response = await catalogService.getTenantBillerServiceDetail(selectedBillerId, selectedServiceId);
        setServiceDetail(response);
        setDestinationCurrency(response.currency);
        setServiceCode(response.serviceCode);
        setServiceFieldValues({});
        setValidationResult(null);
      } catch (err) {
        console.error('Failed to load service detail:', err);
      }
    };

    fetchServiceDetail();
  }, [selectedBillerId, selectedServiceId]);

  const handleValidateFields = async () => {
    if (!selectedBillerId || !selectedServiceId) return;
    setIsValidating(true);
    try {
      const result = await catalogService.validateServiceFields(selectedBillerId, selectedServiceId, {
        fieldValues: serviceFieldValues,
      });
      setValidationResult(result);
      setStatusMessage(result.isValid ? 'Validation passed.' : result.errorMessage ?? 'Validation failed.');
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setStatusMessage(message || 'Validation failed.');
    } finally {
      setIsValidating(false);
    }
  };

  const handleQuote = async () => {
    if (!originCurrency || !originCountry) {
      setStatusMessage('Origin country and currency are required for pricing.');
      return;
    }

    if (!destinationCountry || !destinationCurrency || !serviceCode) {
      setStatusMessage('Select biller, service, destination country, and currency.');
      return;
    }

    const amount = Number(amountValue);
    if (!Number.isFinite(amount) || amount <= 0) {
      setStatusMessage('Enter a valid amount.');
      return;
    }

    setIsQuoting(true);
    setStatusMessage(null);
    try {
      const normalizedOriginCountry = originCountry.toUpperCase();
      const normalizedOriginCurrency = originCurrency.toUpperCase();
      const normalizedDestinationCountry = destinationCountry.toUpperCase();
      const normalizedDestinationCurrency = destinationCurrency.toUpperCase();

      const quote = await pricingService.getQuote({
        originCurrency: normalizedOriginCurrency,
        destinationCurrency: normalizedDestinationCurrency,
        originCountry: normalizedOriginCountry,
        destinationCountry: normalizedDestinationCountry,
        serviceCode,
        destinationAmount: amountMode === 'destination' ? amount : undefined,
        originAmount: amountMode === 'origin' ? amount : undefined,
        customerId: payerPartyId || undefined,
        customerTier: customerTier || undefined,
        quoteContext: quoteContext || undefined,
      });
      setPricingQuote(quote);
      setStatusMessage('Pricing quote ready.');
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setStatusMessage(message || 'Unable to retrieve a quote.');
    } finally {
      setIsQuoting(false);
    }
  };

  const ensureOrder = useCallback(async () => {
    if (order) return order;
    if (!payerPartyId || !originCountry || !originCurrency) {
      throw new Error('Payer, origin country, and origin currency are required.');
    }

    const combinedNotes = [notes, complianceNotes ? `Compliance: ${complianceNotes}` : null]
      .filter(Boolean)
      .join('\n');

    const created = await orderService.createBillPaymentOrder({
      payerPartyId,
      originCountry: originCountry.toUpperCase(),
      originCurrency: originCurrency.toUpperCase(),
      purposeCode: purposeCode || null,
      notes: combinedNotes || null,
      items: null,
    });
    setOrder(created);
    if (!orderId) {
      navigate(`/orders/bill-payments/${created.orderId}`);
    }
    return created;
  }, [order, payerPartyId, originCountry, originCurrency, purposeCode, notes, navigate, orderId]);

  const buildItemPayload = (): CreateBillPaymentItemRequest => {
    const combinedNotes = [notes, complianceNotes ? `Compliance: ${complianceNotes}` : null]
      .filter(Boolean)
      .join('\n');

    if (!pricingQuote) {
      throw new Error('Pricing quote required.');
    }

    if (!selectedBillerId || !selectedServiceId || !destinationCountry || !destinationCurrency) {
      throw new Error('Biller, service, and destination are required.');
    }

    if (receiverMode === 'existing' && !receiverPartyId) {
      throw new Error('Receiver party id is required.');
    }

    if (receiverMode === 'new' && !receiverDraft.displayName.trim()) {
      throw new Error('Receiver details are required.');
    }

    return {
      billerId: selectedBillerId,
      serviceId: selectedServiceId,
      serviceCode: serviceCode || selectedService?.serviceCode || '',
      serviceFieldValues,
      receiverPartyId: receiverMode === 'same'
        ? payerPartyId
        : receiverMode === 'existing'
          ? receiverPartyId
          : undefined,
      newReceiver: receiverMode === 'new'
        ? {
            ...receiverDraft,
            displayName: receiverDraft.displayName.trim(),
            partyType: receiverDraft.partyType || 'Person',
          }
        : undefined,
      relationshipTypeCode: relationshipType || (receiverMode === 'same' ? 'Self' : undefined),
      originAmount: pricingQuote.originAmount,
      destinationAmount: pricingQuote.destinationAmount,
      destinationCurrency: destinationCurrency.toUpperCase(),
      destinationCountry: destinationCountry.toUpperCase(),
      pricingQuoteId: pricingQuote.pricingQuoteId,
      purposeCode: purposeCode || undefined,
      notes: combinedNotes || undefined,
    };
  };

  const handleSaveItem = async () => {
    try {
      if (!payerPartyId) {
        setStatusMessage('Payer party is required.');
        return;
      }

      if (!selectedBillerId || !selectedServiceId) {
        setStatusMessage('Select a biller and service.');
        return;
      }

      if (!destinationCountry) {
        setStatusMessage('Destination country is required.');
        return;
      }

      if (!purposeCode) {
        setStatusMessage('Purpose code is required.');
        return;
      }

      const missingFields = (serviceDetail?.fields ?? [])
        .filter((field) => field.required)
        .filter((field) => !serviceFieldValues[field.key]);

      if (missingFields.length > 0) {
        setStatusMessage(`Missing required fields: ${missingFields.map((field) => field.label).join(', ')}`);
        return;
      }

      if (!validationPassed) {
        setStatusMessage('Validation is required before adding this item.');
        return;
      }

      setIsSavingItem(true);
      const activeOrder = await ensureOrder();
      const payload = buildItemPayload();

      if (editingItemId) {
        await orderService.updateBillPaymentItem(activeOrder.orderId, editingItemId, {
          serviceFieldValues: payload.serviceFieldValues,
          receiverPartyId: payload.receiverPartyId ?? undefined,
          relationshipTypeCode: payload.relationshipTypeCode ?? undefined,
          pricingQuoteId: payload.pricingQuoteId,
          purposeCode: payload.purposeCode ?? undefined,
          notes: payload.notes ?? undefined,
        });
      } else {
        await orderService.addBillPaymentItem(activeOrder.orderId, payload);
      }

      await loadOrder();
      setEditingItemId(null);
      setPricingQuote(null);
      setStatusMessage(editingItemId ? 'Item updated.' : 'Item added to basket.');
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setStatusMessage(message || 'Unable to save the item.');
    } finally {
      setIsSavingItem(false);
    }
  };

  const handleRemoveItem = async (itemId: string) => {
    if (!order) return;
    try {
      await orderService.removeBillPaymentItem(order.orderId, itemId);
      await loadOrder();
      setStatusMessage('Item removed.');
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setStatusMessage(message || 'Unable to remove item.');
    }
  };

  const handleSubmitOrder = async () => {
    if (!order) return;
    setIsSubmittingOrder(true);
    try {
      const result = await orderService.submitOrder(order.orderId);
      setOrder(result);
      setStatusMessage('Order submitted.');
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setStatusMessage(message || 'Unable to submit order.');
    } finally {
      setIsSubmittingOrder(false);
    }
  };

  const handleEditItem = (itemId: string) => {
    const item = orderItems.find((entry) => entry.orderItemId === itemId);
    if (!item) return;
    const biller = billers.find((entry) => entry.billerId === item.billerId);
    setEditingItemId(itemId);
    setSelectedBillerId(item.billerId);
    setSelectedServiceId(item.serviceId);
    setDestinationCurrency(item.currencyOut);
    setDestinationCountry(biller?.countryCode ?? destinationCountry);
    setServiceCode(item.serviceCode);
    setServiceFieldValues(item.serviceFieldValues ?? {});
    setReceiverMode(item.receiverPartyId === payerPartyId ? 'same' : 'existing');
    setReceiverPartyId(item.receiverPartyId);
    setRelationshipType(item.relationshipTypeCode ?? '');
    setAmountMode('origin');
    setAmountValue(item.amountIn.toString());
    setPricingQuote(null);
    setStatusMessage('Edit mode enabled. Re-quote before saving.');
  };

  const handleRefreshQuote = async (itemId: string) => {
    const item = orderItems.find((entry) => entry.orderItemId === itemId);
    if (!item || !order) return;
    const biller = billers.find((entry) => entry.billerId === item.billerId);

    try {
      const quote = await pricingService.getQuote({
        originCurrency: order.originCurrency,
        destinationCurrency: item.currencyOut,
        originCountry: order.originCountry,
        destinationCountry: biller?.countryCode ?? destinationCountry,
        serviceCode: item.serviceCode,
        destinationAmount: item.amountOut,
        originAmount: item.amountIn,
        customerId: order.payerPartyId,
        customerTier: customerTier || undefined,
        quoteContext: 'BillPayment',
      });

      await orderService.updateBillPaymentItem(order.orderId, itemId, {
        pricingQuoteId: quote.pricingQuoteId,
      });
      await loadOrder();
      setStatusMessage('Quote refreshed.');
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setStatusMessage(message || 'Unable to refresh quote.');
    }
  };

  const handleCreatePayer = async (payload: CreatePartyRequest) => {
    const party = await partyService.createParty(payload);
    setPayerParty(party);
    setPayerPartyId(party.partyId);
  };

  const handleCreateReceiver = async (payload: CreatePartyRequest) => {
    setReceiverDraft({
      displayName: payload.displayName,
      partyType: payload.partyType,
      firstName: payload.firstName ?? null,
      lastName: payload.lastName ?? null,
      phone: payload.phone ?? null,
      email: payload.email ?? null,
      countryCode: payload.countryCode ?? null,
    });
    setReceiverMode('new');
  };

  if (orderLoading) {
    return (
      <div className="h-full overflow-auto p-6">
        <div className="flex items-center gap-2 text-[var(--color-text-secondary)]">
          <RefreshCw className="w-4 h-4 animate-spin" /> Loading order...
        </div>
      </div>
    );
  }

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Create bill payment order</h1>
          <p className="text-[var(--color-text-secondary)]">
            Build a multi-item bill payment order with pricing quotes and compliance context.
          </p>
        </div>
        {order && (
          <Badge variant="outline">Draft {order.orderId.slice(0, 8)}</Badge>
        )}
      </div>

      {orderError && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span className="flex-1">{orderError}</span>
          </CardContent>
        </Card>
      )}

      {statusMessage && (
        <Card className="mb-6 border-[var(--color-border-light)] bg-[var(--color-surface-inset)]">
          <CardContent className="p-3 flex items-center gap-3 text-[var(--color-text-secondary)]">
            <CheckCircle2 className="w-4 h-4 text-[var(--color-brand-primary)]" />
            <span>{statusMessage}</span>
          </CardContent>
        </Card>
      )}

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_360px] xl:grid-rows-2">
        <Card className="xl:col-start-1 xl:row-start-1">
            <CardContent className="p-5 space-y-4">
              <div>
                <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Card 1</p>
                <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Biller discovery</h2>
                <p className="text-sm text-[var(--color-text-secondary)]">Select destination corridor, biller, and service.</p>
              </div>

              <label className="text-sm text-[var(--color-text-secondary)]">
                Destination country
                <CountrySelect
                  value={destinationCountry}
                  onChange={setDestinationCountry}
                  placeholder="Select destination"
                  includeEmpty={true}
                  emptyLabel="Clear selection"
                  className="mt-2 w-full"
                />
              </label>

              <label className="text-sm text-[var(--color-text-secondary)]">
                Biller category
                <Select
                  value={categoryId || undefined}
                  onValueChange={(value) => setCategoryId(value === '__all__' ? '' : value)}
                >
                  <SelectTrigger className="mt-2 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 py-2 text-sm">
                    <SelectValue placeholder="All categories" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__all__">All categories</SelectItem>
                    {categories.map((category) => (
                      <SelectItem key={category.categoryId} value={category.categoryId}>
                        {category.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </label>

              <label className="text-sm text-[var(--color-text-secondary)]">
                Search billers
                <input
                  type="text"
                  value={billerSearch}
                  onChange={(event) => setBillerSearch(event.target.value)}
                  placeholder="Search billers"
                  className="mt-2 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 py-2 text-sm"
                />
              </label>

              <label className="text-sm text-[var(--color-text-secondary)]">
                Biller
                <Select
                  value={selectedBillerId || undefined}
                  onValueChange={(value) => setSelectedBillerId(value === '__clear__' ? '' : value)}
                >
                  <SelectTrigger className="mt-2 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 py-2 text-sm">
                    <SelectValue placeholder="Select biller" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__clear__">Select biller</SelectItem>
                    {billers.map((biller) => (
                      <SelectItem key={biller.billerId} value={biller.billerId}>
                        {biller.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </label>

              <label className="text-sm text-[var(--color-text-secondary)]">
                Service
                <Select
                  value={selectedServiceId || undefined}
                  onValueChange={(value) => setSelectedServiceId(value === '__clear__' ? '' : value)}
                >
                  <SelectTrigger className="mt-2 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 py-2 text-sm">
                    <SelectValue placeholder="Select service" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__clear__">Select service</SelectItem>
                    {services.map((service) => (
                      <SelectItem key={service.serviceId} value={service.serviceId}>
                        {service.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </label>

              <div className="grid gap-3 sm:grid-cols-2">
                <label className="text-sm text-[var(--color-text-secondary)]">
                  Service code
                  <input
                    type="text"
                    value={serviceCode}
                    readOnly
                    className="mt-2 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 py-2 text-sm"
                  />
                </label>
                <label className="text-sm text-[var(--color-text-secondary)]">
                  Destination currency
                  <input
                    type="text"
                    value={destinationCurrency}
                    onChange={(event) => setDestinationCurrency(event.target.value.toUpperCase())}
                    className="mt-2 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 py-2 text-sm"
                  />
                </label>
              </div>
            </CardContent>
        </Card>

        <Card className="xl:col-start-2 xl:row-start-1">
            <CardContent className="p-5 space-y-4">
              <div>
                <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Card 2</p>
                <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Customer & account</h2>
                <p className="text-sm text-[var(--color-text-secondary)]">Identify the payer and service-required fields.</p>
              </div>

              <div className="flex items-center gap-2">
                <label className="text-sm text-[var(--color-text-secondary)] flex-1">
                  Payer party id
                  <input
                    type="text"
                    value={payerPartyId}
                    onChange={(event) => setPayerPartyId(event.target.value)}
                    placeholder="UUID"
                    className="mt-2 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 py-2 text-sm"
                  />
                </label>
                <Button variant="outline" size="sm" className="mt-6" onClick={() => setPayerModalOpen(true)}>
                  <UserPlus className="w-4 h-4 mr-1" /> New
                </Button>
              </div>
              {payerParty && (
                <div className="text-xs text-[var(--color-text-tertiary)]">Created {payerParty.displayName}</div>
              )}

              <label className="text-sm text-[var(--color-text-secondary)]">
                Customer tier (optional)
                <input
                  type="text"
                  value={customerTier}
                  onChange={(event) => setCustomerTier(event.target.value)}
                  placeholder="Retail"
                  className="mt-2 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 py-2 text-sm"
                />
              </label>

              {serviceDetail?.fields?.length ? (
                <div className="space-y-3">
                  <div className="flex items-center justify-between">
                    <span className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Service fields</span>
                    {requiresValidation && (
                      <Badge variant="outline" className="text-xs">Validation required</Badge>
                    )}
                  </div>
                  {serviceDetail.fields.map((field) => (
                    <label key={field.key} className="text-sm text-[var(--color-text-secondary)]">
                      {field.label}
                      {field.required && <span className="text-[var(--color-error)]"> *</span>}
                      <input
                        type="text"
                        value={serviceFieldValues[field.key] ?? ''}
                        onChange={(event) => setServiceFieldValues((prev) => ({
                          ...prev,
                          [field.key]: event.target.value,
                        }))}
                        placeholder={field.placeholder ?? ''}
                        className="mt-2 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 py-2 text-sm"
                      />
                    </label>
                  ))}
                  {requiresValidation && (
                    <Button size="sm" variant="outline" onClick={handleValidateFields} disabled={isValidating}>
                      {isValidating ? 'Validating...' : 'Validate fields'}
                    </Button>
                  )}
                  {validationResult && (
                    <div className={`text-xs ${validationResult.isValid ? 'text-[var(--color-brand-primary)]' : 'text-[var(--color-error)]'}`}>
                      {validationResult.isValid ? 'Validation passed.' : validationResult.errorMessage ?? 'Validation failed.'}
                    </div>
                  )}
                </div>
              ) : (
                <div className="text-xs text-[var(--color-text-tertiary)]">Select a service to see required fields.</div>
              )}
            </CardContent>
        </Card>

        <Card className="xl:col-start-1 xl:row-start-2">
            <CardContent className="p-5 space-y-4">
              <div>
                <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Card 3</p>
                <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Amounts & funding</h2>
                <p className="text-sm text-[var(--color-text-secondary)]">Choose amount mode and request a pricing quote.</p>
              </div>

              <div className="grid gap-3 sm:grid-cols-2">
                <label className="text-sm text-[var(--color-text-secondary)]">
                  Origin country
                  <input
                    type="text"
                    value={originCountry}
                    onChange={(event) => setOriginCountry(event.target.value.toUpperCase())}
                    placeholder="NG"
                    className="mt-2 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 py-2 text-sm"
                  />
                </label>
                <label className="text-sm text-[var(--color-text-secondary)]">
                  Origin currency
                  <input
                    type="text"
                    value={originCurrency}
                    onChange={(event) => setOriginCurrency(event.target.value.toUpperCase())}
                    placeholder="NGN"
                    className="mt-2 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 py-2 text-sm"
                  />
                </label>
              </div>

              <div className="flex items-center gap-2">
                <button
                  type="button"
                  className={`px-3 py-2 rounded-sm border text-sm ${amountMode === 'origin'
                    ? 'border-[var(--color-brand-primary)] text-[var(--color-brand-primary)]'
                    : 'border-[var(--color-border)] text-[var(--color-text-secondary)]'}`}
                  onClick={() => setAmountMode('origin')}
                >
                  Origin amount
                </button>
                <button
                  type="button"
                  className={`px-3 py-2 rounded-sm border text-sm ${amountMode === 'destination'
                    ? 'border-[var(--color-brand-primary)] text-[var(--color-brand-primary)]'
                    : 'border-[var(--color-border)] text-[var(--color-text-secondary)]'}`}
                  onClick={() => setAmountMode('destination')}
                >
                  Destination amount
                </button>
              </div>

              <label className="text-sm text-[var(--color-text-secondary)]">
                Amount
                <input
                  type="number"
                  min="0"
                  value={amountValue}
                  onChange={(event) => setAmountValue(event.target.value)}
                  className="mt-2 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 py-2 text-sm"
                />
              </label>

              <label className="text-sm text-[var(--color-text-secondary)]">
                Quote context
                <input
                  type="text"
                  value={quoteContext}
                  onChange={(event) => setQuoteContext(event.target.value)}
                  className="mt-2 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 py-2 text-sm"
                />
              </label>

              <label className="text-sm text-[var(--color-text-secondary)]">
                Funding source (optional)
                <input
                  type="text"
                  value={fundingSourceRef}
                  onChange={(event) => setFundingSourceRef(event.target.value)}
                  placeholder="Wallet or bank ref"
                  className="mt-2 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 py-2 text-sm"
                />
              </label>

              <div className="flex items-center justify-between">
                <Button size="sm" variant="outline" onClick={handleQuote} disabled={isQuoting}>
                  {isQuoting ? 'Quoting...' : 'Get quote'}
                </Button>
                {pricingQuote && (
                  <div className="text-xs text-[var(--color-text-secondary)]">
                    Quote {pricingQuote.pricingQuoteId.slice(0, 8)}
                  </div>
                )}
              </div>

              {pricingQuote && (
                <div className="rounded-sm border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-3 text-xs text-[var(--color-text-secondary)]">
                  <div className="flex justify-between">
                    <span>Origin amount</span>
                    <span>{toCurrency(pricingQuote.originAmount, originCurrency || 'USD')}</span>
                  </div>
                  <div className="flex justify-between">
                    <span>Destination amount</span>
                    <span>{toCurrency(pricingQuote.destinationAmount, destinationCurrency || 'USD')}</span>
                  </div>
                  <div className="flex justify-between">
                    <span>Fees</span>
                    <span>{toCurrency(pricingQuote.feesTotal, originCurrency || 'USD')}</span>
                  </div>
                  <div className="flex justify-between">
                    <span>Total</span>
                    <span>{toCurrency(pricingQuote.totalAmount, originCurrency || 'USD')}</span>
                  </div>
                </div>
              )}
            </CardContent>
        </Card>

        <Card className="xl:col-start-2 xl:row-start-2">
            <CardContent className="p-5 space-y-4">
              <div>
                <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Card 4</p>
                <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Receiver & compliance</h2>
                <p className="text-sm text-[var(--color-text-secondary)]">Capture receiver details and relationship context.</p>
              </div>

              <div className="flex items-center gap-2">
                <button
                  type="button"
                  className={`px-3 py-2 rounded-sm border text-sm ${receiverMode === 'same'
                    ? 'border-[var(--color-brand-primary)] text-[var(--color-brand-primary)]'
                    : 'border-[var(--color-border)] text-[var(--color-text-secondary)]'}`}
                  onClick={() => setReceiverMode('same')}
                >
                  Same as payer
                </button>
                <button
                  type="button"
                  className={`px-3 py-2 rounded-sm border text-sm ${receiverMode === 'existing'
                    ? 'border-[var(--color-brand-primary)] text-[var(--color-brand-primary)]'
                    : 'border-[var(--color-border)] text-[var(--color-text-secondary)]'}`}
                  onClick={() => setReceiverMode('existing')}
                >
                  Existing
                </button>
                <button
                  type="button"
                  className={`px-3 py-2 rounded-sm border text-sm ${receiverMode === 'new'
                    ? 'border-[var(--color-brand-primary)] text-[var(--color-brand-primary)]'
                    : 'border-[var(--color-border)] text-[var(--color-text-secondary)]'}`}
                  onClick={() => setReceiverMode('new')}
                >
                  New
                </button>
              </div>

              {receiverMode === 'existing' && (
                <label className="text-sm text-[var(--color-text-secondary)]">
                  Receiver party id
                  <input
                    type="text"
                    value={receiverPartyId}
                    onChange={(event) => setReceiverPartyId(event.target.value)}
                    className="mt-2 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 py-2 text-sm"
                  />
                </label>
              )}

              {receiverMode === 'new' && (
                <div className="rounded-sm border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-3 text-sm text-[var(--color-text-secondary)]">
                  <div className="flex items-center justify-between">
                    <div>
                      <div className="text-sm font-medium text-[var(--color-text-primary)]">
                        {receiverDraft.displayName || 'New receiver not set'}
                      </div>
                      <div className="text-xs">{receiverDraft.email ?? 'No email'} · {receiverDraft.phone ?? 'No phone'}</div>
                    </div>
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => {
                        setReceiverMode('new');
                        setReceiverModalOpen(true);
                      }}
                    >
                      <Users className="w-4 h-4 mr-1" /> Create
                    </Button>
                  </div>
                </div>
              )}

              <label className="text-sm text-[var(--color-text-secondary)]">
                Relationship type
                <Select
                  value={relationshipType || undefined}
                  onValueChange={(value) => setRelationshipType(value === '__clear__' ? '' : value)}
                >
                  <SelectTrigger className="mt-2 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 py-2 text-sm">
                    <SelectValue placeholder="Select relationship" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__clear__">Select relationship</SelectItem>
                    {relationshipTypes.map((type) => (
                      <SelectItem key={type.code} value={type.code}>
                        {type.displayName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </label>

              <label className="text-sm text-[var(--color-text-secondary)]">
                Purpose code
                <Select
                  value={purposeCode || undefined}
                  onValueChange={(value) => setPurposeCode(value === '__clear__' ? '' : value)}
                >
                  <SelectTrigger className="mt-2 w-full rounded-sm border border-[var(--color-border)] bg-transparent px-3 py-2 text-sm">
                    <SelectValue placeholder="Select purpose" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__clear__">Select purpose</SelectItem>
                    {purposeCodes.map((purpose) => (
                      <SelectItem key={purpose.code} value={purpose.code}>
                        {purpose.displayName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </label>

              <label className="text-sm text-[var(--color-text-secondary)]">
                Notes
                <textarea
                  value={notes}
                  onChange={(event) => setNotes(event.target.value)}
                  className="mt-2 w-full min-h-[72px] rounded-sm border border-[var(--color-border)] bg-transparent px-3 py-2 text-sm"
                />
              </label>

              <label className="text-sm text-[var(--color-text-secondary)]">
                Compliance notes
                <textarea
                  value={complianceNotes}
                  onChange={(event) => setComplianceNotes(event.target.value)}
                  className="mt-2 w-full min-h-[72px] rounded-sm border border-[var(--color-border)] bg-transparent px-3 py-2 text-sm"
                />
              </label>
            </CardContent>
        </Card>

        <Card className="xl:col-start-3 xl:row-start-1 xl:row-span-2">
          <CardContent className="p-5 space-y-4">
            <div>
              <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Basket</p>
              <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Order items</h2>
              <p className="text-sm text-[var(--color-text-secondary)]">Review and submit the order draft.</p>
            </div>

            <div className="rounded-sm border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-3">
              <div className="flex items-center justify-between text-xs text-[var(--color-text-tertiary)]">
                <span>Current item</span>
                {pricingQuote && (
                  <Badge variant="outline">Quote ready</Badge>
                )}
              </div>
              <div className="mt-2 text-sm text-[var(--color-text-primary)]">
                <div className="font-medium">{previewItem.billerName}</div>
                <div className="text-xs text-[var(--color-text-tertiary)]">{previewItem.serviceName}</div>
                <div className="text-xs text-[var(--color-text-tertiary)]">Payer: {previewItem.payerName}</div>
                <div className="text-xs text-[var(--color-text-tertiary)]">Receiver: {previewItem.receiverName}</div>
                {previewItem.fieldsSummary && (
                  <div className="text-xs text-[var(--color-text-tertiary)]">{previewItem.fieldsSummary}</div>
                )}
                {pricingQuote && (
                  <div className="mt-2 text-xs text-[var(--color-text-secondary)]">
                    Total {toCurrency(pricingQuote.totalAmount, originCurrency || 'USD')}
                  </div>
                )}
              </div>
              <Button
                className="mt-3 w-full"
                onClick={handleSaveItem}
                disabled={!isDraftEditable || isSavingItem || !pricingQuote || !validationPassed}
              >
                {editingItemId ? 'Save item' : 'Add item to basket'}
                <ArrowRight className="w-4 h-4 ml-2" />
              </Button>
              {editingItemId && (
                <Button
                  className="mt-2 w-full"
                  variant="outline"
                  onClick={() => setEditingItemId(null)}
                >
                  Cancel edit
                </Button>
              )}
            </div>

            <div className="space-y-3">
              {orderItems.length === 0 ? (
                <div className="text-sm text-[var(--color-text-secondary)]">No items added yet.</div>
              ) : (
                orderItems.map((item) => (
                  <div key={item.orderItemId} className="rounded-sm border border-[var(--color-border-light)] p-3">
                    <div className="flex items-start justify-between">
                      <div>
                        <div className="text-sm font-semibold text-[var(--color-text-primary)]">{item.serviceName}</div>
                        <div className="text-xs text-[var(--color-text-tertiary)]">{item.billerName}</div>
                        <div className="text-xs text-[var(--color-text-tertiary)]">Receiver: {item.receiverName}</div>
                        {Object.keys(item.serviceFieldValues || {}).length > 0 && (
                          <div className="text-xs text-[var(--color-text-tertiary)]">
                            {Object.entries(item.serviceFieldValues)
                              .slice(0, 2)
                              .map(([key, value]) => `${key}: ${value}`)
                              .join(' · ')}
                          </div>
                        )}
                      </div>
                      <Badge variant="outline" className={item.isQuoteExpired ? 'text-[var(--color-error)]' : ''}>
                        {item.isQuoteExpired ? 'Quote expired' : item.status}
                      </Badge>
                    </div>
                    <div className="mt-2 text-xs text-[var(--color-text-secondary)]">
                      {toCurrency(item.amountIn, item.currencyIn)} · Fees {toCurrency(item.feesTotal, item.currencyIn)}
                    </div>
                    <div className="mt-3 flex items-center justify-between">
                      <div className="text-xs text-[var(--color-text-tertiary)]">
                        Quote exp {item.quoteExpiresAt ?? '—'}
                      </div>
                      <div className="flex items-center gap-2">
                        {item.isQuoteExpired && (
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => handleRefreshQuote(item.orderItemId)}
                            disabled={!isDraftEditable}
                          >
                            <RefreshCw className="w-3.5 h-3.5 mr-1" /> Refresh
                          </Button>
                        )}
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => handleEditItem(item.orderItemId)}
                          disabled={!isDraftEditable}
                        >
                          Edit
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => handleRemoveItem(item.orderItemId)}
                          disabled={!isDraftEditable}
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                        </Button>
                      </div>
                    </div>
                  </div>
                ))
              )}
            </div>

            <div className="rounded-sm border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-3 text-xs">
              <div className="flex justify-between text-[var(--color-text-secondary)]">
                <span>Total amount</span>
                <span>{toCurrency(orderTotals.amountIn, originCurrency || order?.originCurrency || 'USD')}</span>
              </div>
              <div className="flex justify-between text-[var(--color-text-secondary)]">
                <span>Total fees</span>
                <span>{toCurrency(orderTotals.fees, originCurrency || order?.originCurrency || 'USD')}</span>
              </div>
              <div className="flex justify-between text-[var(--color-text-primary)] font-semibold mt-1">
                <span>Destination total</span>
                <span>{toCurrency(orderTotals.amountOut, destinationCurrency || order?.destinationCurrency || 'USD')}</span>
              </div>
            </div>

            <Button
              className="w-full"
              onClick={handleSubmitOrder}
              disabled={!order || orderItems.length === 0 || hasExpiredItems || order.status !== 'Draft' || isSubmittingOrder}
            >
              <ShieldCheck className="w-4 h-4 mr-2" />
              {isSubmittingOrder ? 'Submitting...' : 'Submit order'}
            </Button>
            {hasExpiredItems && (
              <div className="text-xs text-[var(--color-error)]">Refresh expired quotes before submission.</div>
            )}
          </CardContent>
        </Card>
      </div>

      <PartyModal
        title="Create payer"
        submitLabel="Create payer"
        isOpen={payerModalOpen}
        onClose={() => setPayerModalOpen(false)}
        onSubmit={handleCreatePayer}
      />

      <PartyModal
        title="Create receiver"
        submitLabel="Use receiver"
        isOpen={receiverModalOpen}
        onClose={() => setReceiverModalOpen(false)}
        onSubmit={handleCreateReceiver}
      />
    </div>
  );
}
