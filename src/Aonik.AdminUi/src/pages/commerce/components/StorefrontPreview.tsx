import { Card as AonikCard, Pill } from '@/components/layout/aonik';
import type { StorefrontConfigDto } from '@/types/commerce';
import { formatUnsignedAmount } from './signedAmountFormat';

export function StorefrontPreview({ config, pending }: { config: StorefrontConfigDto; pending: boolean }) {
  const money = (value: number) => formatUnsignedAmount(value, config.currency);
  const example = config.box?.currency === config.currency ? config.box.presets[0] : undefined;
  return <aside className="space-y-4" aria-label="Saved storefront preview">
    <div><h2 className="text-base font-semibold">Saved storefront preview</h2>
      <p className="text-sm text-muted-foreground">{pending
        ? 'Unsaved changes are not shown here.' : 'Values from the saved storefront document.'}</p></div>
    <AonikCard title="Personaliser badge">
      <div className="flex flex-wrap items-center gap-2"><span className="rounded-md border border-primary px-3 py-2 text-sm">Standard choice</span>
        <Pill tone="success">{config.recommendedChoiceLabel}</Pill></div>
      <p className="mt-3 text-xs text-muted-foreground">Example option chip using the saved recommended-choice label.</p>
    </AonikCard>
    <AonikCard title="Delivery line">
      <div className="flex items-center justify-between gap-3 text-sm"><span>Delivery</span><span className="flex items-center gap-2">
        <s className="text-muted-foreground">{money(config.delivery.listAmount)}</s>
        {config.delivery.chargedAmount === 0 ? <Pill tone="success">Free</Pill> : <strong>{money(config.delivery.chargedAmount)}</strong>}
      </span></div>
      {example && <p className="mt-3 border-t border-border pt-3 text-xs text-muted-foreground">
        Example: {example.size}-portion box {money(example.price)} + delivery {money(config.delivery.chargedAmount)} = {money(example.price + config.delivery.chargedAmount)}. Excludes extras and discounts.
      </p>}
    </AonikCard>
    <AonikCard title="Box size step">
      {!config.box ? <p className="rounded-md border border-dashed border-border p-3 text-sm text-muted-foreground">No default box plan is live. There is no size step to preview.</p> : <>
        <p className="mb-3 text-xs text-muted-foreground">{config.box.minSize}–{config.box.maxSize} portions · {config.box.currency}</p>
        {config.box.currency !== config.currency && <p className="mb-3 text-sm text-destructive">The box plan uses {config.box.currency}; storefront delivery amounts use {config.currency}.</p>}
        <div className="grid gap-2 sm:grid-cols-2">
          {config.box.presets.map((preset) => <div key={preset.size} className="space-y-1 rounded-md border border-border p-3 text-sm">
            <p className="font-medium">{preset.size} portions · {formatUnsignedAmount(preset.price, config.box!.currency)}</p>
            {preset.badge && <Pill tone="muted">{preset.badge}</Pill>}
            {preset.blurb && <p className="text-xs text-muted-foreground">{preset.blurb}</p>}
            {preset.saving != null && <p className="text-xs">Authored saving: {formatUnsignedAmount(preset.saving, config.box!.currency)}</p>}
          </div>)}
        </div>
        {config.box.presets.length === 0 && <p className="text-sm text-muted-foreground">This plan has no authored presets.</p>}
      </>}
    </AonikCard>
    <AonikCard title="Back-to-top trigger">
      <pre className="overflow-x-auto whitespace-pre-wrap break-words text-xs">{JSON.stringify(config.backToTopTrigger, null, 2)}</pre>
    </AonikCard>
  </aside>;
}
