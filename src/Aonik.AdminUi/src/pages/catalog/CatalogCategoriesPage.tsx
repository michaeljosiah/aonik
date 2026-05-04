import { useCallback, useEffect, useMemo, useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetBody,
  SheetFooter,
  SheetTitle,
  SheetDescription,
} from '@/components/ui/sheet';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/dialog';
import {
  RefreshCw,
  AlertCircle,
  Layers,
  Search,
  Plus,
  Pencil,
  Trash2,
} from 'lucide-react';
import { catalogService } from '@/services/catalogService';
import type {
  CatalogBillerCategoryItem,
  CreateCatalogBillerCategoryRequest,
  UpdateCatalogBillerCategoryRequest,
} from '@/types';
import { CountrySelect } from '@/components/ui/country-select';

// ─────────────────────────────────────────────────────────────────────────
// Catalog Categories — tenant-scoped CRUD.
//
// Reads use /catalog/billers/categories (tenant), NOT /host/...; the host
// endpoint requires Tenants.Read which TenantAdmin does not hold. Writes
// require Catalog.Write (granted to TenantAdmin by default).
// ─────────────────────────────────────────────────────────────────────────

interface FormState {
  name: string;
  countryCode: string;
  description: string;
  iconUrl: string;
  sortOrder: string;
  isActive: boolean;
}

const emptyForm: FormState = {
  name: '',
  countryCode: '',
  description: '',
  iconUrl: '',
  sortOrder: '0',
  isActive: true,
};

