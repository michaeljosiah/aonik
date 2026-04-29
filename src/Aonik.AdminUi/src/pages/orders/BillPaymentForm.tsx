// Left-pane bill payment builder — port of `BillPaymentForm` from
// templates/aonik-admin-starterkit/screens/orders.jsx, wired to the live
// catalog / pricing / validation services.
//
// Six sections, gap-4 between them, matching the template 1:1:
//   1. Parties (Payer + Beneficiary, 2-col grid)
//   2. Biller (eyebrow + BillerGrid)
//   3. Service + Account (2-col grid; Service select + first required field;
//      additional required fields stack below)
//   4. Currency + Amount (80px + 1fr grid)
//   5. FxQuote (always-rendered live banner)
//   6. Add to order (full-width primary)
//
// Differences from the template that *cannot* be hidden because the API
// requires more than the template's synthetic flow:
//   • The pricing quote is an async backend call. We auto-quote (debounced
//     500ms) whenever the inputs become valid, instead of computing inline
//     from a hard-coded FX table.
//   • Service-specific fields are dynamic from `serviceDetail.fields`; the
//     template hard-codes a Service-type/Account-ref pair.
//   • Origin country/currency, destination country, customer tier, funding
//     ref, quote context, relationship type, compliance notes, purpose code
//     are all set to sensible defaults so they don't appear in the UI.

import { useEffect, useRef, useState } from 'react';
import { ArrowRight } from 'lucide-react';
import { catalogService } from '@/services/catalogService';
import { pricingService } from '@/services/pricingService';
import { Button } from '@/components/ui/button';
import { PartyPicker, type PartyPickerOption } from './PartyPicker';
import { BillerGrid } from './BillerGrid';
import { FxQuote } from './FxQuote';
import type {
  CatalogBillerCategoryItem,
  CatalogBillerServiceDetailResponse,
  CatalogBillerServiceItem,
  CatalogBillerSummaryItem,
  CatalogServiceFieldValidationResponse,
  PricingQuoteResponse,
} from '@/types';

export interface BillPaymentFormState {
  payerPartyId: string;
  payerOption: PartyPickerOption | null;
  receiverPartyId: string;
  receiverOption: PartyPickerOption | null;
  destinationCountry: string;
  categoryId: string;
  billerSearch: string;
  selectedBillerId: string;
  selectedServiceId: string;
  serviceCode: string;
  destinationCurrency: string;
  serviceFieldValues: Record<string, string>;
  validationResult: CatalogServiceFieldValidationResponse | null;
  originCountry: string;
  originCurrency: string;
  amountMode: 'origin' | 'destination';
  amountValue: string;
  customerTier: string;
  fundingSourceRef: string;
  quoteContext: string;
  purposeCode: string;
  relationshipTypeCode: string;
  notes: string;
  complianceNotes: string;
  pricingQuote: PricingQuoteResponse | null;
}

export function createEmptyFormState(): BillPaymentFormState {
  return {
    payerPartyId: '',
    payerOption: null,
    receiverPartyId: '',
    receiverOption: null,
    destinationCountry: '',
    categoryId: '',
    billerSearch: '',
    selectedBillerId: '',
    selectedServiceId: '',
    serviceCode: '',
    destinationCurrency: '',
    serviceFieldValues: {},
    validationResult: null,
    originCountry: 'GB',
    originCurrency: 'GBP',
    amountMode: 'destination',
    amountValue: '',
    customerTier: '',
    fundingSourceRef: '',
    quoteContext: 'BillPayment',
    purposeCode: '',
    relationshipTypeCode: '',
    notes: '',
    complianceNotes: '',
    pricingQuote: null,
  };
}

export interface BillPaymentFormProps {
  state: BillPaymentFormState;
  onChange: (next: Partial<BillPaymentFormState>) => void;
  onAddItem: () => void;
  onCancelEdit?: () => void;
  isEditing: boolean;
  isSavingItem: boolean;
  disabled?: boolean;
}

const FIELD_LABEL_BY_CATEGORY: Record<string, string> = {
  Electricity: 'Meter number',
  'TV & Cable': 'Smart card no.',
  Internet: 'Account number',
  'Airtime & Data': 'Phone number',
};

