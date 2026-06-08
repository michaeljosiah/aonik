import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
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
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { CountrySelect } from '@/components/ui/country-select';
import { Pill } from '@/components/layout/aonik/Pill';
import {
  RefreshCw,
  AlertCircle,
  Search,
  Plus,
  Download,
  LayoutGrid,
  List as ListIcon,
  ChevronRight,
  Pencil,
  Check,
  X,
} from 'lucide-react';
import { catalogService } from '@/services/catalogService';
import type {
  CatalogBillerSummaryItem,
  CatalogBillerCategoryItem,
  CatalogCountryItem,
  CreateCatalogBillerRequest,
  UpdateCatalogBillerRequest,
  BillerImportSummaryResponse,
} from '@/types';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { BillerDetailDrawer } from './BillerDetailDrawer';
import { BillerImportWizard } from './BillerImportWizard';
import { billerColor, billerInitials, connectorColor, formatSyncTime } from './billerVisuals';

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

const DASH = '—';
const PAGE_SIZE = 100;

interface ImportFlash extends BillerImportSummaryResponse {
  connectorType: string;
}

export function CatalogBillersPage() {
  const navigate = useNavigate();
  const [billers, setBillers] = useState<CatalogBillerSummaryItem[]>([]);
  const [categories, setCategories] = useState<CatalogBillerCategoryItem[]>([]);
  const [countries, setCountries] = useState<CatalogCountryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [selectedCategoryId, setSelectedCategoryId] = useState<string>('all');
  const [search, setSearch] = useState('');
  const [view, setView] = useState<'grid' | 'list'>('grid');

  const [detail, setDetail] = useState<CatalogBillerSummaryItem | null>(null);
  const [wizardOpen, setWizardOpen] = useState(false);
  const [flash, setFlash] = useState<ImportFlash | null>(null);

  // Create / Edit sheet — shared; `editing` non-null ⇒ editing.
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
      const [countriesResponse, categoriesResponse, firstPage] = await Promise.all([
        catalogService.getTenantCountries(false),
        catalogService.getTenantCategories(undefined),
        catalogService.getTenantBillers({ page: 1, pageSize: PAGE_SIZE }),
      ]);

      let allBillers = firstPage.billers;
      const totalPages = firstPage.pagination.totalPages;
      if (totalPages > 1) {
        const rest = await Promise.all(
          Array.from({ length: totalPages - 1 }, (_, i) =>
            catalogService.getTenantBillers({ page: i + 2, pageSize: PAGE_SIZE }),
          ),
        );
        allBillers = allBillers.concat(...rest.map((r) => r.billers));
      }

      setCountries(countriesResponse.countries);
      setCategories(categoriesResponse.categories);
      setBillers(allBillers);
    } catch (err: unknown) {
      console.error('Failed to load billers:', err);
      setError(resolveError(err, 'Failed to load catalog billers.'));
    } finally {
      setLoading(false);
      setInitialLoad(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const categoryMap = useMemo(
    () => new Map(categories.map((c) => [c.categoryId, c])),
    [categories],
  );
  const countryMap = useMemo(
    () => new Map(countries.map((c) => [c.countryCode, c])),
    [countries],
  );

  const categoryCounts = useMemo(() => {
    const counts = new Map<string, number>();
    for (const b of billers) counts.set(b.categoryId, (counts.get(b.categoryId) ?? 0) + 1);
    return counts;
  }, [billers]);

  const filtered = useMemo(() => {
    let list = billers;
    if (selectedCategoryId !== 'all') list = list.filter((b) => b.categoryId === selectedCategoryId);
    const q = search.trim().toLowerCase();
    if (q) {
      list = list.filter(
        (b) =>
          b.name.toLowerCase().includes(q) ||
          (b.providerBillerCode ?? '').toLowerCase().includes(q),
      );
    }
    return list;
  }, [billers, selectedCategoryId, search]);

  const activeCount = useMemo(() => billers.filter((b) => b.isActive).length, [billers]);
  const inactiveCount = billers.length - activeCount;

  const formCountryCategories = useMemo(() => {
    if (!form.countryCode) return [] as CatalogBillerCategoryItem[];
    const cc = form.countryCode.toUpperCase();
    return categories.filter((c) => c.countryCode === cc);
  }, [categories, form.countryCode]);

  const openCreate = () => {
    setEditing(null);
    setForm({ ...emptyForm });
    setFormError(null);
    setSheetOpen(true);
  };

  const openEdit = (biller: CatalogBillerSummaryItem) => {
    setDetail(null);
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
      setFormError(resolveError(err, 'Failed to save biller.'));
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
      setSheetOpen(false);
      setEditing(null);
    } catch (err: unknown) {
      setError(resolveError(err, 'Failed to delete biller.'));
    } finally {
      setDeleting(false);
    }
  };

  const handleImported = (summary: BillerImportSummaryResponse, connectorType: string) => {
    setWizardOpen(false);
    setFlash({ ...summary, connectorType });
    loadData();
  };

  if (initialLoad) {
    return <PageLoadingScreen message="Loading billers" />;
  }

  return (
    <div className="h-full grid grid-cols-[220px_1fr] overflow-hidden">
      {/* Category rail */}
      <div className="border-r border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-3.5 overflow-auto flex flex-col gap-0.5">
        <div className="text-[10px] font-bold uppercase tracking-wider text-[var(--color-text-tertiary)] px-2 pt-1 pb-2">
          Categories
        </div>
        <RailButton
          label="All billers"
          count={billers.length}
          active={selectedCategoryId === 'all'}
          onClick={() => setSelectedCategoryId('all')}
        />
        {categories.map((c) => (
          <RailButton
            key={c.categoryId}
            label={c.name}
            count={categoryCounts.get(c.categoryId) ?? 0}
            active={selectedCategoryId === c.categoryId}
            onClick={() => setSelectedCategoryId(c.categoryId)}
          />
        ))}
        <div className="h-px bg-[var(--color-border-light)] mx-1 my-3" />
        <button
          onClick={() => navigate('/catalog/categories')}
          className="flex items-center justify-center gap-2 px-2.5 py-2 rounded-md border border-dashed border-[var(--color-border-medium)] text-[12.5px] text-[var(--color-text-secondary)] hover:bg-[var(--color-surface)]"
        >
          <Plus className="w-3 h-3" /> Manage categories
        </button>
      </div>

      {/* Main */}
      <div className="p-6 overflow-auto flex flex-col gap-4">
        {/* Header */}
        <div className="flex items-end justify-between gap-4">
          <div>
            <h1 className="text-[22px] font-bold text-[var(--color-text-primary)] tracking-tight">Billers</h1>
            <p className="text-[13px] text-[var(--color-text-secondary)] mt-0.5">
              The catalog of providers your operators can pay through. Routing, fees and policy live here — orders consume this.
            </p>
          </div>
          <div className="flex gap-2">
            <div className="relative">
              <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-[var(--color-text-tertiary)]" />
              <input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search billers"
                className="w-52 pl-8 pr-3 py-[7px] text-[12.5px] rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)] text-[var(--color-text-primary)] placeholder:text-[var(--color-text-tertiary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-brand-primary)]"
              />
            </div>
            <Button variant="outline" size="sm" onClick={() => setWizardOpen(true)} className="rounded-md">
              <Download className="w-3.5 h-3.5 mr-1.5" /> Import from partner
            </Button>
            <Button size="sm" onClick={openCreate} className="rounded-md">
              <Plus className="w-3.5 h-3.5 mr-1.5" /> Add biller
            </Button>
          </div>
        </div>

        {error && (
          <div className="rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] p-3 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span className="flex-1">{error}</span>
            <Button variant="outline" size="sm" onClick={loadData}>
              Retry
            </Button>
          </div>
        )}

        {/* Post-import flash */}
        {flash && (
          <div className="flex items-center gap-2.5 px-3.5 py-2.5 rounded-r-lg bg-[var(--color-success-light)] border-l-[3px] border-[var(--color-success)]">
            <span className="w-[22px] h-[22px] rounded-full bg-[var(--color-success)] text-white grid place-items-center flex-none">
              <Check className="w-3 h-3" />
            </span>
            <div className="text-[12.5px] text-[var(--color-text-primary)]">
              Imported from <b>{flash.connectorType}</b> —{' '}
              <b className="font-mono">{flash.billersCreated}</b> created ·{' '}
              <b className="font-mono">{flash.billersUpdated}</b> updated ·{' '}
              <b className="font-mono">{flash.deactivated}</b> deactivated.
            </div>
            <div className="flex-1" />
            <button
              onClick={() => setFlash(null)}
              aria-label="Dismiss"
              className="text-[var(--color-text-tertiary)] hover:text-[var(--color-text-secondary)]"
            >
              <X className="w-3.5 h-3.5" />
            </button>
          </div>
        )}

        {/* Stats strip — catalogue-backed counts; operational metrics deferred (Spec 040 O7). */}
        <div className="grid grid-cols-4 gap-3">
          <StatCard label="Active billers" value={String(activeCount)} sub={`${inactiveCount} inactive`} />
          <StatCard label="Tx this month" value={DASH} sub="not yet tracked" />
          <StatCard label="Avg success rate" value={DASH} sub="not yet tracked" />
          <StatCard label="Avg time-to-receipt" value={DASH} sub="not yet tracked" />
        </div>

        {/* View toggle */}
        <div className="flex items-center justify-between">
          <div className="text-[12.5px] text-[var(--color-text-secondary)]">
            Showing <b className="text-[var(--color-text-primary)]">{filtered.length}</b> billers
            {selectedCategoryId !== 'all' && (
              <>
                {' '}in <b className="text-[var(--color-text-primary)]">{categoryMap.get(selectedCategoryId)?.name ?? ''}</b>
              </>
            )}
          </div>
          <div className="flex items-center gap-1.5">
            <span className="text-[11px] text-[var(--color-text-tertiary)] mr-1">View</span>
            {(['grid', 'list'] as const).map((v) => {
              const on = view === v;
              const Icon = v === 'grid' ? LayoutGrid : ListIcon;
              return (
                <button
                  key={v}
                  onClick={() => setView(v)}
                  className="flex items-center gap-1 px-2 py-[5px] rounded-md text-[11.5px] font-medium border"
                  style={{
                    background: on ? 'var(--color-surface-inset)' : 'transparent',
                    color: on ? 'var(--color-text-primary)' : 'var(--color-text-tertiary)',
                    borderColor: on ? 'var(--color-border-medium)' : 'var(--color-border-light)',
                  }}
                >
                  <Icon className="w-3 h-3" />
                  {v[0].toUpperCase() + v.slice(1)}
                </button>
              );
            })}
          </div>
        </div>

        {loading ? (
          <div className="p-12 text-center">
            <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
            <p className="text-sm text-[var(--color-text-secondary)]">Loading billers…</p>
          </div>
        ) : view === 'grid' ? (
          <div className="grid grid-cols-3 gap-3">
            {filtered.map((b) => (
              <BillerCard
                key={b.billerId}
                biller={b}
                categoryName={categoryMap.get(b.categoryId)?.name}
                onClick={() => setDetail(b)}
              />
            ))}
            <button
              onClick={() => setWizardOpen(true)}
              className="border-[1.5px] border-dashed border-[var(--color-border-medium)] rounded-lg min-h-[168px] flex flex-col items-center justify-center gap-1.5 text-[var(--color-text-tertiary)] hover:bg-[var(--color-surface-inset)]"
            >
              <Download className="w-[18px] h-[18px]" />
              <div className="text-[12.5px] font-medium">Import from a partner</div>
              <div className="text-[11px]">Pull a connector's live catalogue</div>
            </button>
          </div>
        ) : (
          <BillerList
            billers={filtered}
            categoryMap={categoryMap}
            onRowClick={(b) => setDetail(b)}
          />
        )}
      </div>

      {/* Detail drawer */}
      {detail && (
        <BillerDetailDrawer
          biller={detail}
          categoryName={categoryMap.get(detail.categoryId)?.name}
          countryName={countryMap.get(detail.countryCode)?.name}
          onClose={() => setDetail(null)}
          onEdit={openEdit}
          onViewDetails={(b) => navigate(`/catalog/billers/${b.billerId}`)}
        />
      )}

      {/* Import wizard */}
      {wizardOpen && (
        <BillerImportWizard onClose={() => setWizardOpen(false)} onImported={handleImported} />
      )}

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
                placeholder="e.g. Ikeja Electric"
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
                  placeholder="+234 ..."
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
            {editing && (
              <Button
                variant="outline"
                onClick={() => setDeleteTarget(editing)}
                disabled={submitting}
                className="mr-auto text-[var(--color-error)] border-[var(--color-error)]"
              >
                Delete
              </Button>
            )}
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
              {deleteTarget ? `This will delete "${deleteTarget.name}" and hide it from consumers.` : ''}
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

// ── Sub-components ────────────────────────────────────────────────────────────

function RailButton({
  label,
  count,
  active,
  onClick,
}: {
  label: string;
  count: number;
  active: boolean;
  onClick: () => void;
}) {
  return (
    <button
      onClick={onClick}
      className="flex items-center justify-between px-2.5 py-2 rounded-md text-left text-[12.5px]"
      style={{
        background: active ? 'var(--color-brand-primary-10)' : 'transparent',
        color: active ? 'var(--color-brand-primary)' : 'var(--color-text-secondary)',
        fontWeight: active ? 600 : 500,
      }}
    >
      <span className="truncate">{label}</span>
      <span className="font-mono text-[11px] opacity-70 ml-2 flex-none">{count}</span>
    </button>
  );
}

function StatCard({ label, value, sub }: { label: string; value: string; sub: string }) {
  return (
    <div className="bg-[var(--color-surface)] border border-[var(--color-border-light)] rounded-lg px-4 py-3.5">
      <div className="text-[11px] text-[var(--color-text-tertiary)] uppercase tracking-wide font-semibold">{label}</div>
      <div className="text-[22px] font-bold text-[var(--color-text-primary)] mt-1">{value}</div>
      <div className="text-[11.5px] text-[var(--color-text-secondary)] mt-0.5">{sub}</div>
    </div>
  );
}

function ProvenanceLine({ biller }: { biller: CatalogBillerSummaryItem }) {
  const imported = (biller.sourceConnectors?.length ?? 0) > 0;
  const sourceLabel = biller.sourceConnectors?.join(', ') ?? '';
  return (
    <div className="flex items-center gap-1.5 text-[10.5px] text-[var(--color-text-tertiary)] border-t border-[var(--color-border-light)] pt-2 flex-wrap">
      {imported ? (
        <Download className="w-[11px] h-[11px]" style={{ color: connectorColor(sourceLabel) }} />
      ) : (
        <Pencil className="w-[11px] h-[11px]" />
      )}
      {imported ? (
        <span>
          Imported · <b style={{ color: connectorColor(sourceLabel) }}>{sourceLabel}</b>
          {biller.providerBillerCode && (
            <>
              {' · '}
              <span className="font-mono">{biller.providerBillerCode}</span>
            </>
          )}
        </span>
      ) : (
        <span>Manual entry</span>
      )}
      {!biller.isActive && (
        <>
          <span>·</span>
          <span>{formatSyncTime(biller.lastSyncedAt) ? `dropped ${formatSyncTime(biller.lastSyncedAt)}` : 'no longer offered'}</span>
        </>
      )}
    </div>
  );
}

function BillerCard({
  biller,
  categoryName,
  onClick,
}: {
  biller: CatalogBillerSummaryItem;
  categoryName?: string;
  onClick: () => void;
}) {
  const tile = billerColor(biller.name);
  return (
    <div
      onClick={onClick}
      className="bg-[var(--color-surface)] border border-[var(--color-border-light)] rounded-lg p-3.5 flex flex-col gap-2.5 cursor-pointer hover:border-[var(--color-border-medium)] hover:shadow-sm transition"
      style={{ opacity: biller.isActive ? 1 : 0.66 }}
    >
      <div className="flex items-start justify-between gap-2.5">
        <div className="flex gap-2.5 items-center min-w-0">
          <div
            className="w-9 h-9 rounded-lg flex items-center justify-center text-white font-bold text-xs flex-none"
            style={{ background: tile, filter: biller.isActive ? 'none' : 'grayscale(1)' }}
          >
            {billerInitials(biller.name)}
          </div>
          <div className="min-w-0">
            <div className="text-[13.5px] font-semibold text-[var(--color-text-primary)] truncate">{biller.name}</div>
            <div className="text-[11px] text-[var(--color-text-tertiary)] flex items-center gap-1.5 mt-0.5">
              <span className="truncate">{categoryName ?? 'Uncategorized'}</span>
              <span>·</span>
              <span>{biller.countryCode}</span>
            </div>
          </div>
        </div>
        <Pill tone={biller.isActive ? 'success' : 'muted'} dot>
          {biller.isActive ? 'Active' : 'Inactive'}
        </Pill>
      </div>

      {/* Metrics — operational, deferred (Spec 040 O7) */}
      <div className="grid grid-cols-3 gap-1.5 py-2.5 border-y border-dashed border-[var(--color-border-light)]">
        {[['Tx / mo', DASH], ['Success', DASH], ['p50 ETA', DASH]].map(([l, v]) => (
          <div key={l}>
            <div className="text-[10px] text-[var(--color-text-tertiary)] uppercase tracking-wide font-semibold">{l}</div>
            <div className="font-mono text-[13px] font-semibold text-[var(--color-text-primary)] mt-0.5">{v}</div>
          </div>
        ))}
      </div>

      {/* Partners + fee */}
      <div className="flex items-center justify-between gap-2">
        <div className="flex gap-1 flex-wrap">
          {(biller.sourceConnectors ?? []).map((p) => (
            <span
              key={p}
              className="text-[10.5px] px-1.5 py-0.5 bg-[var(--color-surface-inset)] border border-[var(--color-border-light)] rounded text-[var(--color-text-secondary)]"
            >
              {p}
            </span>
          ))}
          {(biller.sourceConnectors?.length ?? 0) === 0 && (
            <span className="text-[10.5px] text-[var(--color-text-tertiary)]">No partners</span>
          )}
        </div>
        <span className="text-[11px] text-[var(--color-text-tertiary)] font-mono">{DASH}</span>
      </div>

      <ProvenanceLine biller={biller} />
    </div>
  );
}

function BillerList({
  billers,
  categoryMap,
  onRowClick,
}: {
  billers: CatalogBillerSummaryItem[];
  categoryMap: Map<string, CatalogBillerCategoryItem>;
  onRowClick: (b: CatalogBillerSummaryItem) => void;
}) {
  return (
    <div className="bg-[var(--color-surface)] border border-[var(--color-border-light)] rounded-lg overflow-hidden">
      <div className="grid grid-cols-[1fr_140px_150px_90px_110px_30px] gap-3 px-3.5 py-2.5 bg-[var(--color-surface-inset)] border-b border-[var(--color-border-light)] text-[10px] font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)]">
        <div>Biller</div>
        <div>Category</div>
        <div>Source</div>
        <div>Provider code</div>
        <div>Status</div>
        <div />
      </div>
      {billers.map((b, i) => {
        const imported = (b.sourceConnectors?.length ?? 0) > 0;
        const sourceLabel = b.sourceConnectors?.join(', ') ?? '';
        const tile = billerColor(b.name);
        return (
          <div
            key={b.billerId}
            onClick={() => onRowClick(b)}
            className="grid grid-cols-[1fr_140px_150px_90px_110px_30px] gap-3 px-3.5 py-2.5 items-center text-[12.5px] cursor-pointer hover:bg-[var(--color-surface-inset)]"
            style={{
              borderTop: i ? '1px solid var(--color-border-light)' : 'none',
              opacity: b.isActive ? 1 : 0.66,
            }}
          >
            <div className="flex items-center gap-2.5 min-w-0">
              <div
                className="w-7 h-7 rounded flex items-center justify-center text-white font-bold text-[10px] flex-none"
                style={{ background: tile, filter: b.isActive ? 'none' : 'grayscale(1)' }}
              >
                {billerInitials(b.name)}
              </div>
              <div className="min-w-0">
                <div className="text-[var(--color-text-primary)] font-medium truncate">{b.name}</div>
                <div className="text-[11px] text-[var(--color-text-tertiary)]">{b.countryCode}</div>
              </div>
            </div>
            <div className="text-[var(--color-text-secondary)] truncate">
              {categoryMap.get(b.categoryId)?.name ?? 'Uncategorized'}
            </div>
            <div className="flex items-center gap-1.5 min-w-0">
              {imported ? (
                <Download className="w-[11px] h-[11px] flex-none" style={{ color: connectorColor(sourceLabel) }} />
              ) : (
                <Pencil className="w-[11px] h-[11px] flex-none" />
              )}
              <span
                className="truncate"
                style={{ color: imported ? connectorColor(sourceLabel) : 'var(--color-text-tertiary)', fontWeight: 500 }}
              >
                {imported ? sourceLabel : 'Manual'}
              </span>
            </div>
            <div className="font-mono text-[var(--color-text-secondary)] truncate">{b.providerBillerCode ?? DASH}</div>
            <div>
              <Pill tone={b.isActive ? 'success' : 'muted'} dot>
                {b.isActive ? 'Active' : 'Inactive'}
              </Pill>
            </div>
            <div className="text-[var(--color-text-tertiary)]">
              <ChevronRight className="w-3.5 h-3.5" />
            </div>
          </div>
        );
      })}
      {billers.length === 0 && (
        <div className="p-10 text-center text-sm text-[var(--color-text-secondary)]">No billers match.</div>
      )}
    </div>
  );
}

function resolveError(err: unknown, fallback: string): string {
  if (err && typeof err === 'object' && 'userMessage' in err) {
    const msg = String((err as { userMessage?: string }).userMessage ?? '');
    if (msg) return msg;
  }
  return fallback;
}
