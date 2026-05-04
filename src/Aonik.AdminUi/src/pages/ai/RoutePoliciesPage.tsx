import { useState, useCallback, useEffect, useRef } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Switch } from '@/components/ui/switch';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  DataTable,
  DataTableHeader,
  DataTablePagination,
  DataTableRowActions,
  type ColumnDef,
  type DataTableAction,
} from '@/components/ui/data-table';
import {
  Route,
  Plus,
  AlertCircle,
  Loader2,
  Globe,
  Building2,
  CheckCircle2,
  Eye,
  Pencil,
  Trash2,
} from 'lucide-react';
import { routePolicyService, aiModelService } from '@/services/aiService';
import type {
  RoutePolicyResponse,
  CreateRoutePolicyRequest,
  UpdateRoutePolicyRequest,
  AiModelResponse,
} from '@/types/ai';

// ── Helpers ──────────────────────────────────────────────────────────

const getErrorMessage = (err: unknown, fallback: string) => {
  if (err && typeof err === 'object' && 'userMessage' in err) {
    const message = String((err as { userMessage?: string }).userMessage ?? '').trim();
    if (message) return message;
  }
  return fallback;
};

const riskTierOptions = ['Low', 'Standard', 'Medium', 'High'];
const dataSensitivityOptions = ['Public', 'Internal', 'Confidential', 'Restricted'];

const riskTierColor = (tier: string) => {
  switch (tier.toLowerCase()) {
    case 'low': return 'bg-green-500/10 text-green-700 border-green-200';
    case 'standard': return 'bg-blue-500/10 text-blue-700 border-blue-200';
    case 'medium': return 'bg-yellow-500/10 text-yellow-700 border-yellow-200';
    case 'high': return 'bg-red-500/10 text-red-700 border-red-200';
    default: return 'bg-gray-500/10 text-gray-700 border-gray-200';
  }
};

const PAGE_SIZE = 20;

// ── Main Page ────────────────────────────────────────────────────────