export function BillPaymentForm({
  state,
  onChange,
  onAddItem,
  onCancelEdit,
  isEditing,
  isSavingItem,
  disabled,
}: BillPaymentFormProps) {
  const [categories, setCategories] = useState<CatalogBillerCategoryItem[]>([]);
  const [billers, setBillers] = useState<CatalogBillerSummaryItem[]>([]);
  const [billersLoading, setBillersLoading] = useState(false);
  const [services, setServices] = useState<CatalogBillerServiceItem[]>([]);
  const [serviceDetail, setServiceDetail] = useState<CatalogBillerServiceDetailResponse | null>(null);
  const quoteSeqRef = useRef(0);

  // Load categories whenever destination changes (destination is set
  // implicitly by the selected biller).
  useEffect(() => {
    void (async () => {
      try {
        const result = await catalogService.getTenantCategories(state.destinationCountry || undefined);
        setCategories(result.categories);
      } catch {
        setCategories([]);
      }
    })();
  }, [state.destinationCountry]);

  // Load billers whenever category/search changes. We do *not* gate on a
  // destination country chip — that lives implicitly on the biller record.
  useEffect(() => {
    let cancelled = false;
    setBillersLoading(true);
    void (async () => {
      try {
        const result = await catalogService.getTenantBillers({
          countryCode: state.destinationCountry || undefined,
          categoryId: state.categoryId || undefined,
          search: state.billerSearch || undefined,
          page: 1,
          pageSize: 50,
        });
        if (!cancelled) setBillers(result.billers);
      } catch {
        if (!cancelled) setBillers([]);
      } finally {
        if (!cancelled) setBillersLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [state.destinationCountry, state.categoryId, state.billerSearch]);

  // When the biller changes, derive its destination country and load services.
  useEffect(() => {
    if (!state.selectedBillerId) {
      setServices([]);
      onChange({ selectedServiceId: '', serviceCode: '' });
      return;
    }
    const biller = billers.find((b) => b.billerId === state.selectedBillerId);
    if (biller && biller.countryCode !== state.destinationCountry) {
      onChange({ destinationCountry: biller.countryCode });
    }
    void (async () => {
      try {
        const result = await catalogService.getTenantBillerServices(state.selectedBillerId);
        setServices(result.services);
        if (result.services.length === 1) {
          onChange({ selectedServiceId: result.services[0].serviceId });
        }
      } catch {
        setServices([]);
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state.selectedBillerId]);

  // Load service detail whenever biller+service changes.
  useEffect(() => {
    if (!state.selectedBillerId || !state.selectedServiceId) {
      setServiceDetail(null);
      return;
    }
    void (async () => {
      try {
        const result = await catalogService.getTenantBillerServiceDetail(
          state.selectedBillerId,
          state.selectedServiceId,
        );
        setServiceDetail(result);
        onChange({
          destinationCurrency: result.currency,
          serviceCode: result.serviceCode,
          serviceFieldValues: {},
          validationResult: null,
        });
      } catch {
        setServiceDetail(null);
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state.selectedBillerId, state.selectedServiceId]);

  // Auto-quote (debounced) whenever the inputs are sufficient to price.
  const requiresValidation = Boolean(serviceDetail?.requiresValidation);
  const validationPassed = !requiresValidation || state.validationResult?.isValid;
  const missingRequiredFields = (serviceDetail?.fields ?? [])
    .filter((field) => field.required)
    .filter((field) => !state.serviceFieldValues[field.key]?.trim());

  const canQuote =
    !!state.originCountry &&
    !!state.originCurrency &&
    !!state.destinationCountry &&
    !!state.destinationCurrency &&
    !!state.serviceCode &&
    Number(state.amountValue) > 0 &&
    missingRequiredFields.length === 0;

  useEffect(() => {
    if (!canQuote) return;
    const seq = ++quoteSeqRef.current;
    const handle = window.setTimeout(async () => {
      try {
        const amount = Number(state.amountValue);
        const quote = await pricingService.getQuote({
          originCurrency: state.originCurrency.toUpperCase(),
          destinationCurrency: state.destinationCurrency.toUpperCase(),
          originCountry: state.originCountry.toUpperCase(),
          destinationCountry: state.destinationCountry.toUpperCase(),
          serviceCode: state.serviceCode,
          destinationAmount: state.amountMode === 'destination' ? amount : undefined,
          originAmount: state.amountMode === 'origin' ? amount : undefined,
          customerId: state.payerPartyId || undefined,
          quoteContext: state.quoteContext || 'BillPayment',
        });
        if (quoteSeqRef.current === seq) {
          onChange({ pricingQuote: quote });
        }
      } catch {
        if (quoteSeqRef.current === seq) {
          onChange({ pricingQuote: null });
        }
      }
    }, 500);
    return () => window.clearTimeout(handle);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [
    canQuote,
    state.amountValue,
    state.amountMode,
    state.originCurrency,
    state.destinationCurrency,
    state.originCountry,
    state.destinationCountry,
    state.serviceCode,
    state.payerPartyId,
  ]);

  const selectedBiller = billers.find((b) => b.billerId === state.selectedBillerId);
  const selectedCategory = categories.find((c) => c.categoryId === selectedBiller?.categoryId);
  const accountFieldLabel =
    FIELD_LABEL_BY_CATEGORY[selectedCategory?.name ?? ''] ??
    (serviceDetail?.fields.find((f) => f.required)?.label ?? 'Account / phone');

  const requiredFields = (serviceDetail?.fields ?? []).filter((f) => f.required);
  const primaryField = requiredFields[0];
  const extraFields = requiredFields.slice(1);

  // On blur of the primary field, run validation if the service requires it.
  const handleValidateBlur = async () => {
    if (!requiresValidation) return;
    if (!state.selectedBillerId || !state.selectedServiceId) return;
    if (missingRequiredFields.length > 0) return;
    try {
      const result = await catalogService.validateServiceFields(
        state.selectedBillerId,
        state.selectedServiceId,
        { fieldValues: state.serviceFieldValues },
      );
      onChange({ validationResult: result });
    } catch {
      onChange({
        validationResult: {
          isValid: false,
          validatedAt: new Date().toISOString(),
          errorCode: null,
          errorMessage: 'Validation request failed.',
          accountHolderName: null,
          additionalInfo: null,
        },
      });
    }
  };

  const canAdd =
    !disabled &&
    !!state.payerPartyId &&
    !!state.selectedBillerId &&
    !!state.selectedServiceId &&
    !!state.destinationCountry &&
    missingRequiredFields.length === 0 &&
    validationPassed &&
    !!state.pricingQuote;

  return (
    <div className="flex flex-col gap-4">
      {/* 1. Parties */}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <PartyPicker
          label="Payer"
          value={state.payerPartyId}
          preloaded={state.payerOption}
          onChange={(id, opt) => onChange({ payerPartyId: id, payerOption: opt })}
          excludeIds={state.receiverPartyId ? [state.receiverPartyId] : []}
          placeholder="Select payer"
        />
        <PartyPicker
          label="Beneficiary"
          value={state.receiverPartyId}
          preloaded={state.receiverOption}
          onChange={(id, opt) => onChange({ receiverPartyId: id, receiverOption: opt })}
          excludeIds={state.payerPartyId ? [state.payerPartyId] : []}
          placeholder="Select beneficiary"
        />
      </div>

      {/* 2. Biller */}
      <div>
        <div className="mb-2 text-[11px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
          Biller
        </div>
        <BillerGrid
          billers={billers}
          categories={categories}
          selectedBillerId={state.selectedBillerId}
          selectedCategoryId={state.categoryId}
          search={state.billerSearch}
          onSelectBiller={(id) =>
            onChange({
              selectedBillerId: id,
              selectedServiceId: '',
              serviceCode: '',
              pricingQuote: null,
            })
          }
          onSelectCategory={(id) => onChange({ categoryId: id })}
          onSearchChange={(value) => onChange({ billerSearch: value })}
          loading={billersLoading}
        />
      </div>

      {/* 3. Service + Account (only when biller selected) */}
      {selectedBiller && (
        <>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <label className="text-[12px] text-[var(--color-text-secondary)]">
              Service type
              <select
                value={state.selectedServiceId}
                onChange={(e) =>
                  onChange({ selectedServiceId: e.target.value, pricingQuote: null })
                }
                className="aonik-select mt-1.5 text-[13px]"
              >
                <option value="">Select service…</option>
                {services.map((s) => (
                  <option key={s.serviceId} value={s.serviceId}>
                    {s.name}
                  </option>
                ))}
              </select>
            </label>
            {primaryField ? (
              <label className="text-[12px] text-[var(--color-text-secondary)]">
                {primaryField.label || accountFieldLabel}
                <input
                  type="text"
                  value={state.serviceFieldValues[primaryField.key] ?? ''}
                  onChange={(e) =>
                    onChange({
                      serviceFieldValues: {
                        ...state.serviceFieldValues,
                        [primaryField.key]: e.target.value,
                      },
                      validationResult: null,
                    })
                  }
                  onBlur={() => void handleValidateBlur()}
                  placeholder={primaryField.placeholder ?? ''}
                  className="aonik-input mt-1.5 text-[13px]"
                />
              </label>
            ) : (
              <label className="text-[12px] text-[var(--color-text-secondary)]">
                {accountFieldLabel}
                <input
                  type="text"
                  disabled
                  placeholder="Select a service first"
                  className="aonik-input mt-1.5 text-[13px]"
                />
              </label>
            )}
          </div>

          {/* Stack any additional required fields below */}
          {extraFields.length > 0 && (
            <div className="flex flex-col gap-3">
              {extraFields.map((field) => (
                <label key={field.key} className="text-[12px] text-[var(--color-text-secondary)]">
                  {field.label}
                  <input
                    type="text"
                    value={state.serviceFieldValues[field.key] ?? ''}
                    onChange={(e) =>
                      onChange({
                        serviceFieldValues: {
                          ...state.serviceFieldValues,
                          [field.key]: e.target.value,
                        },
                        validationResult: null,
                      })
                    }
                    onBlur={() => void handleValidateBlur()}
                    placeholder={field.placeholder ?? ''}
                    className="aonik-input mt-1.5 text-[13px]"
                  />
                </label>
              ))}
            </div>
          )}

          {/* Validation result, inline */}
          {state.validationResult && (
            <div
              className={
                'text-[11.5px] ' +
                (state.validationResult.isValid
                  ? 'text-[var(--color-brand-primary)]'
                  : 'text-[var(--color-error)]')
              }
            >
              {state.validationResult.isValid
                ? state.validationResult.accountHolderName
                  ? `Verified · ${state.validationResult.accountHolderName}`
                  : 'Verified.'
                : state.validationResult.errorMessage ?? 'Validation failed.'}
            </div>
          )}
        </>
      )}

      {/* 4. Currency + Amount (80px + 1fr) */}
      <div className="grid grid-cols-[80px_1fr] gap-2.5">
        <label className="text-[12px] text-[var(--color-text-secondary)]">
          Currency
          <select
            value={state.destinationCurrency || ''}
            onChange={(e) =>
              onChange({
                destinationCurrency: e.target.value.toUpperCase(),
                pricingQuote: null,
              })
            }
            className="aonik-select mt-1.5 px-2.5 font-[family-name:var(--font-mono)] text-[13px]"
          >
            <option value="">—</option>
            <option value="NGN">NGN</option>
            <option value="GBP">GBP</option>
            <option value="USD">USD</option>
            <option value="EUR">EUR</option>
            <option value="GHS">GHS</option>
            <option value="KES">KES</option>
          </select>
        </label>
        <label className="text-[12px] text-[var(--color-text-secondary)]">
          Amount
          <input
            type="number"
            min={0}
            value={state.amountValue}
            onChange={(e) => onChange({ amountValue: e.target.value, pricingQuote: null })}
            placeholder="0.00"
            className="aonik-input mt-1.5 font-[family-name:var(--font-mono)] text-[13px]"
          />
        </label>
      </div>

      {/* 5. FxQuote — live banner, no button */}
      <FxQuote
        quote={state.pricingQuote}
        originCurrency={state.originCurrency}
        destinationCurrency={state.destinationCurrency}
      />

      {/* 6. Add to order */}
      <div className="flex gap-2 pt-1">
        {isEditing && onCancelEdit && (
          <Button variant="outline" onClick={onCancelEdit}>
            Cancel edit
          </Button>
        )}
        <Button className="flex-1 justify-center" onClick={onAddItem} disabled={!canAdd || isSavingItem}>
          {isSavingItem ? 'Saving…' : isEditing ? 'Save item' : 'Add to order'}
          <ArrowRight className="h-3.5 w-3.5" />
        </Button>
      </div>
    </div>
  );
}
