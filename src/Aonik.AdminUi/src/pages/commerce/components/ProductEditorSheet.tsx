// Product editor (Spec 082 §2) — route-addressable so /commerce/products/:productId opens
// it directly and closing returns to the list URL.
//
// Saving has THREE independent write paths because the API has three: a touched-field PATCH
// for product fields, a full media replace where position is the order, and the surcharge's
// own endpoint (which requires the amount/currency pair together). They are issued only when
// their own section changed.

import { useCallback, useEffect, useState } from 'react';
import { toast } from 'sonner';

import {
  Sheet,
  SheetBody,
  SheetContent,
  SheetFooter,
  SheetHeader,
} from '@/components/ui/sheet';
import { Button } from '@/components/ui/button';
import { commerceCatalogService } from '@/services/commerceCatalogService';
import { commerceStorefrontService } from '@/services/commerceStorefrontService';
import type { AdminProductDetailDto, ProductCategoryDto } from '@/types/commerce';

import { UnderlineTabs } from '../components/UnderlineTabs';
import {
  buildMediaReplacement,
  buildProductPatch,
  formFromProduct,
  isEmptyPatch,
  isSurchargeDirty,
  surchargePayload,
  validateAttributesJson,
  type ProductEditorForm,
} from '../lib/productForm';
import { DetailsTab } from './tabs/DetailsTab';
import { MediaTab, type MediaDraft } from './tabs/MediaTab';
import { StorefrontTab } from './tabs/StorefrontTab';

type EditorTab = 'details' | 'media' | 'storefront';

interface ProductEditorSheetProps {
  productId: string;
  categories: ProductCategoryDto[];
  onClose: () => void;
  onSaved: () => void;
}

