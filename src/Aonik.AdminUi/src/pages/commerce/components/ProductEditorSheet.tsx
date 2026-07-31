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

  // The storefront config's canonical currency is the sensible default for a FIRST-TIME
  // surcharge — the server rejects an amount with a blank currency, so an operator must not
  // have to guess it. Never overwrites a currency the product already carries.
  //
  // Seeding it is a DISPLAY default, not an edit: `isSurchargeDirty` normalises a currency
  // with no amount away, so opening an unsurcharged product and saving posts nothing here.
  useEffect(() => {
    if (loading || surchargeCurrency) return;
    let cancelled = false;
    commerceStorefrontService
      .getPublicStorefrontConfig()
      .then((config) => {
        if (!cancelled && config.currency) setSurchargeCurrency(config.currency);
      })
      .catch(() => {
        /* leave blank — the operator types it, and the server enforces the pair */
      });
    return () => {
      cancelled = true;
    };
  }, [loading, surchargeCurrency]);

  const handleSave = async () => {
    if (!form || !original) return;

    const attributesError = validateAttributesJson(form.attributesJson);
    if (attributesError) {
      setError(attributesError);
      setActiveTab('details');
      return;
    }

    const draft = { amount: surchargeAmount, currency: surchargeCurrency };
    const surchargeTouched = isSurchargeDirty(originalSurcharge, draft);
    const { amount: parsedAmount, currency: parsedCurrency } = surchargePayload(
      surchargeAmount,
      surchargeCurrency,
    );
    if (surchargeTouched && parsedAmount !== null && !Number.isFinite(parsedAmount)) {
      setError('Surcharge amount must be a number.');
      setActiveTab('storefront');
      return;
    }
    if (surchargeTouched && parsedAmount !== null && !parsedCurrency) {
      setError('A surcharge amount needs a currency — the server rejects the pair otherwise.');
      setActiveTab('storefront');
      return;
    }

    setSaving(true);
    setError(null);
    try {
      const patch = buildProductPatch(original, form);
      if (!isEmptyPatch(patch)) {
        await commerceCatalogService.patchProduct(productId, patch);
      }

      const mediaLines = buildMediaReplacement(media);
      if (JSON.stringify(media) !== originalMedia) {
        await commerceCatalogService.replaceProductMedia(productId, mediaLines);
      }

      if (surchargeTouched) {
        await commerceCatalogService.setUnitSurcharge(productId, parsedAmount, parsedCurrency);
      }

      toast.success('Product saved');
      onSaved();
      onClose();
    } catch (err: unknown) {
      setError(readMessage(err) || 'Failed to save product.');
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
            <div className="flex flex-col gap-4">
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
                  form={form}
                  onChange={(patch) => setForm({ ...form, ...patch })}
                  surchargeAmount={surchargeAmount}
                  surchargeCurrency={surchargeCurrency}
                  onSurchargeChange={(next) => {
                    if (next.amount !== undefined) setSurchargeAmount(next.amount);
                    if (next.currency !== undefined) setSurchargeCurrency(next.currency);
                  }}
                />
              )}
            </div>
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
