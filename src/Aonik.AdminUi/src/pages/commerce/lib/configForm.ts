import type { StorefrontConfigDto, UpdateStorefrontConfigRequest } from '@/types/commerce';
import { validateDecimalInput } from './decimalInput';

export const CONFIG_FIELDS = ['recommendedChoiceLabel', 'resultsPageSize', 'backToTopTriggerJson',
  'deliveryListAmount', 'deliveryChargedAmount', 'defaultBoxSlug', 'extrasCollectionSlug'] as const;
export type ConfigField = typeof CONFIG_FIELDS[number];
export type ConfigDraft = Record<ConfigField, string>;
export type ConfigTouched = Partial<Record<ConfigField, true>>;
export const CLEARABLE_CONFIG_FIELDS = ['recommendedChoiceLabel', 'backToTopTriggerJson', 'defaultBoxSlug'] as const;

export function configDraftFrom(config: StorefrontConfigDto): ConfigDraft {
  return {
    recommendedChoiceLabel: config.recommendedChoiceLabel,
    resultsPageSize: String(config.resultsPageSize),
    backToTopTriggerJson: JSON.stringify(config.backToTopTrigger, null, 2),
    deliveryListAmount: String(config.delivery.listAmount),
    deliveryChargedAmount: String(config.delivery.chargedAmount),
    defaultBoxSlug: config.defaultBoxSlug ?? '',
    extrasCollectionSlug: config.extrasCollectionSlug ?? '',
  };
}

export function validateConfigDraft(touched: ConfigTouched, values: ConfigDraft): string | null {
  if (touched.recommendedChoiceLabel && values.recommendedChoiceLabel.length > 4000)
    return 'The recommended label can contain at most 4000 characters.';
  if (touched.resultsPageSize && (!/^\d+$/.test(values.resultsPageSize.trim())
      || Number(values.resultsPageSize) < 1 || Number(values.resultsPageSize) > 200))
    return 'Results per page must be a whole number from 1 to 200.';
  for (const field of ['deliveryListAmount', 'deliveryChargedAmount'] as const) {
    if (!touched[field]) continue;
    if (!values[field].trim()) return 'Enter a delivery amount. Use 0 for free; a blank does not reset it.';
    const invalid = validateDecimalInput(values[field], { scale: 2, max: 999999.99, subject: 'A delivery amount' });
    if (invalid) return invalid.includes('is stored to')
      ? 'Delivery amounts must use at most two decimal places so card checkout can charge the exact amount.' : invalid;
  }
  if (touched.backToTopTriggerJson) {
    if (values.backToTopTriggerJson.length > 4000) return 'The back-to-top JSON can contain at most 4000 characters.';
    if (values.backToTopTriggerJson.trim()) {
      try {
        const parsed: unknown = JSON.parse(values.backToTopTriggerJson);
        if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return 'The back-to-top trigger must be a JSON object.';
      } catch { return 'The back-to-top trigger must be valid JSON.'; }
    }
  }
  if (touched.defaultBoxSlug && values.defaultBoxSlug !== '' && !/^[a-z0-9-]{1,160}$/.test(values.defaultBoxSlug.trim().toLowerCase()))
    return 'Select a valid default box, or clear its override.';
  if (touched.extrasCollectionSlug && !/^[a-z0-9-]{1,64}$/.test(values.extrasCollectionSlug.trim().toLowerCase()))
    return 'Select an extras collection. This setting cannot be cleared.';
  return null;
}

/** Only authored fields are sent; neither untouched values nor numeric reset sentinels are inferred. */
export function buildConfigCommand(touched: ConfigTouched, values: ConfigDraft): UpdateStorefrontConfigRequest {
  const invalid = validateConfigDraft(touched, values);
  if (invalid) throw new Error(invalid);
  const command: UpdateStorefrontConfigRequest = {};
  if (touched.recommendedChoiceLabel) command.recommendedChoiceLabel = values.recommendedChoiceLabel;
  if (touched.resultsPageSize) command.resultsPageSize = Number(values.resultsPageSize);
  if (touched.backToTopTriggerJson) command.backToTopTriggerJson = values.backToTopTriggerJson.trim() ? values.backToTopTriggerJson : '';
  if (touched.deliveryListAmount) command.deliveryListAmount = Number(values.deliveryListAmount);
  if (touched.deliveryChargedAmount) command.deliveryChargedAmount = Number(values.deliveryChargedAmount);
  if (touched.defaultBoxSlug) command.defaultBoxSlug = values.defaultBoxSlug.trim().toLowerCase();
  if (touched.extrasCollectionSlug) command.extrasCollectionSlug = values.extrasCollectionSlug.trim().toLowerCase();
  return command;
}