export function ProductEditorSheet({
  productId,
  categories,
  onClose,
  onSaved,
}: ProductEditorSheetProps) {
  const [product, setProduct] = useState<AdminProductDetailDto | null>(null);
  const [original, setOriginal] = useState<ProductEditorForm | null>(null);
  const [form, setForm] = useState<ProductEditorForm | null>(null);
  const [media, setMedia] = useState<MediaDraft[]>([]);
  const [originalMedia, setOriginalMedia] = useState<string>('[]');
  const [surchargeAmount, setSurchargeAmount] = useState('');
  const [surchargeCurrency, setSurchargeCurrency] = useState('');
  const [originalSurcharge, setOriginalSurcharge] = useState({ amount: '', currency: '' });
  // The storefront's canonical quote currency, and whether we know it yet. Without it a
  // surcharge cannot be edited at all: the write path checks only shape, so an unconstrained
  // currency saves and then fails quoting (V10). "Loading" is tracked separately from
  // "failed" so a slow config read does not flash the surcharge card as unavailable.
  const [storefrontCurrency, setStorefrontCurrency] = useState<string | null>(null);
  const [currencyKnown, setCurrencyKnown] = useState<'loading' | 'ready' | 'failed'>('loading');
  const [activeTab, setActiveTab] = useState<EditorTab>('details');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Every field is re-seeded from the opened product, so switching products can never leave
  // one product's edits sitting in another's editor.
  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const detail = await commerceCatalogService.getProduct(productId);
      const seeded = formFromProduct(detail);
      const mediaDraft = detail.media.map((m) => ({ url: m.url, kind: m.kind }));
      setProduct(detail);
      setOriginal(seeded);
      setForm(seeded);
      setMedia(mediaDraft);
      setOriginalMedia(JSON.stringify(mediaDraft));
      const amount = detail.unitSurcharge != null ? String(detail.unitSurcharge) : '';
      const currency = detail.unitSurchargeCurrency ?? '';
      setSurchargeAmount(amount);
      setSurchargeCurrency(currency);
      setOriginalSurcharge({ amount, currency });
      setActiveTab('details');
    } catch (err: unknown) {
      setError(readMessage(err) || 'Failed to load product.');
    } finally {
      setLoading(false);
    }
  }, [productId]);

  useEffect(() => {
    void load();
  }, [load]);

  // The storefront's canonical currency does two jobs: it is the default for a FIRST-TIME
  // surcharge (the server rejects an amount with a blank currency, so an operator must not
  // have to guess), and it is the only value a surcharge may carry — quoting rejects any
  // other with V10, so a free-typed currency would persist and then break checkout pricing.
  //
  // Seeding it is a DISPLAY default, not an edit: `isSurchargeDirty` normalises a currency
  // with no amount away, so opening an unsurcharged product and saving posts nothing here.
  useEffect(() => {
    let cancelled = false;
    commerceStorefrontService
      .getPublicStorefrontConfig()
      .then((config) => {
        if (cancelled) return;
        if (!config.currency) {
          setCurrencyKnown('failed');
          return;
        }
        setStorefrontCurrency(config.currency);
        setCurrencyKnown('ready');
      })
      .catch(() => {
        // Not knowing the canonical currency closes surcharge editing rather than falling
        // back to unchecked input — an unconstrained currency is exactly the bug this
        // control exists to prevent.
        if (!cancelled) setCurrencyKnown('failed');
      });
    return () => {
      cancelled = true;
    };
  }, []);

  // ONE expression for what the operator sees and what gets saved. Seeding the state instead
  // races the product read: whichever request lands second wins, so the config could seed the
  // currency and `load` then blank it — leaving the select showing a currency while the save
  // reported one missing.
  const effectiveSurchargeCurrency = surchargeCurrency || storefrontCurrency || '';

  const handleSave = async () => {
    if (!form || !original) return;

    // Only validate attributes the operator actually touched. A legacy row may hold malformed
    // JSON the server still serves; blocking a name-only edit until it is repaired would
    // defeat the partial update that exists precisely to leave such values alone.
    if (form.attributesJson !== original.attributesJson) {
      const attributesError = validateAttributesJson(form.attributesJson);
      if (attributesError) {
        setError(attributesError);
        setActiveTab('details');
        return;
      }
    }

    const draft = { amount: surchargeAmount, currency: effectiveSurchargeCurrency };
    const surchargeTouched = isSurchargeDirty(originalSurcharge, draft);
    const { amount: parsedAmount, currency: parsedCurrency } = surchargePayload(
      surchargeAmount,
      effectiveSurchargeCurrency,
    );
    if (surchargeTouched && parsedAmount !== null && !Number.isFinite(parsedAmount)) {
      setError('Surcharge amount must be a number.');
      setActiveTab('storefront');
      return;
    }
    // Negative is rejected server-side (V6), but by then the details and media writes have
    // already committed — a partial save caused by an input we could reject up front.
    if (surchargeTouched && parsedAmount !== null && parsedAmount < 0) {
      setError('A surcharge cannot be negative.');
      setActiveTab('storefront');
      return;
    }
    if (surchargeTouched && parsedAmount !== null && !parsedCurrency) {
      setError('A surcharge amount needs a currency — the server rejects the pair otherwise.');
      setActiveTab('storefront');
      return;
    }
    // The surcharge endpoint accepts any three-letter code, but quoting (V10) rejects one
    // that differs from the storefront currency — so a mismatch saves cleanly and then breaks
    // this product's selection quotes and checkout pricing. Caught here, while it is an edit.
    //
    // Not knowing the canonical currency BLOCKS the write rather than waving it through: an
    // unverifiable currency is the same defect as a wrong one. Clearing stays allowed, since
    // it sends no currency at all and cannot mis-denominate anything.
    if (surchargeTouched && parsedAmount !== null && !storefrontCurrency) {
      setError(
        'The storefront currency could not be read, so a surcharge cannot be set right now — ' +
          'it would save unverified and then fail quoting. Reopen the product to retry.',
      );
      setActiveTab('storefront');
      return;
    }
    if (
      surchargeTouched &&
      parsedAmount !== null &&
      storefrontCurrency &&
      parsedCurrency !== storefrontCurrency
    ) {
      setError(
        `A surcharge must be denominated in ${storefrontCurrency}, the storefront currency. ` +
          'Any other currency saves but then fails quoting for this product.',
      );
      setActiveTab('storefront');
      return;
    }

    setSaving(true);
    setError(null);

    // Three endpoints means three commit points, not one transaction. Each section that
    // lands advances its own baseline immediately, so a retry after a later failure sends
    // only what is still unsaved — otherwise retrying would reissue a media replacement that
    // already succeeded and clobber whatever another operator changed in between.
    const persisted: string[] = [];
    try {
      const patch = buildProductPatch(original, form);
      if (!isEmptyPatch(patch)) {
        await commerceCatalogService.patchProduct(productId, patch);
        setOriginal(form);
        persisted.push('details');
      }

      const mediaSnapshot = JSON.stringify(media);
      if (mediaSnapshot !== originalMedia) {
        await commerceCatalogService.replaceProductMedia(productId, buildMediaReplacement(media));
        setOriginalMedia(mediaSnapshot);
        persisted.push('media');
      }

      if (surchargeTouched) {
        await commerceCatalogService.setUnitSurcharge(productId, parsedAmount, parsedCurrency);
        setOriginalSurcharge(draft);
        persisted.push('surcharge');
      }

      toast.success('Product saved');
      onSaved();
      onClose();
    } catch (err: unknown) {
      const message = readMessage(err) || 'Failed to save product.';
      // Say what did land. "Save failed" alone would be untrue when part of the product has
      // already changed, and the operator needs that to decide whether to retry or reload.
      setError(
        persisted.length > 0
          ? `${message} Already saved: ${persisted.join(', ')} — retrying will not resend those.`
          : message,
      );
      // A partial write means the list behind the sheet is now stale too.
      if (persisted.length > 0) onSaved();
    } finally {
      setSaving(false);
    }
  };

  // Nothing to edit: the detail read failed (a stale deep link, a 404, a dropped connection).
  const unloaded = !loading && (!form || !product);

  return (
    <Sheet
      open
      // Every dismissal path — Escape, the overlay, the header close button — routes through
      // here, so a save in flight must block them all. Closing mid-save would look like an
      // abandoned action while the writes carried on off-screen.
      onOpenChange={(open) => !open && !saving && onClose()}
    >
      <SheetContent size="lg">
        <SheetHeader
          title={product?.name ?? 'Product'}
          subtitle={product ? product.slug : undefined}
        />

        <SheetBody>
          {error && (
            <div className="mb-3 rounded border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
              {error}
            </div>
          )}

          {loading ? (
            <p className="py-8 text-center text-sm text-[var(--color-text-secondary)]">Loading…</p>
          ) : !form || !product ? (
            <div className="flex flex-col items-center gap-3 py-10">
              <p className="text-sm text-[var(--color-text-secondary)]">
                This product could not be loaded.
              </p>
              <Button variant="outline" onClick={() => void load()}>
                Try again
              </Button>
            </div>
          ) : (
            // Frozen while saving: the payloads were built from the state at click time, so
            // a keystroke or reorder made during a slow save would be silently discarded by
            // the success path that closes the sheet and reports the product saved.
            <fieldset disabled={saving} className="flex min-w-0 flex-col gap-4 border-0 p-0">
              <UnderlineTabs
                tabs={[
                  { key: 'details', label: 'Details' },
                  { key: 'media', label: 'Media', badge: media.length },
                  { key: 'storefront', label: 'Storefront' },
                ]}
                active={activeTab}
                onChange={(key) => setActiveTab(key as EditorTab)}
              />

              {activeTab === 'details' && (
                <DetailsTab
                  slug={product.slug}
                  kind={product.kind}
                  form={form}
                  categories={categories}
                  onChange={(patch) => setForm({ ...form, ...patch })}
                />
              )}

              {activeTab === 'media' && <MediaTab items={media} onChange={setMedia} />}

              {activeTab === 'storefront' && (
                <StorefrontTab
                  key={product.id}
                  product={product}
                  // A fieldset disables controls, not anchors — the deep-surface links need
                  // to be told separately, or they would navigate away mid-save.
                  frozen={saving}
                  form={form}
                  onChange={(patch) => setForm({ ...form, ...patch })}
                  surchargeAmount={surchargeAmount}
                  surchargeCurrency={effectiveSurchargeCurrency}
                  storefrontCurrency={storefrontCurrency}
                  currencyKnown={currencyKnown}
                  onSurchargeChange={(next) => {
                    if (next.amount !== undefined) setSurchargeAmount(next.amount);
                    if (next.currency !== undefined) setSurchargeCurrency(next.currency);
                  }}
                />
              )}
            </fieldset>
          )}
        </SheetBody>

        <SheetFooter>
          <Button variant="outline" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          {/* Save stays disabled when nothing loaded — handleSave would return immediately,
              so an enabled button would promise an action it cannot perform. */}
          <Button onClick={() => void handleSave()} disabled={saving || loading || unloaded}>
            {saving ? 'Saving…' : 'Save'}
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

function readMessage(err: unknown): string {
  return err && typeof err === 'object' && 'userMessage' in err
    ? String((err as { userMessage?: string }).userMessage ?? '')
    : '';
}
