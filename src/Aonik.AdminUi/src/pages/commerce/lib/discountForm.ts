import type { DiscountDefinition, UpdateDiscountRequest } from '@/services/commerceDiscountService';
import type { DiscountDto, DiscountKind } from '@/types/commerce';
import { validateDecimalInput } from './decimalInput';

export const MAX_DISCOUNT_PRODUCTS = 200;

export interface DiscountForm {
  code: string;
  kind: DiscountKind;
  value: string;
  currency: string;
  isActive: boolean;
  maxRedemptions: string;
  expiresAtUtc: string;
  selectedProductsOnly: boolean;
  productIds: string[];
}

function utcInput(value: string | null): string {
  if (!value) return '';
  // ASP.NET can return an unspecified-kind DateTime without Z; the contract is UTC.
  const date = new Date(/(?:Z|[+-]\d{2}:\d{2})$/i.test(value) ? value : `${value}Z`);
  return Number.isNaN(date.getTime()) ? value : date.toISOString().slice(0, -1);
}

export function discountFormFrom(dto?: DiscountDto): DiscountForm {
  return {
    code: dto?.code ?? '',
    kind: dto?.kind ?? 'Percentage',
    value: dto ? String(dto.value) : '',
    currency: dto?.currency ?? '',
    isActive: dto?.isActive ?? true,
    maxRedemptions: dto?.maxRedemptions == null ? '' : String(dto.maxRedemptions),
    expiresAtUtc: utcInput(dto?.expiresAt ?? null),
    selectedProductsOnly: dto?.eligibleProductIds != null,
    productIds: [...(dto?.eligibleProductIds ?? [])],
  };
}

/** The input is labelled UTC, so the operator's browser timezone never changes expiry. */
function expiryFromInput(value: string): string | null {
  if (!value) return null;
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d{1,3})?)?$/.test(value) || value.startsWith('0000')) {
    throw new Error('Enter a valid expiry date and time in UTC.');
  }
  const date = new Date(`${value}Z`);
  const expectedSeconds = value.length === 16 ? `${value}:00` : value.slice(0, 19);
  // Compare calendar components as well: Date accepts 30 February by rolling into March.
  if (Number.isNaN(date.getTime()) || date.toISOString().slice(0, 19) !== expectedSeconds) {
    throw new Error('Enter a valid expiry date and time in UTC.');
  }
  return date.toISOString();
}

export function discountDefinitionFrom(form: DiscountForm, original?: DiscountDto): DiscountDefinition {
  if (form.kind !== 'Percentage' && form.kind !== 'FixedAmount') throw new Error('Choose a discount kind.');
  const valueError = validateDecimalInput(form.value, {
    scale: form.kind === 'Percentage' ? 4 : 2,
    max: form.kind === 'Percentage' ? 100 : undefined, subject: 'The discount value',
  });
  if (valueError) throw new Error(valueError);
  if (!form.value.trim() || Number(form.value) <= 0) throw new Error('The discount value must be greater than zero.');

  const currency = form.kind === 'FixedAmount' ? form.currency.trim().toUpperCase() : null;
  if (currency !== null && !/^[A-Z]{3}$/.test(currency)) throw new Error('Enter a three-letter currency code for the fixed amount.');

  const capText = form.maxRedemptions.trim();
  const cap = capText === '' ? null : Number(capText);
  if (cap !== null && (!/^\d+$/.test(capText) || !Number.isSafeInteger(cap) || cap < 0 || cap > 2_147_483_647)) {
    throw new Error('The redemption limit must be a nonnegative whole number, or blank for no limit.');
  }
  if (cap !== null && original && cap < original.timesRedeemed + original.reservedCount) {
    throw new Error('The limit cannot be below the completed and reserved redemptions.');
  }
  const eligibleProductIds = form.selectedProductsOnly ? [...new Set(form.productIds)] : null;
  if (eligibleProductIds && (eligibleProductIds.length === 0 || eligibleProductIds.length > MAX_DISCOUNT_PRODUCTS)) {
    throw new Error(`Select between 1 and ${MAX_DISCOUNT_PRODUCTS} products, or choose all eligible goods.`);
  }

  // An unrelated edit must not truncate the provider's original timestamp precision.
  const expiresAt = original && form.expiresAtUtc === utcInput(original.expiresAt)
    ? original.expiresAt
    : expiryFromInput(form.expiresAtUtc);
  return { kind: form.kind, value: Number(form.value), currency, maxRedemptions: cap, expiresAt, eligibleProductIds };
}

export function discountCreateFrom(form: DiscountForm) {
  const code = form.code.trim().toUpperCase();
  if (!code || code.length > 64 || [...form.code].some((char) => char.charCodeAt(0) < 32 || char.charCodeAt(0) === 127)) {
    throw new Error('Enter a discount code of 1 to 64 characters without control characters.');
  }
  return { ...discountDefinitionFrom(form), code };
}

export function discountUpdateFrom(form: DiscountForm, original: DiscountDto): UpdateDiscountRequest {
  return { ...discountDefinitionFrom(form, original), isActive: form.isActive, expectedVersion: original.version };
}
