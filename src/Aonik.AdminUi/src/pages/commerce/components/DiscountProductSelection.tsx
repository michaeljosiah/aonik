import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { commerceCatalogService } from '@/services/commerceCatalogService';
import type { ProductSummaryDto } from '@/types/commerce';
import { MAX_DISCOUNT_PRODUCTS } from '../lib/discountForm';

export function DiscountProductSelection({ selectedIds, onChange, disabled }: {
  selectedIds: string[];
  onChange: (ids: string[]) => void;
  disabled: boolean;
}) {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [products, setProducts] = useState<ProductSummaryDto[]>([]);
  const [names, setNames] = useState<Record<string, string>>({});
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [retry, setRetry] = useState(0);
  const pageSize = 10;

  useEffect(() => {
    let cancelled = false;
    const timer = setTimeout(() => {
      setLoading(true);
      setError(null);
      commerceCatalogService.listProducts({ page, pageSize, search: search.trim() || undefined })
        .then((result) => {
          if (cancelled) return;
          const lastPage = Math.max(1, Math.ceil(result.totalCount / pageSize));
          if (page > lastPage) { setPage(lastPage); return; }
          setProducts(result.items);
          setTotal(result.totalCount);
          setNames((previous) => ({ ...previous, ...Object.fromEntries(result.items.map((p) => [p.id, p.name])) }));
          setLoading(false);
        })
        .catch((err: unknown) => {
          if (cancelled) return;
          setProducts([]);
          setTotal(0);
          setError(err && typeof err === 'object' && 'userMessage' in err
            ? String(err.userMessage) : 'Products could not be loaded. Your selection is unchanged.');
          setLoading(false);
        });
    }, 250);
    return () => { cancelled = true; clearTimeout(timer); };
  }, [search, page, retry]);

  return (
    <div className="space-y-3 rounded-md border border-border p-3">
      <div className="space-y-1">
        <Label htmlFor="discount-product-search">Find catalog products</Label>
        <Input id="discount-product-search" value={search} maxLength={200} disabled={disabled}
          onChange={(e) => { setSearch(e.target.value); setPage(1); setLoading(true); }} placeholder="Search by name" />
        <p className="text-xs text-muted-foreground">Select a box product to cover its aggregate price. Individual dishes in a box have no separate discount price.</p>
      </div>
      {error && <Alert variant="destructive"><AlertDescription>
        {error} <Button type="button" variant="link" size="sm" disabled={disabled} onClick={() => setRetry((value) => value + 1)}>Retry</Button>
      </AlertDescription></Alert>}
      <div aria-live="polite" aria-busy={loading}>
        {loading ? <p className="py-3 text-sm text-muted-foreground">Loading products…</p> : (
          <>
            <ul className="space-y-2">
              {products.map((product) => (
                <li key={product.id} className="flex items-start gap-2">
                  <Checkbox id={`discount-product-${product.id}`} checked={selectedIds.includes(product.id)}
                    disabled={disabled || (!selectedIds.includes(product.id) && selectedIds.length >= MAX_DISCOUNT_PRODUCTS)}
                    onCheckedChange={(checked) => onChange(checked === true
                      ? [...new Set([...selectedIds, product.id])]
                      : selectedIds.filter((id) => id !== product.id))} />
                  <Label htmlFor={`discount-product-${product.id}`} className="block min-w-0 text-sm font-normal">
                    {product.name} <span className="text-xs text-muted-foreground">{product.kind} · {product.status}</span>
                  </Label>
                </li>
              ))}
            </ul>
            {!products.length && !error && <p className="text-sm text-muted-foreground">No matching products.</p>}
          </>
        )}
      </div>
      <div className="flex items-center justify-between gap-2">
        <Button type="button" variant="outline" size="sm" disabled={disabled || loading || page <= 1}
          onClick={() => { setPage((value) => value - 1); setLoading(true); }}>Previous</Button>
        <span className="text-xs text-muted-foreground">Page {page} · {total} products</span>
        <Button type="button" variant="outline" size="sm" disabled={disabled || loading || page * pageSize >= total}
          onClick={() => { setPage((value) => value + 1); setLoading(true); }}>Next</Button>
      </div>
      <p className="text-xs text-muted-foreground">{selectedIds.length} of {MAX_DISCOUNT_PRODUCTS} selected. Existing selections stay included until you remove them.</p>
      {selectedIds.length > 0 && <ul className="max-h-40 space-y-1 overflow-y-auto border-t border-border pt-2">
        {selectedIds.map((id) => <li key={id} className="flex items-center justify-between gap-2 text-xs">
          <span className="min-w-0 break-words">{names[id] ?? `Product ${id} (not loaded)`}</span>
          <Button type="button" variant="ghost" size="sm" disabled={disabled}
            aria-label={`Remove ${names[id] ?? id}`} onClick={() => onChange(selectedIds.filter((selected) => selected !== id))}>Remove</Button>
        </li>)}
      </ul>}
    </div>
  );
}
