import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
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
  Building2,
  Search,
  ArrowUpRight,
  Plus,
  Pencil,
  Trash2,
} from 'lucide-react';
import { catalogService } from '@/services/catalogService';
import type {
  CatalogBillerSummaryItem,
  CatalogBillerCategoryItem,
  CatalogCountryItem,
  CreateCatalogBillerRequest,
  UpdateCatalogBillerRequest,
} from '@/types';
import { DataTablePagination } from '@/components/ui/data-table';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { CountrySelect } from '@/components/ui/country-select';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';

interface FormState {
  name: string;
  countryCode: string;
  categoryId: string;
  description: string;
  logoUrl: string;
  bannerUrl: string;
  supportPhone: string;
  supportEmail: string;
  sortOrder: string;
  isActive: boolean;
  isFeatured: boolean;
}

const emptyForm: FormState = {
  name: '',
  countryCode: '',
  categoryId: '',
  description: '',
  logoUrl: '',
  bannerUrl: '',
  supportPhone: '',
  supportEmail: '',
  sortOrder: '0',
  isActive: true,
  isFeatured: false,
};

export function CatalogBillersPage() {
  const navigate = useNavigate();
  const [billers, setBillers] = useState<CatalogBillerSummaryItem[]>([]);
  const [categories, setCategories] = useState<CatalogBillerCategoryItem[]>([]);
  const [countries, setCountries] = useState<CatalogCountryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [countryFilter, setCountryFilter] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [pageSize, setPageSize] = useState(12);

  // Sheet state — shared by Create and Edit; if `editing` is non-null, we're editing.
  const [sheetOpen, setSheetOpen] = useState(false);
  const [editing, setEditing] = useState<CatalogBillerSummaryItem | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const [deleteTarget, setDeleteTarget] = useState<CatalogBillerSummaryItem | null>(null);
  const [deleting, setDeleting] = useState(false);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [countriesResponse, categoriesResponse, billersResponse] = await Promise.all([
        catalogService.getTenantCountries(false),
        catalogService.getTenantCategories(countryFilter || undefined),
        catalogService.getTenantBillers({
          countryCode: countryFilter || undefined,
          categoryId: categoryFilter || undefined,
          search: search || undefined,
          page,
          pageSize,
        }),
      ]);

      setCountries(countriesResponse.countries);
      setCategories(categoriesResponse.categories);
      setBillers(billersResponse.billers);
      setTotalCount(billersResponse.pagination.totalCount || 0);
    } catch (err: unknown) {
      console.error('Failed to load billers:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load catalog billers.');
    } finally {
      setLoading(false);
      setInitialLoad(false);
    }
  }, [countryFilter, categoryFilter, search, page, pageSize]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  useEffect(() => {
    setPage(1);
  }, [countryFilter, categoryFilter, search]);

  const handlePageSizeChange = (newPageSize: number) => {
    setPageSize(newPageSize);
    setPage(1);
  };

  const categoryMap = useMemo(() => {
    return new Map(categories.map((category) => [category.categoryId, category]));
  }, [categories]);

  const countryMap = useMemo(() => {
    return new Map(countries.map((country) => [country.countryCode, country]));
  }, [countries]);

  const activeFilters = useMemo(() => {
    return [countryFilter, categoryFilter, search].filter(Boolean).length;
  }, [countryFilter, categoryFilter, search]);

  // Categories restricted to the picked country (for the form's category dropdown).
  const formCountryCategories = useMemo(() => {
    if (!form.countryCode) return [] as CatalogBillerCategoryItem[];
    const cc = form.countryCode.toUpperCase();
    return categories.filter((c) => c.countryCode === cc);
  }, [categories, form.countryCode]);

  const openCreate = () => {
    setEditing(null);
    setForm({ ...emptyForm, countryCode: countryFilter || '' });
    setFormError(null);
    setSheetOpen(true);
  };

  const openEdit = (biller: CatalogBillerSummaryItem) => {
    setEditing(biller);
    setForm({
      name: biller.name,
      countryCode: biller.countryCode,
      categoryId: biller.categoryId,
      description: '',
      logoUrl: biller.logoUrl ?? '',
      bannerUrl: '',
      supportPhone: '',
      supportEmail: '',
      sortOrder: '0',
      isActive: biller.isActive,
      isFeatured: biller.isFeatured,
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
    if (!editing && !form.categoryId) {
      setFormError('Category is required.');
      return;
    }

    setSubmitting(true);
    try {
      if (editing) {
        const body: UpdateCatalogBillerRequest = {
          name: form.name.trim() || undefined,
          categoryId: form.categoryId || undefined,
          description: form.description.trim() || null,
          logoUrl: form.logoUrl.trim() || null,
          bannerUrl: form.bannerUrl.trim() || null,
          supportPhone: form.supportPhone.trim() || null,
          supportEmail: form.supportEmail.trim() || null,
          isActive: form.isActive,
          isFeatured: form.isFeatured,
          sortOrder: form.sortOrder ? Number(form.sortOrder) : undefined,
        };
        await catalogService.updateTenantBiller(editing.billerId, body);
      } else {
        const body: CreateCatalogBillerRequest = {
          name: form.name.trim(),
          countryCode: form.countryCode.trim().toUpperCase(),
          categoryId: form.categoryId,
          description: form.description.trim() || null,
          logoUrl: form.logoUrl.trim() || null,
          bannerUrl: form.bannerUrl.trim() || null,
          supportPhone: form.supportPhone.trim() || null,
          supportEmail: form.supportEmail.trim() || null,
          isActive: form.isActive,
          isFeatured: form.isFeatured,
          sortOrder: form.sortOrder ? Number(form.sortOrder) : 0,
        };
        await catalogService.createTenantBiller(body);
      }
      await loadData();
      setSheetOpen(false);
      setEditing(null);
      setForm(emptyForm);
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setFormError(message || 'Failed to save biller.');
    } finally {
      setSubmitting(false);
    }
  };

  const confirmDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await catalogService.deleteTenantBiller(deleteTarget.billerId);
      await loadData();
      setDeleteTarget(null);
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to delete biller.');
    } finally {
      setDeleting(false);
    }
  };

  if (initialLoad) {
    return <PageLoadingScreen message="Loading billers" />;
  }

  return (
    <div className="h-full overflow-auto p-6">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Catalog Billers</h1>
          <p className="text-[var(--color-text-secondary)]">
            Manage the billers your tenant offers for collections. Group them under categories, mark featured ones, and toggle availability.
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={loadData} disabled={loading} className="rounded-sm">
            <RefreshCw className={`w-4 h-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
          <Button onClick={openCreate} className="rounded-sm">
            <Plus className="w-4 h-4 mr-2" />
            New biller
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
                  placeholder="Search for billers"
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

              <Select
                value={categoryFilter || undefined}
                onValueChange={(value) => setCategoryFilter(value === '__all__' ? '' : value)}
              >
                <SelectTrigger aria-label="Filter by category" className="h-9 rounded-sm w-56">
                  <SelectValue placeholder="Filter by category" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">All categories</SelectItem>
                  {categories.map((category) => (
                    <SelectItem key={category.categoryId} value={category.categoryId}>
                      {category.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="mt-3 rounded-md border border-[var(--color-border-light)] overflow-hidden">
            {loading ? (
              <div className="p-12 text-center">
                <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                <p className="text-sm text-[var(--color-text-secondary)]">Loading billers...</p>
              </div>
            ) : billers.length === 0 ? (
              <div className="p-12 text-center">
                <div className="mb-3 flex justify-center text-[var(--color-text-tertiary)]">
                  <Building2 className="w-12 h-12" />
                </div>
                <p className="text-[var(--color-text-primary)] font-medium mb-1">No billers found</p>
                <p className="text-sm text-[var(--color-text-secondary)]">
                  Click "New biller" above to add your first one.
                </p>
              </div>
            ) : (
              <div className="divide-y divide-[var(--color-border-light)]">
                {billers.map((biller) => {
                  const category = categoryMap.get(biller.categoryId);
                  const country = countryMap.get(biller.countryCode);
                  return (
                    <div
                      key={biller.billerId}
                      className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4 px-4 py-4 hover:bg-[var(--color-surface-inset)] transition-colors"
                    >
                      <div className="flex items-start gap-4">
                        <div className="w-12 h-12 rounded-md bg-[var(--color-brand-primary-light)] flex items-center justify-center">
                          <Building2 className="w-6 h-6 text-[var(--color-brand-primary)]" />
                        </div>
                        <div>
                          <div className="flex items-center gap-2 flex-wrap">
                            <h3 className="text-lg font-semibold text-[var(--color-text-primary)]">{biller.name}</h3>
                            {!biller.isActive && (
                              <Badge variant="outline" className="text-[var(--color-text-tertiary)]">
                                Inactive
                              </Badge>
                            )}
                            {biller.isFeatured && (
                              <Badge className="bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)]">
                                Featured
                              </Badge>
                            )}
                          </div>
                          <p className="text-sm text-[var(--color-text-secondary)]">
                            {category?.name ?? 'Uncategorized'} • {country?.name ?? biller.countryCode}
                          </p>
                          <div className="text-xs text-[var(--color-text-tertiary)] mt-1">
                            ID: {biller.billerId.slice(0, 8)}
                          </div>
                        </div>
                      </div>

                      <div className="flex items-center gap-2">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => openEdit(biller)}
                          aria-label="Edit biller"
                        >
                          <Pencil className="w-4 h-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => setDeleteTarget(biller)}
                          aria-label="Delete biller"
                        >
                          <Trash2 className="w-4 h-4 text-[var(--color-error)]" />
                        </Button>
                        <Button
                          variant="outline"
                          className="rounded-sm"
                          onClick={() => navigate(`/catalog/billers/${biller.billerId}`)}
                        >
                          View
                          <ArrowUpRight className="w-4 h-4 ml-2" />
                        </Button>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          <div className="pt-4">
            <DataTablePagination
              pageNumber={page}
              pageSize={pageSize}
              totalCount={totalCount}
              onPageChange={setPage}
              onPageSizeChange={handlePageSizeChange}
              className="px-0 border-t-0"
            />
          </div>

          {activeFilters > 0 && (
            <div className="mt-3 flex flex-wrap items-center gap-3">
              <Badge variant="outline">{activeFilters} filters applied</Badge>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Create / Edit sheet */}
      <Sheet open={sheetOpen} onOpenChange={(open) => (open ? setSheetOpen(true) : closeSheet())}>
        <SheetContent size="lg">
          <SheetHeader
            title={editing ? 'Edit biller' : 'New biller'}
            subtitle={
              editing
                ? 'Update biller details. Country cannot be changed after creation.'
                : 'A biller represents a payee customers can send funds to. Pick a country and category first.'
            }
          />
          <SheetBody className="space-y-4">
            {formError && (
              <div className="p-3 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] text-[var(--color-error)] text-sm">
                {formError}
              </div>
            )}

            <div className="space-y-1">
              <Label htmlFor="biller-name">Name *</Label>
              <Input
                id="biller-name"
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                placeholder="e.g. Pacific Gas & Electric"
                disabled={submitting}
              />
            </div>

            {!editing && (
              <div className="space-y-1">
                <Label htmlFor="biller-country">Country *</Label>
                <CountrySelect
                  value={form.countryCode}
                  onChange={(value) => setForm({ ...form, countryCode: value, categoryId: '' })}
                  placeholder="Pick a country"
                  className="w-full"
                />
              </div>
            )}

            <div className="space-y-1">
              <Label htmlFor="biller-category">Category *</Label>
              <Select
                value={form.categoryId || undefined}
                onValueChange={(value) => setForm({ ...form, categoryId: value })}
                disabled={!form.countryCode && !editing}
              >
                <SelectTrigger className="h-9 rounded-sm w-full">
                  <SelectValue
                    placeholder={
                      !editing && !form.countryCode
                        ? 'Pick a country first'
                        : formCountryCategories.length === 0 && !editing
                          ? 'No categories for this country yet'
                          : 'Pick a category'
                    }
                  />
                </SelectTrigger>
                <SelectContent>
                  {(editing ? categories : formCountryCategories).map((category) => (
                    <SelectItem key={category.categoryId} value={category.categoryId}>
                      {category.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {!editing && form.countryCode && formCountryCategories.length === 0 && (
                <p className="text-xs text-[var(--color-text-tertiary)] mt-1">
                  No categories exist for this country yet. Create one on the Categories page first.
                </p>
              )}
            </div>

            <div className="space-y-1">
              <Label htmlFor="biller-desc">Description</Label>
              <Textarea
                id="biller-desc"
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
                rows={2}
                disabled={submitting}
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <Label htmlFor="biller-phone">Support phone</Label>
                <Input
                  id="biller-phone"
                  value={form.supportPhone}
                  onChange={(e) => setForm({ ...form, supportPhone: e.target.value })}
                  placeholder="+1 555 ..."
                  disabled={submitting}
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="biller-email">Support email</Label>
                <Input
                  id="biller-email"
                  type="email"
                  value={form.supportEmail}
                  onChange={(e) => setForm({ ...form, supportEmail: e.target.value })}
                  placeholder="support@..."
                  disabled={submitting}
                />
              </div>
            </div>

            <div className="space-y-1">
              <Label htmlFor="biller-logo">Logo URL</Label>
              <Input
                id="biller-logo"
                value={form.logoUrl}
                onChange={(e) => setForm({ ...form, logoUrl: e.target.value })}
                placeholder="https://..."
                disabled={submitting}
              />
            </div>

            <div className="space-y-1">
              <Label htmlFor="biller-sort">Sort order</Label>
              <Input
                id="biller-sort"
                type="number"
                value={form.sortOrder}
                onChange={(e) => setForm({ ...form, sortOrder: e.target.value })}
                disabled={submitting}
              />
            </div>

            <div className="flex flex-col gap-2 pt-2">
              <label className="flex items-center gap-2 text-sm cursor-pointer">
                <input
                  type="checkbox"
                  checked={form.isActive}
                  onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
                  disabled={submitting}
                />
                <span>Active (visible to consumers)</span>
              </label>
              <label className="flex items-center gap-2 text-sm cursor-pointer">
                <input
                  type="checkbox"
                  checked={form.isFeatured}
                  onChange={(e) => setForm({ ...form, isFeatured: e.target.checked })}
                  disabled={submitting}
                />
                <span>Featured</span>
              </label>
            </div>
          </SheetBody>
          <SheetFooter>
            <Button variant="outline" onClick={closeSheet} disabled={submitting}>
              Cancel
            </Button>
            <Button onClick={handleSubmit} disabled={submitting}>
              {submitting ? 'Saving…' : editing ? 'Save changes' : 'Create biller'}
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
            <DialogTitle>Delete biller</DialogTitle>
            <DialogDescription>
              {deleteTarget
                ? `This will delete "${deleteTarget.name}" and hide it from consumers.`
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