export function CatalogCategoriesPage() {
  const [categories, setCategories] = useState<CatalogBillerCategoryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [countryFilter, setCountryFilter] = useState('');
  const [search, setSearch] = useState('');

  // Sheet state — shared by Create and Edit; if `editing` is non-null, we're editing.
  const [sheetOpen, setSheetOpen] = useState(false);
  const [editing, setEditing] = useState<CatalogBillerCategoryItem | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  // Delete confirmation state.
  const [deleteTarget, setDeleteTarget] = useState<CatalogBillerCategoryItem | null>(null);
  const [deleting, setDeleting] = useState(false);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const categoriesResponse = await catalogService.getTenantCategories(countryFilter || undefined);
      setCategories(categoriesResponse.categories);
    } catch (err: unknown) {
      console.error('Failed to load categories:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load catalog categories.');
    } finally {
      setLoading(false);
    }
  }, [countryFilter]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const filteredCategories = useMemo(() => {
    if (!search.trim()) {
      return categories;
    }

    const lowered = search.trim().toLowerCase();
    return categories.filter((category) =>
      category.name.toLowerCase().includes(lowered) ||
      category.countryCode.toLowerCase().includes(lowered)
    );
  }, [categories, search]);

  const openCreate = () => {
    setEditing(null);
    setForm({ ...emptyForm, countryCode: countryFilter || '' });
    setFormError(null);
    setSheetOpen(true);
  };

  const openEdit = (category: CatalogBillerCategoryItem) => {
    setEditing(category);
    setForm({
      name: category.name,
      countryCode: category.countryCode,
      description: category.description ?? '',
      iconUrl: category.iconUrl ?? '',
      sortOrder: '0',
      isActive: true,
    });
    setFormError(null);
    setSheetOpen(true);
  };

  const closeSheet = () => {
    if (submitting) return;
    setSheetOpen(false);
    setEditing(null);
    setForm(emptyForm);
    setFormError(null);
  };

  const handleSubmit = async () => {
    setFormError(null);
    if (!form.name.trim()) {
      setFormError('Name is required.');
      return;
    }
    if (!editing && !form.countryCode.trim()) {
      setFormError('Country is required.');
      return;
    }

    setSubmitting(true);
    try {
      if (editing) {
        const body: UpdateCatalogBillerCategoryRequest = {
          name: form.name.trim() || undefined,
          description: form.description.trim() || null,
          iconUrl: form.iconUrl.trim() || null,
          sortOrder: form.sortOrder ? Number(form.sortOrder) : undefined,
          isActive: form.isActive,
        };
        await catalogService.updateTenantCategory(editing.categoryId, body);
      } else {
        const body: CreateCatalogBillerCategoryRequest = {
          name: form.name.trim(),
          countryCode: form.countryCode.trim().toUpperCase(),
          description: form.description.trim() || null,
          iconUrl: form.iconUrl.trim() || null,
          sortOrder: form.sortOrder ? Number(form.sortOrder) : 0,
          isActive: form.isActive,
        };
        await catalogService.createTenantCategory(body);
      }
      await loadData();
      setSheetOpen(false);
      setEditing(null);
      setForm(emptyForm);
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setFormError(message || 'Failed to save category.');
    } finally {
      setSubmitting(false);
    }
  };

  const confirmDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await catalogService.deleteTenantCategory(deleteTarget.categoryId);
      await loadData();
      setDeleteTarget(null);
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to delete category.');
    } finally {
      setDeleting(false);
    }
  };

  return (
    <div className="h-full overflow-auto p-6">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Catalog Categories</h1>
          <p className="text-[var(--color-text-secondary)]">
            Curate category groupings for billers. Filter by market and keep the catalog consistent.
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={loadData} disabled={loading} className="rounded-sm">
            <RefreshCw className={`w-4 h-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
          <Button onClick={openCreate} className="rounded-sm">
            <Plus className="w-4 h-4 mr-2" />
            New category
          </Button>
        </div>
      </div>

      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span className="flex-1">{error}</span>
            <Button variant="outline" size="sm" onClick={loadData}>
              Retry
            </Button>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="p-4">
          <div className="flex items-center justify-between gap-4">
            <div className="flex items-center gap-4 flex-1">
              <div className="relative w-96 max-w-full">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--color-text-tertiary)]" />
                <input
                  type="text"
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Search for categories"
                  className="w-full pl-10 pr-4 py-2 text-sm rounded-sm border border-[var(--color-border)] bg-transparent text-[var(--color-text-primary)] placeholder:text-[var(--color-text-tertiary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-brand-primary)] focus:border-[var(--color-brand-primary)]"
                />
              </div>

              <div className="w-56 max-w-full">
                <CountrySelect
                  value={countryFilter}
                  onChange={setCountryFilter}
                  placeholder="Filter by country"
                  includeEmpty={true}
                  emptyLabel="All countries"
                  className="w-full"
                />
              </div>
            </div>

            <Badge variant="secondary">{filteredCategories.length} categories</Badge>
          </div>

          <div className="mt-3 rounded-md border border-[var(--color-border-light)] overflow-hidden">
            {loading ? (
              <div className="p-12 text-center">
                <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                <p className="text-sm text-[var(--color-text-secondary)]">Loading categories...</p>
              </div>
            ) : filteredCategories.length === 0 ? (
              <div className="p-12 text-center">
                <div className="mb-3 flex justify-center text-[var(--color-text-tertiary)]">
                  <Layers className="w-12 h-12" />
                </div>
                <p className="text-[var(--color-text-primary)] font-medium mb-1">No categories found</p>
                <p className="text-sm text-[var(--color-text-secondary)]">
                  Click "New category" above to create your first one.
                </p>
              </div>
            ) : (
              <div className="grid gap-4 p-6 md:grid-cols-2 xl:grid-cols-3">
                {filteredCategories.map((category) => (
                  <div
                    key={category.categoryId}
                    className="border border-[var(--color-border-light)] rounded-md p-4 bg-[var(--color-surface)] shadow-sm"
                  >
                    <div className="flex items-center justify-between mb-3">
                      <Badge variant="secondary" className="font-mono">
                        {category.countryCode}
                      </Badge>
                      <div className="flex gap-1">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => openEdit(category)}
                          aria-label="Edit category"
                        >
                          <Pencil className="w-4 h-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => setDeleteTarget(category)}
                          aria-label="Delete category"
                        >
                          <Trash2 className="w-4 h-4 text-[var(--color-error)]" />
                        </Button>
                      </div>
                    </div>
                    <h3 className="text-lg font-semibold text-[var(--color-text-primary)]">{category.name}</h3>
                    <p className="text-sm text-[var(--color-text-secondary)] mb-3">
                      {category.description || 'No description provided.'}
                    </p>
                    <div className="text-xs text-[var(--color-text-tertiary)]">
                      ID: {category.categoryId.slice(0, 8)}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Create / Edit sheet */}
      <Sheet open={sheetOpen} onOpenChange={(open) => (open ? setSheetOpen(true) : closeSheet())}>
        <SheetContent className="w-[480px]">
          <SheetHeader>
            <SheetTitle>{editing ? 'Edit category' : 'New category'}</SheetTitle>
            <SheetDescription>
              {editing
                ? 'Update the category details. Country cannot be changed after creation.'
                : 'Categories group billers under a market. They are scoped to your tenant.'}
            </SheetDescription>
          </SheetHeader>
          <SheetBody className="space-y-4">
            {formError && (
              <div className="p-3 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] text-[var(--color-error)] text-sm">
                {formError}
              </div>
            )}
            <div className="space-y-1">
              <Label htmlFor="cat-name">Name *</Label>
              <Input
                id="cat-name"
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                placeholder="e.g. Utilities"
                disabled={submitting}
              />
            </div>
            {!editing && (
              <div className="space-y-1">
                <Label htmlFor="cat-country">Country *</Label>
                <CountrySelect
                  value={form.countryCode}
                  onChange={(value) => setForm({ ...form, countryCode: value })}
                  placeholder="Pick a country"
                  className="w-full"
                />
              </div>
            )}
            <div className="space-y-1">
              <Label htmlFor="cat-desc">Description</Label>
              <Textarea
                id="cat-desc"
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
                placeholder="Short description shown next to the category"
                rows={3}
                disabled={submitting}
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="cat-icon">Icon URL</Label>
              <Input
                id="cat-icon"
                value={form.iconUrl}
                onChange={(e) => setForm({ ...form, iconUrl: e.target.value })}
                placeholder="https://..."
                disabled={submitting}
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="cat-sort">Sort order</Label>
              <Input
                id="cat-sort"
                type="number"
                value={form.sortOrder}
                onChange={(e) => setForm({ ...form, sortOrder: e.target.value })}
                disabled={submitting}
              />
            </div>
            <label className="flex items-center gap-2 text-sm cursor-pointer">
              <input
                type="checkbox"
                checked={form.isActive}
                onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
                disabled={submitting}
              />
              <span>Active (visible to consumers)</span>
            </label>
          </SheetBody>
          <SheetFooter>
            <Button variant="outline" onClick={closeSheet} disabled={submitting}>
              Cancel
            </Button>
            <Button onClick={handleSubmit} disabled={submitting}>
              {submitting ? 'Saving…' : editing ? 'Save changes' : 'Create category'}
            </Button>
          </SheetFooter>
        </SheetContent>
      </Sheet>

      {/* Delete confirmation */}
      <Dialog
        open={deleteTarget !== null}
        onOpenChange={(open) => {
          if (!open && !deleting) setDeleteTarget(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete category</DialogTitle>
            <DialogDescription>
              {deleteTarget
                ? `This will delete "${deleteTarget.name}". The category cannot be deleted if any billers still reference it.`
                : ''}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteTarget(null)} disabled={deleting}>
              Cancel
            </Button>
            <Button onClick={confirmDelete} disabled={deleting}>
              {deleting ? 'Deleting…' : 'Delete'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
