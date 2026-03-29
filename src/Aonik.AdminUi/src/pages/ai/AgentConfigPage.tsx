import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
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
  Bot,
  Check,
  Pencil,
  RotateCcw,
  Shield,
  X,
} from 'lucide-react';

import type { AgentConfigurationResponse, AiModelResponse } from '@/types/ai';
import { agentConfigService, aiModelService } from '@/services/aiService';

const riskTierStyles: Record<string, { text: string; bg: string }> = {
  low: { text: 'text-[var(--color-success)]', bg: 'bg-[var(--color-success-light)]' },
  medium: { text: 'text-[var(--color-warning)]', bg: 'bg-[var(--color-warning-light)]' },
  high: { text: 'text-[var(--color-error)]', bg: 'bg-[var(--color-error-light)]' },
};

const formatDate = (dateString?: string | null) => {
  if (!dateString) return '';
  return new Date(dateString).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
};

export function AgentConfigPage() {
  const navigate = useNavigate();

  // ── State ──────────────────────────────────────────────────────────
  const [configs, setConfigs] = useState<AgentConfigurationResponse[]>([]);
  const [models, setModels] = useState<AiModelResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const requestIdRef = useRef(0);

  // Edit dialog state
  const [showEditDialog, setShowEditDialog] = useState(false);
  const [editingAgent, setEditingAgent] = useState<AgentConfigurationResponse | null>(null);
  const [saving, setSaving] = useState(false);

  // Edit form fields
  const [editDescription, setEditDescription] = useState('');
  const [editInstructions, setEditInstructions] = useState('');
  const [editRiskTier, setEditRiskTier] = useState('low');
  const [editIsActive, setEditIsActive] = useState(true);
  const [editModelId, setEditModelId] = useState('');

  // ── Data loading ───────────────────────────────────────────────────

  const loadData = useCallback(async () => {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setLoading(true);
    setError(null);

    try {
      const [configList, modelList] = await Promise.all([
        agentConfigService.list(),
        aiModelService.list(),
      ]);

      if (requestIdRef.current !== requestId) return;

      setConfigs(configList);
      setModels(modelList);
      setLoading(false);
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) return;
      console.error('Failed to load agent configurations:', err);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load agent configurations. Please try again.');
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // ── Filtering ──────────────────────────────────────────────────────

  const filteredConfigs = configs.filter((c) => {
    if (!searchQuery) return true;
    const q = searchQuery.toLowerCase();
    return (
      c.name.toLowerCase().includes(q) ||
      c.domain.toLowerCase().includes(q) ||
      c.description.toLowerCase().includes(q)
    );
  });

  // Group by agent name — show only the most relevant row per agent
  // (tenant override if present, otherwise global)
  const uniqueAgents = new Map<string, AgentConfigurationResponse>();
  for (const config of filteredConfigs) {
    const existing = uniqueAgents.get(config.name);
    if (!existing || config.isOverride) {
      uniqueAgents.set(config.name, config);
    }
  }
  const displayConfigs = Array.from(uniqueAgents.values());

  // ── Edit dialog ────────────────────────────────────────────────────

  const openEdit = (config: AgentConfigurationResponse) => {
    setEditingAgent(config);
    setEditDescription(config.description);
    setEditInstructions(config.instructionsText);
    setEditRiskTier(config.riskTier);
    setEditIsActive(config.isActive);
    setEditModelId(config.modelId ?? '');
    setShowEditDialog(true);
  };

  const saveConfig = async () => {
    if (!editingAgent) return;
    setSaving(true);
    try {
      await agentConfigService.upsert(editingAgent.name, {
        description: editDescription,
        instructionsText: editInstructions,
        riskTier: editRiskTier,
        isActive: editIsActive,
        modelId: editModelId || null,
      });
      setShowEditDialog(false);
      await loadData();
    } catch (err: unknown) {
      console.error('Failed to save agent config:', err);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to save agent configuration.');
    } finally {
      setSaving(false);
    }
  };

  const deleteOverride = async (config: AgentConfigurationResponse) => {
    if (!config.isOverride) return;
    if (!confirm(`Delete tenant override for "${config.name}"? The agent will revert to the global default.`)) return;
    try {
      await agentConfigService.delete(config.name);
      await loadData();
    } catch (err: unknown) {
      console.error('Failed to delete override:', err);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to delete agent override.');
    }
  };

  // ── Helpers ────────────────────────────────────────────────────────

  const parseToolList = (json: string): string[] => {
    try {
      const parsed = JSON.parse(json);
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  };

  // ── Render ─────────────────────────────────────────────────────────

  const breadcrumbItems = [
    { label: 'AI & Agents', href: '/ai' },
    { label: 'Agents', icon: <Bot className="w-3.5 h-3.5" /> },
  ];

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Agent Configuration</h1>
          <p className="text-[var(--color-text-secondary)]">
            Configure domain agents, assign models, and manage tenant overrides.
          </p>
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

      {/* Search bar */}
      <div className="mb-4">
        <Input
          placeholder="Search agents..."
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          className="h-9 w-[300px] rounded-sm"
        />
      </div>

      {loading && configs.length === 0 ? (
        <Card>
          <CardContent className="p-8 text-center">
            <p className="text-sm text-[var(--color-text-tertiary)]">Loading agents...</p>
          </CardContent>
        </Card>
      ) : displayConfigs.length === 0 ? (
        <Card>
          <CardContent className="p-12 text-center">
            <Bot className="w-12 h-12 mx-auto mb-3 text-[var(--color-text-tertiary)]" />
            <p className="text-sm font-medium text-[var(--color-text-primary)]">No agents found</p>
            <p className="text-xs text-[var(--color-text-tertiary)] mt-1">
              {searchQuery ? 'Try adjusting your search.' : 'Agents will appear here once registered.'}
            </p>
          </CardContent>
        </Card>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          {displayConfigs.map((config) => {
            const tools = parseToolList(config.toolsetIdsJson);
            const riskStyle = riskTierStyles[config.riskTier] ?? riskTierStyles.low;

            return (
              <Card key={config.id} className="cursor-pointer hover:border-[var(--color-brand-primary)] transition-colors" onClick={() => navigate(`/ai/agents/${config.name}`)}>
                <CardContent className="p-5">
                  {/* Header */}
                  <div className="flex items-start justify-between mb-3">
                    <div className="flex items-center gap-2">
                      <Bot className="w-5 h-5 text-[var(--color-brand-primary)]" />
                      <h3 className="text-base font-semibold text-[var(--color-text-primary)]">{config.name}</h3>
                      {config.isOverride && (
                        <span className="px-1.5 py-0.5 rounded text-[10px] font-medium bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)]">
                          OVERRIDE
                        </span>
                      )}
                    </div>
                    <div className="flex items-center gap-1">
                      <Button variant="ghost" size="sm" onClick={(e) => { e.stopPropagation(); openEdit(config); }}>
                        <Pencil className="w-3.5 h-3.5" />
                      </Button>
                      {config.isOverride && (
                        <Button variant="ghost" size="sm" onClick={(e) => { e.stopPropagation(); deleteOverride(config); }} title="Delete override (revert to global)">
                          <RotateCcw className="w-3.5 h-3.5 text-[var(--color-warning)]" />
                        </Button>
                      )}
                    </div>
                  </div>

                  {/* Description */}
                  <p className="text-sm text-[var(--color-text-secondary)] mb-3 line-clamp-2">
                    {config.description || 'No description.'}
                  </p>

                  {/* Meta row */}
                  <div className="flex flex-wrap items-center gap-2 mb-3">
                    <span className="text-xs text-[var(--color-text-tertiary)] bg-[var(--color-surface-inset)] px-2 py-0.5 rounded">
                      {config.domain}
                    </span>
                    <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium ${riskStyle.bg} ${riskStyle.text}`}>
                      <Shield className="w-3 h-3" /> {config.riskTier}
                    </span>
                    {config.isActive ? (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-[var(--color-success-light)] text-[var(--color-success)]">
                        <Check className="w-3 h-3" /> Active
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-[var(--color-surface-inset)] text-[var(--color-text-tertiary)]">
                        <X className="w-3 h-3" /> Inactive
                      </span>
                    )}
                  </div>

                  {/* Model assignment */}
                  <div className="text-xs text-[var(--color-text-tertiary)] mb-2">
                    <span className="font-medium">Model:</span>{' '}
                    {config.modelName ?? (config.modelId ? `ID: ${config.modelId.slice(0, 8)}...` : 'Platform default')}
                  </div>

                  {/* Tools */}
                  {tools.length > 0 && (
                    <div className="flex flex-wrap gap-1">
                      {tools.slice(0, 6).map((tool) => (
                        <span
                          key={tool}
                          className="text-[10px] px-1.5 py-0.5 rounded bg-[var(--color-surface-inset)] text-[var(--color-text-tertiary)] font-mono"
                        >
                          {tool}
                        </span>
                      ))}
                      {tools.length > 6 && (
                        <span className="text-[10px] px-1.5 py-0.5 text-[var(--color-text-tertiary)]">
                          +{tools.length - 6} more
                        </span>
                      )}
                    </div>
                  )}

                  {/* Footer */}
                  <div className="mt-3 pt-3 border-t border-[var(--color-border-light)] text-xs text-[var(--color-text-tertiary)]">
                    Created {formatDate(config.createdAt)}
                    {config.updatedAt ? ` | Updated ${formatDate(config.updatedAt)}` : ''}
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}

      {/* ── Edit dialog ──────────────────────────────────────────── */}
      <Dialog open={showEditDialog} onOpenChange={setShowEditDialog}>
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>Edit Agent: {editingAgent?.name}</DialogTitle>
            <DialogDescription>
              Update the agent configuration. This creates a tenant-level override.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2 max-h-[60vh] overflow-y-auto">
            <div className="grid gap-2">
              <Label htmlFor="edit-description">Description</Label>
              <Input
                id="edit-description"
                value={editDescription}
                onChange={(e) => setEditDescription(e.target.value)}
                placeholder="Agent description"
              />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="edit-instructions">Instructions</Label>
              <Textarea
                id="edit-instructions"
                value={editInstructions}
                onChange={(e) => setEditInstructions(e.target.value)}
                placeholder="System instructions for the agent"
                rows={4}
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="grid gap-2">
                <Label htmlFor="edit-risk-tier">Risk Tier</Label>
                <Select value={editRiskTier} onValueChange={setEditRiskTier}>
                  <SelectTrigger id="edit-risk-tier">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="low">Low</SelectItem>
                    <SelectItem value="medium">Medium</SelectItem>
                    <SelectItem value="high">High</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label htmlFor="edit-model">AI Model</Label>
                <Select value={editModelId || '__none__'} onValueChange={(v) => setEditModelId(v === '__none__' ? '' : v)}>
                  <SelectTrigger id="edit-model">
                    <SelectValue placeholder="Platform default" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__none__">Platform default</SelectItem>
                    {models.filter(m => m.isActive).map((m) => (
                      <SelectItem key={m.id} value={m.id}>{m.modelName}{m.providerName ? ` (${m.providerName})` : ''}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="flex items-center gap-2">
              <input
                type="checkbox"
                id="edit-active"
                checked={editIsActive}
                onChange={(e) => setEditIsActive(e.target.checked)}
                className="rounded"
              />
              <Label htmlFor="edit-active">Active</Label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowEditDialog(false)}>Cancel</Button>
            <Button onClick={saveConfig} disabled={saving}>
              {saving ? 'Saving...' : 'Save Configuration'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
