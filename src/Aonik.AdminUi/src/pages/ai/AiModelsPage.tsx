import { useCallback, useEffect, useRef, useState } from 'react';

import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
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
  AlertCircle,
  BrainCircuit,
  Check,
  Download,
  LoaderCircle,
  Pencil,
  Plus,
  Power,
  PowerOff,
  Trash2,
  X,
} from 'lucide-react';
import { toast } from 'sonner';

import type {
  AiCatalogModelProviderResponse,
  AiCatalogModelResponse,
  AiModelResponse,
  AiProviderResponse,
} from '@/types/ai';
import { aiModelCatalogService, aiModelService, aiProviderService } from '@/services/aiService';
import {
  DataTable,
  DataTableHeader,
  DataTableRowActions,
  type ColumnDef,
  type DataTableAction,
  type FilterOption,
} from '@/components/ui/data-table';

const formatDate = (dateString?: string | null) => {
  if (!dateString) return '';
  return new Date(dateString).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
};

const getErrorMessage = (err: unknown, fallback: string) => {
  if (err && typeof err === 'object' && 'userMessage' in err) {
    const message = String((err as { userMessage?: string }).userMessage ?? '').trim();
    if (message) {
      return message;
    }
  }

  return fallback;
};

export function AiModelsPage() {
  // ── State ──────────────────────────────────────────────────────────
  const [providers, setProviders] = useState<AiProviderResponse[]>([]);
  const [models, setModels] = useState<AiModelResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [providerFilter, setProviderFilter] = useState('');
  const requestIdRef = useRef(0);

  // Dialog state
  const [showProviderDialog, setShowProviderDialog] = useState(false);
  const [showModelDialog, setShowModelDialog] = useState(false);
  const [showImportDialog, setShowImportDialog] = useState(false);
  const [editingProvider, setEditingProvider] = useState<AiProviderResponse | null>(null);
  const [editingModel, setEditingModel] = useState<AiModelResponse | null>(null);
  const [saving, setSaving] = useState(false);
  const [catalogLoading, setCatalogLoading] = useState(false);
  const [catalogLoadingModels, setCatalogLoadingModels] = useState(false);
  const [catalogImporting, setCatalogImporting] = useState(false);
  const [catalogQuery, setCatalogQuery] = useState('');
  const [catalogModelProviders, setCatalogModelProviders] = useState<AiCatalogModelProviderResponse[]>([]);
  const [catalogModels, setCatalogModels] = useState<AiCatalogModelResponse[]>([]);
  const [selectedCatalogModelProviderKey, setSelectedCatalogModelProviderKey] = useState('');

  // Provider form fields
  const [providerName, setProviderName] = useState('');
  const [providerAuthRef, setProviderAuthRef] = useState('');
  const [providerActive, setProviderActive] = useState(true);

  // Model form fields
  const [modelName, setModelName] = useState('');
  const [modelProviderId, setModelProviderId] = useState('');
  const [modelContextWindow, setModelContextWindow] = useState('128000');
  const [modelActive, setModelActive] = useState(true);

  const resetProviderForm = () => {
    setEditingProvider(null);
    setProviderName('');
    setProviderAuthRef('');
    setProviderActive(true);
  };

  const resetModelForm = () => {
    setEditingModel(null);
    setModelName('');
    setModelProviderId(providers.length > 0 ? providers[0].id : '');
    setModelContextWindow('128000');
    setModelActive(true);
  };

  const handleProviderDialogOpenChange = (open: boolean) => {
    if (!open) {
      resetProviderForm();
      setSaving(false);
    }
    setShowProviderDialog(open);
  };

  const handleModelDialogOpenChange = (open: boolean) => {
    if (!open) {
      resetModelForm();
      setSaving(false);
    }
    setShowModelDialog(open);
  };

  const handleImportDialogOpenChange = (open: boolean) => {
    if (!open) {
      setCatalogQuery('');
      setCatalogModels([]);
      setCatalogLoading(false);
      setCatalogLoadingModels(false);
      setCatalogImporting(false);
      setSelectedCatalogModelProviderKey('');
    }

    setShowImportDialog(open);
  };

  // ── Data loading ───────────────────────────────────────────────────

  const loadData = useCallback(async () => {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setLoading(true);
    setError(null);

    try {
      const [providerList, modelList] = await Promise.all([
        aiProviderService.list(),
        aiModelService.list(providerFilter || undefined),
      ]);

      if (requestIdRef.current !== requestId) return;

      setProviders(providerList);
      setModels(modelList);
      setLoading(false);
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) return;
      console.error('Failed to load AI models:', err);
      setError(getErrorMessage(err, 'Failed to load AI models. Please try again.'));
      setLoading(false);
    }
  }, [providerFilter]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // ── Filtering ──────────────────────────────────────────────────────

  const filteredModels = models.filter((m) => {
    if (!searchQuery) return true;
    const q = searchQuery.toLowerCase();
    return (
      m.modelName.toLowerCase().includes(q) ||
      (m.providerName ?? '').toLowerCase().includes(q)
    );
  });

  const filteredCatalogModelProviders = catalogModelProviders.filter((modelProvider) => {
    if (!catalogQuery) return true;

    const query = catalogQuery.toLowerCase();
    return (
      modelProvider.name.toLowerCase().includes(query)
      || modelProvider.modelProviderKey.toLowerCase().includes(query)
      || (modelProvider.sdkPackage ?? '').toLowerCase().includes(query)
    );
  });

  const selectedCatalogModelProvider = catalogModelProviders.find(
    (modelProvider) => modelProvider.modelProviderKey === selectedCatalogModelProviderKey,
  ) ?? null;

  const loadCatalogModelProviders = useCallback(async () => {
    setCatalogLoading(true);

    try {
      const modelProviders = await aiModelCatalogService.listModelProviders();
      setCatalogModelProviders(modelProviders);
      setSelectedCatalogModelProviderKey((currentValue) => {
        if (currentValue && modelProviders.some((modelProvider) => modelProvider.modelProviderKey === currentValue)) {
          return currentValue;
        }

        return modelProviders[0]?.modelProviderKey ?? '';
      });
    } catch (err: unknown) {
      const message = getErrorMessage(err, 'Failed to load model catalog providers.');
      setError(message);
      toast.error(message);
    } finally {
      setCatalogLoading(false);
    }
  }, []);

  const loadCatalogModels = useCallback(async (modelProviderKey: string) => {
    if (!modelProviderKey) {
      setCatalogModels([]);
      return;
    }

    setCatalogLoadingModels(true);

    try {
      const modelList = await aiModelCatalogService.listModels(modelProviderKey);
      setCatalogModels(modelList);
    } catch (err: unknown) {
      const message = getErrorMessage(err, 'Failed to load model catalog models.');
      setError(message);
      toast.error(message);
      setCatalogModels([]);
    } finally {
      setCatalogLoadingModels(false);
    }
  }, []);

  useEffect(() => {
    if (!showImportDialog) {
      return;
    }

    void loadCatalogModels(selectedCatalogModelProviderKey);
  }, [loadCatalogModels, selectedCatalogModelProviderKey, showImportDialog]);

  // ── Provider dialog ────────────────────────────────────────────────

  const openNewProvider = () => {
    resetProviderForm();
    setShowProviderDialog(true);
  };

  const openEditProvider = (provider: AiProviderResponse) => {
    setEditingProvider(provider);
    setProviderName(provider.name);
    setProviderAuthRef(provider.authConfigRef ?? '');
    setProviderActive(provider.isActive);
    setShowProviderDialog(true);
  };

  const saveProvider = async () => {
    setSaving(true);
    try {
      if (editingProvider) {
        await aiProviderService.update(editingProvider.id, {
          name: providerName,
          authConfigRef: providerAuthRef || null,
          isActive: providerActive,
        });
      } else {
        await aiProviderService.create({
          name: providerName,
          authConfigRef: providerAuthRef || null,
          isActive: providerActive,
        });
      }
      setShowProviderDialog(false);
      await loadData();
    } catch (err: unknown) {
      console.error('Failed to save provider:', err);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to save provider.');
    } finally {
      setSaving(false);
    }
  };

  const deleteProvider = async (provider: AiProviderResponse) => {
    if (!confirm(`Delete provider "${provider.name}" and all its models?`)) return;
    try {
      await aiProviderService.delete(provider.id);
      await loadData();
    } catch (err: unknown) {
      console.error('Failed to delete provider:', err);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to delete provider.');
    }
  };

  // ── Model dialog ───────────────────────────────────────────────────

  const openNewModel = () => {
    if (providers.length === 0) {
      setError('Create a provider first, then add a model.');
      return;
    }

    resetModelForm();
    setShowModelDialog(true);
  };

  const openEditModel = (model: AiModelResponse) => {
    setEditingModel(model);
    setModelName(model.modelName);
    setModelProviderId(model.aiProviderId);
    setModelContextWindow(String(model.contextWindow));
    setModelActive(model.isActive);
    setShowModelDialog(true);
  };

  const saveModel = async () => {
    setSaving(true);
    try {
      if (editingModel) {
        await aiModelService.update(editingModel.id, {
          modelName: modelName,
          contextWindow: parseInt(modelContextWindow, 10) || 0,
          isActive: modelActive,
        });
      } else {
        await aiModelService.create({
          aiProviderId: modelProviderId,
          modelName: modelName,
          contextWindow: parseInt(modelContextWindow, 10) || 128000,
          isActive: modelActive,
        });
      }
      setShowModelDialog(false);
      await loadData();
    } catch (err: unknown) {
      console.error('Failed to save model:', err);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to save model.');
    } finally {
      setSaving(false);
    }
  };

  const deleteModel = async (model: AiModelResponse) => {
    if (!confirm(`Delete model "${model.modelName}"?`)) return;
    try {
      await aiModelService.delete(model.id);
      await loadData();
    } catch (err: unknown) {
      console.error('Failed to delete model:', err);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to delete model.');
    }
  };

  const openImportDialog = async () => {
    handleImportDialogOpenChange(true);
    await loadCatalogModelProviders();
  };

  const importModelProvider = async () => {
    if (!selectedCatalogModelProvider) {
      return;
    }

    setCatalogImporting(true);

    try {
      const result = await aiModelCatalogService.importModelProvider(selectedCatalogModelProvider.modelProviderKey, {
        importModelsAsInactive: true,
      });

      toast.success(
        `${result.providerName} imported: ${result.modelsCreated} new, ${result.modelsLinked} linked, ${result.modelsSkipped} skipped.`,
      );

      handleImportDialogOpenChange(false);
      await loadData();
    } catch (err: unknown) {
      const message = getErrorMessage(err, 'Failed to import model provider.');
      setError(message);
      toast.error(message);
    } finally {
      setCatalogImporting(false);
    }
  };

  // ── Render ─────────────────────────────────────────────────────────

  const breadcrumbItems = [
    { label: 'AI & Agents', href: '/ai' },
    { label: 'Models', icon: <BrainCircuit className="w-3.5 h-3.5" /> },
  ];

  const totalProviders = providers.length;
  const activeProviders = providers.filter((provider) => provider.isActive).length;
  const totalModels = models.length;
  const activeModels = models.filter((model) => model.isActive).length;

  const providerFilterOptions: FilterOption[] = providers.map((provider) => ({
    value: provider.id,
    label: provider.name,
  }));

  const getProviderActions = (provider: AiProviderResponse): DataTableAction[] => [
    {
      label: 'Edit provider',
      icon: <Pencil className="w-4 h-4" />,
      onClick: () => openEditProvider(provider),
    },
    {
      label: 'Delete provider',
      icon: <Trash2 className="w-4 h-4" />,
      onClick: () => deleteProvider(provider),
      variant: 'danger',
    },
  ];

  const getModelActions = (model: AiModelResponse): DataTableAction[] => [
    {
      label: 'Edit model',
      icon: <Pencil className="w-4 h-4" />,
      onClick: () => openEditModel(model),
    },
    {
      label: 'Delete model',
      icon: <Trash2 className="w-4 h-4" />,
      onClick: () => deleteModel(model),
      variant: 'danger',
    },
  ];

  const modelColumns: ColumnDef<AiModelResponse>[] = [
    {
      id: 'model',
      header: 'Model',
      accessorFn: (row) => row.modelName,
      sortable: true,
      headerClassName: 'pl-4',
      className: 'pl-4',
      cell: (model) => (
        <div>
          <p className="font-medium text-[var(--color-text-primary)]">{model.modelName}</p>
          <p className="text-xs text-[var(--color-text-tertiary)]">{model.providerName ?? 'No provider assigned'}</p>
        </div>
      ),
    },
    {
      id: 'provider',
      header: 'Provider',
      accessorFn: (row) => row.providerName ?? '',
      sortable: true,
      cell: (model) => (
        <span className="text-sm text-[var(--color-text-secondary)]">{model.providerName ?? '—'}</span>
      ),
    },
    {
      id: 'contextWindow',
      header: 'Context',
      accessorFn: (row) => row.contextWindow,
      sortable: true,
      headerClassName: 'justify-end text-right',
      className: 'text-right',
      cell: (model) => (
        <span className="text-sm text-[var(--color-text-secondary)]">
          {model.contextWindow > 0 ? `${(model.contextWindow / 1000).toFixed(0)}k` : '—'}
        </span>
      ),
    },
    {
      id: 'status',
      header: 'Status',
      accessorFn: (row) => row.isActive,
      sortable: true,
      cell: (model) => (
        model.isActive ? (
          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium bg-[var(--color-success-light)] text-[var(--color-success)]">
            <Check className="w-3 h-3" /> Active
          </span>
        ) : (
          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium bg-[var(--color-surface-inset)] text-[var(--color-text-tertiary)]">
            <X className="w-3 h-3" /> Inactive
          </span>
        )
      ),
    },
    {
      id: 'createdAt',
      header: 'Created',
      accessorFn: (row) => row.createdAt ? new Date(row.createdAt) : null,
      sortable: true,
      cell: (model) => (
        <span className="text-sm text-[var(--color-text-secondary)]">{formatDate(model.createdAt)}</span>
      ),
    },
  ];

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">AI Models</h1>
          <p className="text-[var(--color-text-secondary)]">
            Manage AI providers and their models used across the platform.
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={openImportDialog} className="rounded-sm">
            <Download className="w-4 h-4 mr-2" />
            Import model provider
          </Button>
          <Button variant="outline" onClick={openNewProvider} className="rounded-sm">
            <Plus className="w-4 h-4 mr-2" />
            New provider
          </Button>
          <Button onClick={openNewModel} className="rounded-sm" disabled={providers.length === 0}>
            <Plus className="w-4 h-4 mr-2" />
            New model
          </Button>
        </div>
      </div>

      <div className="grid gap-4 mb-6 md:grid-cols-2 xl:grid-cols-4">
        <Card className="rounded-none border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <CardContent className="p-4 flex items-center gap-3">
            <div className="w-10 h-10 rounded-full flex items-center justify-center bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]">
              <BrainCircuit className="w-5 h-5" />
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Model providers</p>
              <p className="text-2xl font-semibold text-[var(--color-text-primary)]">{totalProviders}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">Configured in this tenant</p>
            </div>
          </CardContent>
        </Card>
        <Card className="rounded-none border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <CardContent className="p-4 flex items-center gap-3">
            <div className="w-10 h-10 rounded-full flex items-center justify-center bg-[var(--color-success-light)] text-[var(--color-success)]">
              <Power className="w-5 h-5" />
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Active providers</p>
              <p className="text-2xl font-semibold text-[var(--color-text-primary)]">{activeProviders}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">Available for routing policies</p>
            </div>
          </CardContent>
        </Card>
        <Card className="rounded-none border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <CardContent className="p-4 flex items-center gap-3">
            <div className="w-10 h-10 rounded-full flex items-center justify-center bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]">
              <Download className="w-5 h-5" />
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Catalog models</p>
              <p className="text-2xl font-semibold text-[var(--color-text-primary)]">{totalModels}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">Local model records</p>
            </div>
          </CardContent>
        </Card>
        <Card className="rounded-none border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <CardContent className="p-4 flex items-center gap-3">
            <div className="w-10 h-10 rounded-full flex items-center justify-center bg-[var(--color-success-light)] text-[var(--color-success)]">
              <Check className="w-5 h-5" />
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Active models</p>
              <p className="text-2xl font-semibold text-[var(--color-text-primary)]">{activeModels}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">Enabled for runtime use</p>
            </div>
          </CardContent>
        </Card>
      </div>

      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span>{error}</span>
            <Button variant="outline" size="sm" onClick={loadData} className="ml-auto">
              Retry
            </Button>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="p-4">
          <div className="flex items-start justify-between gap-4 pb-4">
            <div>
              <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Model Providers</h2>
              <p className="text-sm text-[var(--color-text-secondary)]">
                Configure provider access first, then review and curate the models they expose.
              </p>
            </div>
            <Badge variant="outline" className="text-xs">
              {totalProviders} configured
            </Badge>
          </div>

          {loading && providers.length === 0 ? (
            <div className="rounded-md border border-[var(--color-border-light)] px-4 py-10 text-center text-sm text-[var(--color-text-tertiary)]">
              Loading providers...
            </div>
          ) : providers.length === 0 ? (
            <div className="rounded-md border border-[var(--color-border-light)] px-4 py-10 text-center">
              <BrainCircuit className="w-12 h-12 mx-auto mb-3 text-[var(--color-text-tertiary)]" />
              <p className="text-sm font-medium text-[var(--color-text-primary)]">No model providers configured</p>
              <p className="text-xs text-[var(--color-text-tertiary)] mt-1">
                Import a model provider from the external catalog or add one manually.
              </p>
            </div>
          ) : (
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
              {providers.map((provider) => (
                <div
                  key={provider.id}
                  className="rounded-none border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="space-y-2">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-medium text-[var(--color-text-primary)]">{provider.name}</span>
                      {provider.isActive ? (
                          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium bg-[var(--color-success-light)] text-[var(--color-success)]">
                          <Power className="w-3 h-3" /> Active
                        </span>
                      ) : (
                          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium bg-[var(--color-surface-inset)] text-[var(--color-text-tertiary)]">
                          <PowerOff className="w-3 h-3" /> Inactive
                        </span>
                      )}
                      </div>
                      <p className="text-xs text-[var(--color-text-tertiary)]">
                        {provider.models.length} model{provider.models.length !== 1 ? 's' : ''}
                        {provider.authConfigRef ? ` | Auth: ${provider.authConfigRef}` : ''}
                      </p>
                    </div>
                    <DataTableRowActions actions={getProviderActions(provider)} />
                  </div>
                </div>
              ))}
            </div>
          )}

          <div className="mt-6 pt-6 border-t border-[var(--color-border-light)]">
            <DataTableHeader
              searchValue={searchQuery}
              onSearchChange={setSearchQuery}
              searchPlaceholder="Search models"
              filterValue={providerFilter}
              onFilterChange={setProviderFilter}
              filterOptions={providerFilterOptions}
              filterPlaceholder="Provider"
              showViewToggle={false}
              actions={
                <Badge variant="outline" className="text-xs">
                  {filteredModels.length} shown
                </Badge>
              }
              className="px-0 border-b-0"
            />

            <div className="mt-3 rounded-md border border-[var(--color-border-light)] overflow-hidden">
              <DataTable
                data={filteredModels}
                columns={modelColumns}
                getRowId={(model) => model.id}
                showCheckboxes={false}
                loading={loading}
                loadingMessage="Loading models..."
                emptyIcon={<BrainCircuit className="w-12 h-12" />}
                emptyTitle="No models found"
                emptyDescription={
                  searchQuery || providerFilter
                    ? 'Try adjusting your filters.'
                    : 'Create a model or import a model provider to get started.'
                }
                rowActions={(model) => <DataTableRowActions actions={getModelActions(model)} />}
              />
            </div>
          </div>
        </CardContent>
      </Card>

      {/* ── External model catalog dialog ─────────────────────────── */}
      <Dialog open={showImportDialog} onOpenChange={handleImportDialogOpenChange}>
        <DialogContent className="sm:max-w-[960px]">
          <DialogHeader>
            <DialogTitle>Import Model Provider</DialogTitle>
            <DialogDescription>
              Browse the configured model catalog source, select a model provider, and import all of its models.
              Imported models start inactive so your team can review and enable them deliberately.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-2 md:grid-cols-[280px_minmax(0,1fr)]">
            <div className="space-y-3">
              <div className="space-y-2">
                <Label htmlFor="catalog-model-provider-search">Model providers</Label>
                <Input
                  id="catalog-model-provider-search"
                  value={catalogQuery}
                  onChange={(event) => setCatalogQuery(event.target.value)}
                  placeholder="Search model providers..."
                />
              </div>

              <div className="max-h-[420px] overflow-y-auto rounded-md border border-[var(--color-border-light)]">
                {catalogLoading ? (
                  <div className="flex items-center justify-center gap-2 px-4 py-10 text-sm text-[var(--color-text-tertiary)]">
                    <LoaderCircle className="h-4 w-4 animate-spin" />
                    Loading model providers...
                  </div>
                ) : filteredCatalogModelProviders.length === 0 ? (
                  <div className="px-4 py-10 text-center text-sm text-[var(--color-text-tertiary)]">
                    No model providers match your search.
                  </div>
                ) : (
                  <div className="divide-y divide-[var(--color-border-light)]">
                    {filteredCatalogModelProviders.map((modelProvider) => {
                      const isSelected = modelProvider.modelProviderKey === selectedCatalogModelProviderKey;

                      return (
                        <button
                          key={modelProvider.modelProviderKey}
                          type="button"
                          onClick={() => setSelectedCatalogModelProviderKey(modelProvider.modelProviderKey)}
                          className={`flex w-full flex-col gap-1 px-4 py-3 text-left transition-colors ${
                            isSelected
                              ? 'bg-[var(--color-surface-inset)]'
                              : 'hover:bg-[var(--color-surface-inset)]/60'
                          }`}
                        >
                          <span className="font-medium text-[var(--color-text-primary)]">{modelProvider.name}</span>
                          <span className="text-xs text-[var(--color-text-secondary)]">{modelProvider.modelProviderKey}</span>
                          <span className="text-xs text-[var(--color-text-tertiary)]">
                            {modelProvider.modelCount} model{modelProvider.modelCount !== 1 ? 's' : ''}
                          </span>
                        </button>
                      );
                    })}
                  </div>
                )}
              </div>
            </div>

            <div className="min-h-[420px] rounded-md border border-[var(--color-border-light)] p-4">
              {!selectedCatalogModelProvider ? (
                <div className="flex h-full items-center justify-center text-sm text-[var(--color-text-tertiary)]">
                  Select a model provider to preview its metadata and models.
                </div>
              ) : (
                <div className="space-y-4">
                  <div className="space-y-2">
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <h3 className="text-lg font-semibold text-[var(--color-text-primary)]">
                          {selectedCatalogModelProvider.name}
                        </h3>
                        <p className="text-sm text-[var(--color-text-secondary)]">
                          {selectedCatalogModelProvider.modelProviderKey}
                        </p>
                      </div>
                      <span className="inline-flex items-center rounded-full bg-[var(--color-surface-inset)] px-2 py-1 text-xs font-medium text-[var(--color-text-secondary)]">
                        {selectedCatalogModelProvider.modelCount} model{selectedCatalogModelProvider.modelCount !== 1 ? 's' : ''}
                      </span>
                    </div>

                    {(selectedCatalogModelProvider.documentationUrl || selectedCatalogModelProvider.sdkPackage) && (
                      <div className="space-y-1 text-sm text-[var(--color-text-secondary)]">
                        {selectedCatalogModelProvider.sdkPackage && (
                          <p>SDK package: {selectedCatalogModelProvider.sdkPackage}</p>
                        )}
                        {selectedCatalogModelProvider.documentationUrl && (
                          <p className="truncate">Docs: {selectedCatalogModelProvider.documentationUrl}</p>
                        )}
                        {selectedCatalogModelProvider.apiBaseUrl && (
                          <p className="truncate">API base: {selectedCatalogModelProvider.apiBaseUrl}</p>
                        )}
                      </div>
                    )}

                    {selectedCatalogModelProvider.environmentVariables.length > 0 && (
                      <div>
                        <p className="mb-2 text-xs font-medium uppercase tracking-wide text-[var(--color-text-tertiary)]">
                          Environment variables
                        </p>
                        <div className="flex flex-wrap gap-2">
                          {selectedCatalogModelProvider.environmentVariables.map((environmentVariable) => (
                            <span
                              key={environmentVariable}
                              className="rounded-full bg-[var(--color-surface-inset)] px-2 py-1 text-xs text-[var(--color-text-secondary)]"
                            >
                              {environmentVariable}
                            </span>
                          ))}
                        </div>
                      </div>
                    )}
                  </div>

                  <div className="space-y-2">
                    <div className="flex items-center justify-between gap-3">
                      <p className="text-sm font-medium text-[var(--color-text-primary)]">Models to import</p>
                      <span className="text-xs text-[var(--color-text-tertiary)]">
                        Imported models will be inactive by default
                      </span>
                    </div>

                    {catalogLoadingModels ? (
                      <div className="flex items-center gap-2 rounded-md border border-[var(--color-border-light)] px-4 py-8 text-sm text-[var(--color-text-tertiary)]">
                        <LoaderCircle className="h-4 w-4 animate-spin" />
                        Loading models...
                      </div>
                    ) : catalogModels.length === 0 ? (
                      <div className="rounded-md border border-[var(--color-border-light)] px-4 py-8 text-sm text-[var(--color-text-tertiary)]">
                        No models were returned for this model provider.
                      </div>
                    ) : (
                      <div className="max-h-[240px] overflow-y-auto rounded-md border border-[var(--color-border-light)]">
                        <table className="w-full text-sm">
                          <thead>
                            <tr className="border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)]">
                              <th className="p-3 text-left font-medium text-[var(--color-text-secondary)]">Model</th>
                              <th className="p-3 text-left font-medium text-[var(--color-text-secondary)]">Family</th>
                              <th className="p-3 text-right font-medium text-[var(--color-text-secondary)]">Context</th>
                              <th className="p-3 text-right font-medium text-[var(--color-text-secondary)]">Output</th>
                            </tr>
                          </thead>
                          <tbody>
                            {catalogModels.map((catalogModel) => (
                              <tr
                                key={catalogModel.modelKey}
                                className="border-b border-[var(--color-border-light)] last:border-b-0"
                              >
                                <td className="p-3 align-top">
                                  <p className="font-medium text-[var(--color-text-primary)]">{catalogModel.name}</p>
                                  <p className="text-xs text-[var(--color-text-tertiary)]">{catalogModel.modelKey}</p>
                                </td>
                                <td className="p-3 text-[var(--color-text-secondary)]">{catalogModel.family ?? '—'}</td>
                                <td className="p-3 text-right text-[var(--color-text-secondary)]">
                                  {catalogModel.contextWindow > 0 ? `${(catalogModel.contextWindow / 1000).toFixed(0)}k` : '—'}
                                </td>
                                <td className="p-3 text-right text-[var(--color-text-secondary)]">
                                  {catalogModel.outputTokenLimit > 0 ? `${(catalogModel.outputTokenLimit / 1000).toFixed(0)}k` : '—'}
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}
                  </div>
                </div>
              )}
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => handleImportDialogOpenChange(false)} disabled={catalogImporting}>
              Cancel
            </Button>
            <Button
              onClick={importModelProvider}
              disabled={!selectedCatalogModelProvider || catalogImporting || catalogLoadingModels}
            >
              {catalogImporting ? 'Importing...' : 'Import model provider'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Provider dialog ──────────────────────────────────────── */}
      <Dialog open={showProviderDialog} onOpenChange={handleProviderDialogOpenChange}>
        <DialogContent className="sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>{editingProvider ? 'Edit Provider' : 'New Provider'}</DialogTitle>
            <DialogDescription>
              {editingProvider
                ? 'Update the provider configuration.'
                : 'Add a new AI provider (e.g. OpenAI, Anthropic, Azure OpenAI).'}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2">
            <div className="grid gap-2">
              <Label htmlFor="provider-name">Name</Label>
              <Input
                id="provider-name"
                value={providerName}
                onChange={(e) => setProviderName(e.target.value)}
                placeholder="e.g. OpenAI"
              />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="provider-auth">Auth Config Ref</Label>
              <Input
                id="provider-auth"
                value={providerAuthRef}
                onChange={(e) => setProviderAuthRef(e.target.value)}
                placeholder="e.g. vault://openai-key"
              />
            </div>
            <div className="flex items-center gap-2">
              <input
                type="checkbox"
                id="provider-active"
                checked={providerActive}
                onChange={(e) => setProviderActive(e.target.checked)}
                className="rounded"
              />
              <Label htmlFor="provider-active">Active</Label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => handleProviderDialogOpenChange(false)}>Cancel</Button>
            <Button onClick={saveProvider} disabled={saving || !providerName.trim()}>
              {saving ? 'Saving...' : editingProvider ? 'Update' : 'Create'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Model dialog ─────────────────────────────────────────── */}
      <Dialog open={showModelDialog} onOpenChange={handleModelDialogOpenChange}>
        <DialogContent className="sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>{editingModel ? 'Edit Model' : 'New Model'}</DialogTitle>
            <DialogDescription>
              {editingModel
                ? 'Update the model configuration.'
                : 'Register a new AI model under a provider.'}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2">
            {!editingModel && (
              <div className="grid gap-2">
                <Label htmlFor="model-provider">Provider</Label>
                <Select
                  value={modelProviderId || '__unset__'}
                  onValueChange={(value) => setModelProviderId(value === '__unset__' ? '' : value)}
                >
                  <SelectTrigger id="model-provider">
                    <SelectValue placeholder="Select provider" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__unset__" disabled>
                      Select provider
                    </SelectItem>
                    {providers.map((p) => (
                      <SelectItem key={p.id} value={p.id}>{p.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            <div className="grid gap-2">
              <Label htmlFor="model-name">Model Name</Label>
              <Input
                id="model-name"
                value={modelName}
                onChange={(e) => setModelName(e.target.value)}
                placeholder="e.g. gpt-5-mini"
              />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="model-context">Context Window</Label>
              <Input
                id="model-context"
                type="number"
                value={modelContextWindow}
                onChange={(e) => setModelContextWindow(e.target.value)}
                placeholder="128000"
              />
            </div>
            <div className="flex items-center gap-2">
              <input
                type="checkbox"
                id="model-active"
                checked={modelActive}
                onChange={(e) => setModelActive(e.target.checked)}
                className="rounded"
              />
              <Label htmlFor="model-active">Active</Label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => handleModelDialogOpenChange(false)}>Cancel</Button>
            <Button onClick={saveModel} disabled={saving || !modelName.trim() || (!editingModel && !modelProviderId)}>
              {saving ? 'Saving...' : editingModel ? 'Update' : 'Create'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
