import { useCallback, useEffect, useRef, useState } from 'react';

import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
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
  Pencil,
  Plus,
  Power,
  PowerOff,
  Trash2,
  X,
} from 'lucide-react';

import type { AiModelResponse, AiProviderResponse } from '@/types/ai';
import { aiModelService, aiProviderService } from '@/services/aiService';

const formatDate = (dateString?: string | null) => {
  if (!dateString) return '';
  return new Date(dateString).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
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
  const [editingProvider, setEditingProvider] = useState<AiProviderResponse | null>(null);
  const [editingModel, setEditingModel] = useState<AiModelResponse | null>(null);
  const [saving, setSaving] = useState(false);

  // Provider form fields
  const [providerName, setProviderName] = useState('');
  const [providerAuthRef, setProviderAuthRef] = useState('');
  const [providerActive, setProviderActive] = useState(true);

  // Model form fields
  const [modelName, setModelName] = useState('');
  const [modelProviderId, setModelProviderId] = useState('');
  const [modelContextWindow, setModelContextWindow] = useState('128000');
  const [modelActive, setModelActive] = useState(true);

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
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load AI models. Please try again.');
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

  // ── Provider dialog ────────────────────────────────────────────────

  const openNewProvider = () => {
    setEditingProvider(null);
    setProviderName('');
    setProviderAuthRef('');
    setProviderActive(true);
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
    setEditingModel(null);
    setModelName('');
    setModelProviderId(providers.length > 0 ? providers[0].id : '');
    setModelContextWindow('128000');
    setModelActive(true);
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

  // ── Render ─────────────────────────────────────────────────────────

  const breadcrumbItems = [
    { label: 'AI & Agents', href: '/ai' },
    { label: 'Models', icon: <BrainCircuit className="w-3.5 h-3.5" /> },
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
          <Button variant="outline" onClick={openNewProvider} className="rounded-sm">
            <Plus className="w-4 h-4 mr-2" />
            New provider
          </Button>
          <Button onClick={openNewModel} className="rounded-sm">
            <Plus className="w-4 h-4 mr-2" />
            New model
          </Button>
        </div>
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

      {/* ── Providers section ─────────────────────────────────────── */}
      <Card className="mb-6">
        <CardContent className="p-4">
          <h2 className="text-lg font-semibold text-[var(--color-text-primary)] mb-3">Providers</h2>
          {loading && providers.length === 0 ? (
            <p className="text-sm text-[var(--color-text-tertiary)]">Loading providers...</p>
          ) : providers.length === 0 ? (
            <p className="text-sm text-[var(--color-text-tertiary)]">No providers configured yet.</p>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
              {providers.map((provider) => (
                <div
                  key={provider.id}
                  className="border border-[var(--color-border-light)] rounded-md p-4 flex flex-col gap-2"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <span className="font-medium text-[var(--color-text-primary)]">{provider.name}</span>
                      {provider.isActive ? (
                        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-[var(--color-success-light)] text-[var(--color-success)]">
                          <Power className="w-3 h-3" /> Active
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-[var(--color-surface-inset)] text-[var(--color-text-tertiary)]">
                          <PowerOff className="w-3 h-3" /> Inactive
                        </span>
                      )}
                    </div>
                    <div className="flex gap-1">
                      <Button variant="ghost" size="sm" onClick={() => openEditProvider(provider)}>
                        <Pencil className="w-3.5 h-3.5" />
                      </Button>
                      <Button variant="ghost" size="sm" onClick={() => deleteProvider(provider)}>
                        <Trash2 className="w-3.5 h-3.5 text-[var(--color-error)]" />
                      </Button>
                    </div>
                  </div>
                  <p className="text-xs text-[var(--color-text-tertiary)]">
                    {provider.models.length} model{provider.models.length !== 1 ? 's' : ''}
                    {provider.authConfigRef ? ` | Auth: ${provider.authConfigRef}` : ''}
                  </p>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {/* ── Models section ────────────────────────────────────────── */}
      <Card>
        <CardContent className="p-4">
          <div className="flex items-center justify-between mb-3">
            <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Models</h2>
            <div className="flex items-center gap-2">
              <Input
                placeholder="Search models..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="h-9 w-[200px] rounded-sm"
              />
              <Select
                value={providerFilter || undefined}
                onValueChange={(v) => setProviderFilter(v === '__all__' ? '' : v)}
              >
                <SelectTrigger aria-label="Filter by provider" className="h-9 w-[180px] rounded-sm">
                  <SelectValue placeholder="All providers" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">All providers</SelectItem>
                  {providers.map((p) => (
                    <SelectItem key={p.id} value={p.id}>{p.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          {loading && models.length === 0 ? (
            <p className="text-sm text-[var(--color-text-tertiary)] py-8 text-center">Loading models...</p>
          ) : filteredModels.length === 0 ? (
            <div className="py-12 text-center">
              <BrainCircuit className="w-12 h-12 mx-auto mb-3 text-[var(--color-text-tertiary)]" />
              <p className="text-sm font-medium text-[var(--color-text-primary)]">No models found</p>
              <p className="text-xs text-[var(--color-text-tertiary)] mt-1">
                {searchQuery || providerFilter ? 'Try adjusting your filters.' : 'Add a model to get started.'}
              </p>
            </div>
          ) : (
            <div className="rounded-md border border-[var(--color-border-light)] overflow-hidden">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)]">
                    <th className="text-left p-3 font-medium text-[var(--color-text-secondary)]">Model</th>
                    <th className="text-left p-3 font-medium text-[var(--color-text-secondary)]">Provider</th>
                    <th className="text-right p-3 font-medium text-[var(--color-text-secondary)]">Context</th>
                    <th className="text-center p-3 font-medium text-[var(--color-text-secondary)]">Status</th>
                    <th className="text-left p-3 font-medium text-[var(--color-text-secondary)]">Created</th>
                    <th className="text-right p-3 font-medium text-[var(--color-text-secondary)]">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredModels.map((model) => (
                    <tr key={model.id} className="border-b border-[var(--color-border-light)] last:border-b-0 hover:bg-[var(--color-surface-inset)]/50">
                      <td className="p-3">
                        <p className="font-medium text-[var(--color-text-primary)]">{model.modelName}</p>
                      </td>
                      <td className="p-3 text-[var(--color-text-secondary)]">{model.providerName ?? '—'}</td>
                      <td className="p-3 text-right text-[var(--color-text-secondary)]">
                        {model.contextWindow > 0 ? `${(model.contextWindow / 1000).toFixed(0)}k` : '—'}
                      </td>
                      <td className="p-3 text-center">
                        {model.isActive ? (
                          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-[var(--color-success-light)] text-[var(--color-success)]">
                            <Check className="w-3 h-3" /> Active
                          </span>
                        ) : (
                          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-[var(--color-surface-inset)] text-[var(--color-text-tertiary)]">
                            <X className="w-3 h-3" /> Inactive
                          </span>
                        )}
                      </td>
                      <td className="p-3 text-[var(--color-text-secondary)]">{formatDate(model.createdAt)}</td>
                      <td className="p-3 text-right">
                        <div className="flex justify-end gap-1">
                          <Button variant="ghost" size="sm" onClick={() => openEditModel(model)}>
                            <Pencil className="w-3.5 h-3.5" />
                          </Button>
                          <Button variant="ghost" size="sm" onClick={() => deleteModel(model)}>
                            <Trash2 className="w-3.5 h-3.5 text-[var(--color-error)]" />
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      {/* ── Provider dialog ──────────────────────────────────────── */}
      <Dialog open={showProviderDialog} onOpenChange={setShowProviderDialog}>
        <DialogContent>
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
            <Button variant="outline" onClick={() => setShowProviderDialog(false)}>Cancel</Button>
            <Button onClick={saveProvider} disabled={saving || !providerName.trim()}>
              {saving ? 'Saving...' : editingProvider ? 'Update' : 'Create'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Model dialog ─────────────────────────────────────────── */}
      <Dialog open={showModelDialog} onOpenChange={setShowModelDialog}>
        <DialogContent>
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
                <Select value={modelProviderId} onValueChange={setModelProviderId}>
                  <SelectTrigger id="model-provider">
                    <SelectValue placeholder="Select provider" />
                  </SelectTrigger>
                  <SelectContent>
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
            <Button variant="outline" onClick={() => setShowModelDialog(false)}>Cancel</Button>
            <Button onClick={saveModel} disabled={saving || !modelName.trim() || (!editingModel && !modelProviderId)}>
              {saving ? 'Saving...' : editingModel ? 'Update' : 'Create'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
