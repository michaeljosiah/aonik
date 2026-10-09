import { useEffect, useState } from 'react';
import type { AxiosRequestConfig } from 'axios';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Sheet, SheetBody, SheetContent, SheetFooter, SheetHeader } from '@/components/ui/sheet';
import { commerceCatalogService } from '@/services/commerceCatalogService';
import type { ProductSummaryDto } from '@/types/commerce';
import { merchandisingError } from '../lib/merchandisingDraft';

export function CollectionProductPicker({ selectedIds, onAdd, onClose, isCurrent, canWrite, requestConfig }: {
  selectedIds: string[]; onAdd: (product: ProductSummaryDto) => void; onClose: () => void;
  isCurrent: () => boolean; canWrite: boolean; requestConfig: AxiosRequestConfig;
}) {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [products, setProducts] = useState<ProductSummaryDto[]>([]);
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
      commerceCatalogService.listProducts({ page, pageSize, search: search.trim() || undefined }, requestConfig).then((result) => {
        if (cancelled || !isCurrent()) return;
        const lastPage = Math.max(1, Math.ceil(result.totalCount / pageSize));
        if (page > lastPage) { setPage(lastPage); return; }
        setProducts(result.items); setTotal(result.totalCount); setLoading(false);
      }).catch((err: unknown) => {
        if (cancelled || !isCurrent()) return;
        setProducts([]); setTotal(0); setLoading(false);
        setError(merchandisingError(err, 'Products could not be loaded.'));
      });
    }, 250);
    return () => { cancelled = true; clearTimeout(timer); };
  }, [search, page, retry, isCurrent, requestConfig]);

  return <Sheet open onOpenChange={(open) => { if (!open) onClose(); }}><SheetContent size="md">
    <SheetHeader title="Add collection members" subtitle="Draft products can be staged; only active products appear publicly." />
    <SheetBody className="space-y-4">
      <Label htmlFor="collection-product-search">Find products</Label>
      <Input id="collection-product-search" value={search} maxLength={200} disabled={!canWrite}
        onChange={(event) => { setSearch(event.target.value); setPage(1); setLoading(true); }} placeholder="Search by name" />
      {error && <Alert variant="destructive"><AlertDescription>{error}
        <Button variant="link" size="sm" onClick={() => setRetry((value) => value + 1)}>Retry</Button>
      </AlertDescription></Alert>}
      {loading ? <p role="status" className="text-sm text-muted-foreground">Loading products…</p> : <ul className="space-y-3">
        {products.map((product) => <li key={product.id} className="flex items-center justify-between gap-3">
          <span className="min-w-0 text-sm">{product.name}<span className="block text-xs text-muted-foreground">{product.status} · {product.kind}</span></span>
          <Button size="sm" variant="outline" disabled={!canWrite || selectedIds.includes(product.id)}
            onClick={() => onAdd(product)}>{selectedIds.includes(product.id) ? 'Added' : 'Add'}</Button>
        </li>)}
        {!products.length && !error && <li className="text-sm text-muted-foreground">No matching products.</li>}
      </ul>}
      <div className="flex items-center justify-between gap-2">
        <Button variant="outline" size="sm" disabled={loading || page <= 1} onClick={() => { setPage((value) => value - 1); setLoading(true); }}>Previous</Button>
        <span className="text-xs text-muted-foreground">Page {page} · {total} products</span>
        <Button variant="outline" size="sm" disabled={loading || page * pageSize >= total} onClick={() => { setPage((value) => value + 1); setLoading(true); }}>Next</Button>
      </div>
    </SheetBody><SheetFooter><Button onClick={onClose}>Done</Button></SheetFooter>
  </SheetContent></Sheet>;
}