export function RoutePoliciesPage() {
  const [policies, setPolicies] = useState<RoutePolicyResponse[]>([]);
  const [models, setModels] = useState<AiModelResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [scopeFilter, setScopeFilter] = useState('');
  const [pageNumber, setPageNumber] = useState(1);
  const requestIdRef = useRef(0);

  // Create/Edit dialog
  const [showDialog, setShowDialog] = useState(false);
  const [editingPolicy, setEditingPolicy] = useState<RoutePolicyResponse | null>(null);
  const [saving, setSaving] = useState(false);

  // Detail dialog
  const [detailPolicy, setDetailPolicy] = useState<RoutePolicyResponse | null>(null);
  const [showDetailDialog, setShowDetailDialog] = useState(false);

  // Delete confirmation dialog
  const [deleteTarget, setDeleteTarget] = useState<RoutePolicyResponse | null>(null);
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [deleting, setDeleting] = useState(false);

  // Form state
  const [formUseCase, setFormUseCase] = useState('');
  const [formRiskTier, setFormRiskTier] = useState('Standard');
  const [formDataSensitivity, setFormDataSensitivity] = useState('Internal');
  const [formCostCeiling, setFormCostCeiling] = useState(0);
  const [formPrimaryModelId, setFormPrimaryModelId] = useState('');
  const [formIsActive, setFormIsActive] = useState(true);

  // ── Data Loading ──────────────────────────────────────────────────

  const loadData = useCallback(async () => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);
    try {
      const [policiesResult, modelsResult] = await Promise.all([
        routePolicyService.list(),
        aiModelService.list(),
      ]);
      if (requestIdRef.current !== requestId) return;
      setPolicies(policiesResult);
      setModels(modelsResult);
    } catch (err) {
      if (requestIdRef.current !== requestId) return;
      setError(getErrorMessage(err, 'Failed to load route policies'));
    } finally {
      if (requestIdRef.current === requestId) setLoading(false);
    }
  }, []);

  useEffect(() => { loadData(); }, [loadData]);

  // ── Filtering & Pagination ────────────────────────────────────────

  const filteredPolicies = policies.filter((p) => {
    const matchesSearch = !searchQuery ||
      p.useCase.toLowerCase().includes(searchQuery.toLowerCase()) ||
      (p.primaryModelName ?? '').toLowerCase().includes(searchQuery.toLowerCase());
    const matchesScope = !scopeFilter ||
      (scopeFilter === 'global' && !p.isOverride) ||
      (scopeFilter === 'override' && p.isOverride);
    return matchesSearch && matchesScope;
  });

  useEffect(() => { setPageNumber(1); }, [searchQuery, scopeFilter]);

  const paginatedPolicies = filteredPolicies.slice(
    (pageNumber - 1) * PAGE_SIZE,
    pageNumber * PAGE_SIZE,
  );

  // ── Stats ─────────────────────────────────────────────────────────

  const totalPolicies = filteredPolicies.length;
  const activePolicies = filteredPolicies.filter((p) => p.isActive).length;
  const globalPolicies = filteredPolicies.filter((p) => !p.isOverride).length;
  const overridePolicies = filteredPolicies.filter((p) => p.isOverride).length;

  // ── Form helpers ──────────────────────────────────────────────────

  const resetForm = () => {
    setFormUseCase('');
    setFormRiskTier('Standard');
    setFormDataSensitivity('Internal');
    setFormCostCeiling(0);
    setFormPrimaryModelId('');
    setFormIsActive(true);
  };

  const openCreate = () => {
    resetForm();
    setEditingPolicy(null);
    setShowDialog(true);
  };

  const openEdit = (policy: RoutePolicyResponse, e?: React.MouseEvent) => {
    e?.stopPropagation();
    setFormUseCase(policy.useCase);
    setFormRiskTier(policy.riskTier);
    setFormDataSensitivity(policy.dataSensitivity);
    setFormCostCeiling(policy.costCeiling);
    setFormPrimaryModelId(policy.primaryModelId ?? '');
    setFormIsActive(policy.isActive);
    setEditingPolicy(policy);
    setShowDialog(true);
  };

  const openDetail = (policy: RoutePolicyResponse) => {
    setDetailPolicy(policy);
    setShowDetailDialog(true);
  };

  const confirmDelete = (policy: RoutePolicyResponse, e?: React.MouseEvent) => {
    e?.stopPropagation();
    setDeleteTarget(policy);
    setShowDeleteDialog(true);
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      if (editingPolicy) {
        const request: UpdateRoutePolicyRequest = {
          riskTier: formRiskTier,
          dataSensitivity: formDataSensitivity,
          costCeiling: formCostCeiling,
          primaryModelId: formPrimaryModelId || null,
          isActive: formIsActive,
        };
        await routePolicyService.update(editingPolicy.id, request);
      } else {
        const request: CreateRoutePolicyRequest = {
          useCase: formUseCase,
          riskTier: formRiskTier,
          dataSensitivity: formDataSensitivity,
          costCeiling: formCostCeiling,
          primaryModelId: formPrimaryModelId,
          isActive: formIsActive,
        };
        await routePolicyService.create(request);
      }
      setShowDialog(false);
      loadData();
    } catch (err) {
      setError(getErrorMessage(err, 'Failed to save route policy'));
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await routePolicyService.delete(deleteTarget.id);
      setShowDeleteDialog(false);
      setDeleteTarget(null);
      loadData();
    } catch (err) {
      setError(getErrorMessage(err, 'Failed to delete route policy'));
    } finally {
      setDeleting(false);
    }
  };

  // ── Table columns ─────────────────────────────────────────────────

  const getRowActions = (policy: RoutePolicyResponse): DataTableAction[] => [
    {
      label: 'View Details',
      icon: <Eye className="w-4 h-4" />,
      onClick: () => openDetail(policy),
    },
    {
      label: 'Edit',
      icon: <Pencil className="w-4 h-4" />,
      onClick: () => openEdit(policy),
    },
    {
      label: 'Delete',
      icon: <Trash2 className="w-4 h-4" />,
      onClick: () => confirmDelete(policy),
      variant: 'danger' as const,
    },
  ];

  const columns: ColumnDef<RoutePolicyResponse>[] = [
    {
      id: 'useCase',
      header: 'Use Case',
      accessorKey: 'useCase',
      sortable: true,
      cell: (policy) => (
        <div>
          <p className="font-medium font-mono text-sm text-[var(--color-text-primary)]">{policy.useCase}</p>
          <div className="flex items-center gap-1.5 mt-0.5">
            {policy.isOverride ? (
              <span className="inline-flex items-center gap-1 text-[11px] text-blue-600">
                <Building2 className="w-3 h-3" /> Tenant override
              </span>
            ) : (
              <span className="inline-flex items-center gap-1 text-[11px] text-[var(--color-text-tertiary)]">
                <Globe className="w-3 h-3" /> Global default
              </span>
            )}
          </div>
        </div>
      ),
    },
    {
      id: 'model',
      header: 'Model',
      accessorFn: (row) => row.primaryModelName ?? '',
      sortable: true,
      cell: (policy) => (
        policy.primaryModelName ? (
          <Badge variant="outline" className="text-xs font-mono">{policy.primaryModelName}</Badge>
        ) : (
          <span className="text-xs text-[var(--color-text-tertiary)] italic">Not set</span>
        )
      ),
    },
    {
      id: 'riskTier',
      header: 'Risk Tier',
      accessorKey: 'riskTier',
      sortable: true,
      cell: (policy) => (
        <Badge className={`text-xs ${riskTierColor(policy.riskTier)}`}>{policy.riskTier}</Badge>
      ),
    },
    {
      id: 'dataSensitivity',
      header: 'Sensitivity',
      accessorKey: 'dataSensitivity',
      sortable: true,
      cell: (policy) => (
        <span className="text-sm text-[var(--color-text-secondary)]">{policy.dataSensitivity}</span>
      ),
    },
    {
      id: 'status',
      header: 'Status',
      accessorKey: 'isActive',
      sortable: true,
      cell: (policy) => (
        policy.isActive ? (
          <Badge className="text-xs bg-green-500/10 text-green-700 border-green-200">Active</Badge>
        ) : (
          <Badge variant="secondary" className="text-xs">Inactive</Badge>
        )
      ),
    },
  ];

  // ── Grouped models for selector ───────────────────────────────────

  const activeModels = models.filter((m) => m.isActive);
  const groupedModels = activeModels.reduce<Record<string, AiModelResponse[]>>((acc, m) => {
    const provider = m.providerName ?? 'Unknown';
    if (!acc[provider]) acc[provider] = [];
    acc[provider].push(m);
    return acc;
  }, {});

  // ── Breadcrumb ────────────────────────────────────────────────────
  // ── Render ────────────────────────────────────────────────────────

  return (
    <div className="h-full overflow-auto p-6">

      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Route Policies</h1>
          <p className="text-[var(--color-text-secondary)]">
            Configure which AI model is used for each use case. Tenant overrides take precedence over global defaults.
          </p>
        </div>
        <Button onClick={openCreate} className="rounded-sm">
          <Plus className="w-4 h-4 mr-2" />
          New Policy
        </Button>
      </div>

      {/* Stat cards */}
      <div className="grid gap-4 mb-6 md:grid-cols-2 xl:grid-cols-4">
        <Card className="rounded-none border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <CardContent className="p-4 flex items-center gap-3">
            <div className="w-10 h-10 rounded-full flex items-center justify-center bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]">
              <Route className="w-5 h-5" />
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Total policies</p>
              <p className="text-2xl font-semibold text-[var(--color-text-primary)]">{totalPolicies}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">Matches current filters</p>
            </div>
          </CardContent>
        </Card>
        <Card className="rounded-none border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <CardContent className="p-4 flex items-center gap-3">
            <div className="w-10 h-10 rounded-full flex items-center justify-center bg-[var(--color-success-light)] text-[var(--color-success)]">
              <CheckCircle2 className="w-5 h-5" />
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Active</p>
              <p className="text-2xl font-semibold text-[var(--color-text-primary)]">{activePolicies}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">On this page</p>
            </div>
          </CardContent>
        </Card>
        <Card className="rounded-none border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <CardContent className="p-4 flex items-center gap-3">
            <div className="w-10 h-10 rounded-full flex items-center justify-center bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]">
              <Globe className="w-5 h-5" />
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Global defaults</p>
              <p className="text-2xl font-semibold text-[var(--color-text-primary)]">{globalPolicies}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">Apply to all tenants</p>
            </div>
          </CardContent>
        </Card>
        <Card className="rounded-none border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <CardContent className="p-4 flex items-center gap-3">
            <div className="w-10 h-10 rounded-full flex items-center justify-center bg-blue-500/10 text-blue-600">
              <Building2 className="w-5 h-5" />
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Tenant overrides</p>
              <p className="text-2xl font-semibold text-[var(--color-text-primary)]">{overridePolicies}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">Tenant-specific</p>
            </div>
          </CardContent>
        </Card>
      </div>

      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span>{error}</span>
            <Button variant="outline" size="sm" onClick={loadData} className="ml-auto">Retry</Button>
          </CardContent>
        </Card>
      )}

      {/* Table */}
      <Card>
        <CardContent className="p-4">
          <DataTableHeader
            searchValue={searchQuery}
            onSearchChange={setSearchQuery}
            searchPlaceholder="Search by use case or model..."
            filterValue={scopeFilter}
            onFilterChange={setScopeFilter}
            filterOptions={[
              { value: 'global', label: 'Global defaults' },
              { value: 'override', label: 'Tenant overrides' },
            ]}
            filterPlaceholder="Scope"
            showViewToggle={false}
            className="px-0 border-b-0"
          />

          <div className="mt-3 rounded-md border border-[var(--color-border-light)] overflow-hidden">
            <DataTable
              data={paginatedPolicies}
              columns={columns}
              getRowId={(p) => p.id}
              onRowClick={openDetail}
              loading={loading}
              loadingMessage="Loading route policies..."
              showCheckboxes={false}
              emptyIcon={<Route className="w-12 h-12" />}
              emptyTitle="No route policies found"
              emptyDescription={
                searchQuery || scopeFilter
                  ? 'Try adjusting your filters.'
                  : 'Route policies will appear here once created. Use the button above to create your first policy.'
              }
              rowActions={(policy) => <DataTableRowActions actions={getRowActions(policy)} />}
            />
          </div>

          <div className="pt-4">
            <DataTablePagination
              pageNumber={pageNumber}
              pageSize={PAGE_SIZE}
              totalCount={filteredPolicies.length}
              onPageChange={setPageNumber}
              onPageSizeChange={() => {/* fixed page size */}}
              className="px-0 border-t-0"
            />
          </div>
        </CardContent>
      </Card>

      {/* ── Detail Dialog ──────────────────────────────────────────────── */}
      <Dialog open={showDetailDialog} onOpenChange={setShowDetailDialog}>
        <DialogContent className="max-w-[500px]">
          {detailPolicy && (
            <>
              <DialogHeader>
                <DialogTitle className="font-mono">{detailPolicy.useCase}</DialogTitle>
                <DialogDescription>
                  {detailPolicy.isOverride ? 'Tenant override policy' : 'Global default policy'}
                </DialogDescription>
              </DialogHeader>
              <div className="space-y-3 py-2">
                <div className="grid grid-cols-2 gap-3">
                  <div className="rounded-lg border p-3">
                    <p className="text-xs text-[var(--color-text-tertiary)] mb-1">Model</p>
                    <p className="text-sm font-medium font-mono">{detailPolicy.primaryModelName ?? 'Not set'}</p>
                  </div>
                  <div className="rounded-lg border p-3">
                    <p className="text-xs text-[var(--color-text-tertiary)] mb-1">Risk Tier</p>
                    <Badge className={`text-xs ${riskTierColor(detailPolicy.riskTier)}`}>{detailPolicy.riskTier}</Badge>
                  </div>
                  <div className="rounded-lg border p-3">
                    <p className="text-xs text-[var(--color-text-tertiary)] mb-1">Data Sensitivity</p>
                    <p className="text-sm font-medium">{detailPolicy.dataSensitivity}</p>
                  </div>
                  <div className="rounded-lg border p-3">
                    <p className="text-xs text-[var(--color-text-tertiary)] mb-1">Cost Ceiling</p>
                    <p className="text-sm font-medium">${detailPolicy.costCeiling.toFixed(2)}</p>
                  </div>
                </div>
                <div className="flex items-center gap-2 flex-wrap">
                  {detailPolicy.isActive ? (
                    <Badge className="text-xs bg-green-500/10 text-green-700 border-green-200">Active</Badge>
                  ) : (
                    <Badge variant="secondary" className="text-xs">Inactive</Badge>
                  )}
                  {detailPolicy.isOverride ? (
                    <Badge className="text-xs bg-blue-500/10 text-blue-700 border-blue-200">
                      <Building2 className="w-3 h-3 mr-1" />Tenant override
                    </Badge>
                  ) : (
                    <Badge variant="outline" className="text-xs">
                      <Globe className="w-3 h-3 mr-1" />Global default
                    </Badge>
                  )}
                </div>
              </div>
              <DialogFooter>
                <Button variant="outline" onClick={() => setShowDetailDialog(false)}>Close</Button>
                <Button onClick={() => { setShowDetailDialog(false); openEdit(detailPolicy); }}>
                  <Pencil className="w-4 h-4 mr-2" />Edit
                </Button>
              </DialogFooter>
            </>
          )}
        </DialogContent>
      </Dialog>

      {/* ── Create/Edit Dialog ─────────────────────────────────────────── */}
      <Dialog open={showDialog} onOpenChange={setShowDialog}>
        <DialogContent className="max-w-[560px]">
          <DialogHeader>
            <DialogTitle>{editingPolicy ? `Edit: ${editingPolicy.useCase}` : 'New Route Policy'}</DialogTitle>
            <DialogDescription>
              {editingPolicy
                ? 'Update the model assignment and governance settings for this use case.'
                : 'Create a new AI model routing policy for a use case.'}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2 max-h-[60vh] overflow-y-auto pr-1">

            <div className="space-y-2">
              <label className="text-sm font-medium" htmlFor="use-case">Use Case</label>
              <Input
                id="use-case"
                value={formUseCase}
                onChange={(e) => setFormUseCase(e.target.value)}
                placeholder="e.g. personal-finance-agent"
                disabled={!!editingPolicy}
                className="font-mono"
              />
              <p className="text-xs text-[var(--color-text-tertiary)]">
                Must match the agent name or task use case exactly.
              </p>
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium" htmlFor="primary-model">Primary Model</label>
              <Select value={formPrimaryModelId || '__none__'} onValueChange={(v) => setFormPrimaryModelId(v === '__none__' ? '' : v)}>
                <SelectTrigger id="primary-model">
                  <SelectValue placeholder="Select a model..." />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__none__">No model assigned</SelectItem>
                  {Object.entries(groupedModels).map(([provider, providerModels]) => (
                    <div key={provider}>
                      <div className="px-2 py-1.5 text-[11px] font-semibold tracking-wider text-[var(--color-text-tertiary)]">
                        {provider}
                      </div>
                      {providerModels.map((m) => (
                        <SelectItem key={m.id} value={m.id}>
                          {m.modelName}
                        </SelectItem>
                      ))}
                    </div>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <label className="text-sm font-medium" htmlFor="risk-tier">Risk Tier</label>
                <Select value={formRiskTier} onValueChange={setFormRiskTier}>
                  <SelectTrigger id="risk-tier">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {riskTierOptions.map((tier) => (
                      <SelectItem key={tier} value={tier}>{tier}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium" htmlFor="data-sensitivity">Data Sensitivity</label>
                <Select value={formDataSensitivity} onValueChange={setFormDataSensitivity}>
                  <SelectTrigger id="data-sensitivity">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {dataSensitivityOptions.map((ds) => (
                      <SelectItem key={ds} value={ds}>{ds}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium" htmlFor="cost-ceiling">Cost Ceiling ($ per run)</label>
              <Input
                id="cost-ceiling"
                type="number"
                value={formCostCeiling}
                onChange={(e) => setFormCostCeiling(Number(e.target.value))}
                min={0}
                step={0.001}
              />
              <p className="text-xs text-[var(--color-text-tertiary)]">
                Set to 0 to disable cost limiting.
              </p>
            </div>

            <div className="flex items-center justify-between rounded-md border border-[var(--color-border-light)] px-4 py-3">
              <div>
                <p className="text-sm font-medium">Active</p>
                <p className="text-xs text-[var(--color-text-tertiary)]">Inactive policies are ignored at runtime.</p>
              </div>
              <Switch id="active" checked={formIsActive} onCheckedChange={setFormIsActive} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowDialog(false)}>Cancel</Button>
            <Button onClick={handleSave} disabled={saving || (!editingPolicy && !formUseCase)}>
              {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {editingPolicy ? 'Save Changes' : 'Create'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Delete Confirmation Dialog ─────────────────────────────────── */}
      <Dialog open={showDeleteDialog} onOpenChange={setShowDeleteDialog}>
        <DialogContent className="max-w-[450px]">
          <DialogHeader>
            <DialogTitle>Delete Route Policy</DialogTitle>
            <DialogDescription>
              Are you sure you want to delete the policy for <strong className="font-mono">{deleteTarget?.useCase}</strong>? This action cannot be undone.
              {deleteTarget?.isOverride && (
                <span className="block mt-1 text-blue-600">This is a tenant override — deleting it will revert to the global default.</span>
              )}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowDeleteDialog(false)}>Cancel</Button>
            <Button variant="destructive" onClick={handleDelete} disabled={deleting}>
              {deleting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Delete
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
