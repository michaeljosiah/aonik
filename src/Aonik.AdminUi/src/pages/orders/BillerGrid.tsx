// Visual biller picker — port of `BillerGrid` from
// templates/aonik-admin-starterkit/screens/orders.jsx.
//
// Renders a 4-column grid of biller "logo" cards with category filter chips
// and search. The catalog DTO does not yet carry brand color / symbol, so we
// derive a deterministic 2-letter symbol and hashed teal/coral palette from
// the biller name. Replace with real biller artwork once the DTO is extended.

import { Check, Search } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { CatalogBillerCategoryItem, CatalogBillerSummaryItem } from '@/types';

export interface BillerGridProps {
  billers: CatalogBillerSummaryItem[];
  categories: CatalogBillerCategoryItem[];
  selectedBillerId: string;
  selectedCategoryId: string;
  search: string;
  onSelectBiller: (billerId: string) => void;
  onSelectCategory: (categoryId: string) => void;
  onSearchChange: (value: string) => void;
  loading?: boolean;
}

const BRAND_PALETTE = [
  '#055a60', // teal
  '#eb5c37', // coral
  '#1e4d8c', // navy
  '#1f7a5e', // forest
  '#7b76b6', // violet
  '#0097a9', // patrol
  '#e8a838', // amber
  '#d97706', // orange
];

function hash(value: string): number {
  let h = 0;
  for (let i = 0; i < value.length; i += 1) {
    h = (h * 31 + value.charCodeAt(i)) >>> 0;
  }
  return h;
}

function deriveSymbol(name: string): string {
  return (name || '?')
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0])
    .join('')
    .toUpperCase()
    .slice(0, 2);
}

function brandColor(name: string): string {
  return BRAND_PALETTE[hash(name) % BRAND_PALETTE.length];
}

function BillerLogoMark({ name, size = 38 }: { name: string; size?: number }) {
  return (
    <div
      className="flex flex-none items-center justify-center font-[family-name:var(--font-brand)] font-extrabold leading-none text-white"
      style={{
        width: size,
        height: size,
        borderRadius: 8,
        background: brandColor(name),
        fontSize: Math.round(size * 0.32),
        letterSpacing: '-0.01em',
      }}
    >
      {deriveSymbol(name)}
    </div>
  );
}

export function BillerGrid({
  billers,
  categories,
  selectedBillerId,
  selectedCategoryId,
  search,
  onSelectBiller,
  onSelectCategory,
  onSearchChange,
  loading,
}: BillerGridProps) {
  const allCategories = [{ id: '', name: 'All' }, ...categories.map((c) => ({ id: c.categoryId, name: c.name }))];

  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap gap-1">
        {allCategories.map((cat) => {
          const active = cat.id === selectedCategoryId;
          return (
            <button
              key={cat.id || 'all'}
              type="button"
              onClick={() => onSelectCategory(cat.id)}
              className={cn(
                'rounded-md px-2.5 py-1 text-[11.5px] font-medium transition-colors',
                active
                  ? 'bg-[var(--color-brand-primary)] text-white'
                  : 'bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]',
              )}
            >
              {cat.name}
            </button>
          );
        })}
      </div>

      <div className="relative">
        <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3 w-3 -translate-y-1/2 text-[var(--color-text-tertiary)]" />
        <input
          type="text"
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
          placeholder="Search billers…"
          className="aonik-input h-[34px] pl-8 text-[12.5px]"
        />
      </div>

      <div className="grid max-h-[220px] grid-cols-2 gap-2 overflow-auto pr-0.5 sm:grid-cols-3 lg:grid-cols-4">
        {loading && (
          <div className="col-span-full py-6 text-center text-[12px] text-[var(--color-text-tertiary)]">
            Loading billers…
          </div>
        )}
        {!loading && billers.length === 0 && (
          <div className="col-span-full py-6 text-center text-[12px] text-[var(--color-text-tertiary)]">
            No billers found
          </div>
        )}
        {!loading &&
          billers.map((biller) => {
            const sel = biller.billerId === selectedBillerId;
            const category = categories.find((c) => c.categoryId === biller.categoryId);
            return (
              <button
                key={biller.billerId}
                type="button"
                onClick={() => onSelectBiller(sel ? '' : biller.billerId)}
                className={cn(
                  'relative flex flex-col items-center gap-1.5 rounded-[10px] px-2 py-3 transition-colors',
                  sel
                    ? 'border-[2px] border-[var(--color-brand-primary)] bg-[var(--color-brand-primary-10)]'
                    : 'border-[1.5px] border-[var(--color-border-light)] bg-[var(--color-surface)] hover:border-[var(--color-border)]',
                )}
              >
                {sel && (
                  <Check className="absolute right-1.5 top-1.5 h-3 w-3 text-[var(--color-brand-primary)]" />
                )}
                <BillerLogoMark name={biller.name} size={38} />
                <div className="line-clamp-1 text-center text-[11px] font-semibold leading-tight text-[var(--color-text-primary)]">
                  {biller.name}
                </div>
                <div className="text-center text-[10px] text-[var(--color-text-tertiary)]">
                  {category?.name ?? biller.countryCode}
                </div>
              </button>
            );
          })}
      </div>
    </div>
  );
}
