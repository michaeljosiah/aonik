import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { toast } from 'sonner';

import {
  AlertCircle,
  Bot,
  Brain,
  Check,
  Loader2,
  Plus,
  RefreshCw,
  Save,
  Shield,
  Sparkles,
  X,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';

import type { AgentConfigurationResponse, AiModelResponse, RoutePolicyResponse } from '@/types/ai';
import { agentConfigService, aiModelService, routePolicyService } from '@/services/aiService';
import { loadAgentIcons, type AgentIconOption } from '@/data/agentIcons';

// ── Helpers ─────────────────────────────────────────────────────────

const parseJsonArray = (json: string): string[] => {
  try {
    const parsed = JSON.parse(json);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
};

const DESCRIPTION_MAX = 300;
const NAME_MAX = 80;

// ── Edit state ──────────────────────────────────────────────────────

interface EditState {
  description: string;
  instructionsText: string;
  riskTier: string;
  isActive: boolean;
  tools: string[];
  iconUrl: string;
}

function createEditState(agent: AgentConfigurationResponse): EditState {
  return {
    description: agent.description,
    instructionsText: agent.instructionsText,
    riskTier: agent.riskTier,
    isActive: agent.isActive,
    tools: parseJsonArray(agent.toolsetIdsJson),
    iconUrl: agent.iconUrl ?? '',
  };
}

// ── Main component ──────────────────────────────────────────────────

export function AgentDetailPage() {
  const navigate = useNavigate();
  const { agentName } = useParams<{ agentName: string }>();

  const [agent, setAgent] = useState<AgentConfigurationResponse | null>(null);
  const [models, setModels] = useState<AiModelResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const requestIdRef = useRef(0);

  // Route policy model state — separate from agent config
  const [tenantPolicy, setTenantPolicy] = useState<RoutePolicyResponse | null>(null);
  const [globalPolicy, setGlobalPolicy] = useState<RoutePolicyResponse | null>(null);
  const [selectedModelId, setSelectedModelId] = useState<string | null>(null);

  // Edit state — always active (form-first layout)
  const [editState, setEditState] = useState<EditState | null>(null);
  const [saving, setSaving] = useState(false);
  const [newToolName, setNewToolName] = useState('');
  const [availableIcons, setAvailableIcons] = useState<AgentIconOption[]>([]);
  const [iconPickerOpen, setIconPickerOpen] = useState(false);

  // AI prompt wizard
  const [showPromptWizard, setShowPromptWizard] = useState(false);
  const [wizardIntent, setWizardIntent] = useState('');
  const [wizardImproving, setWizardImproving] = useState(false);
  const [wizardPreview, setWizardPreview] = useState<string | null>(null);

  // ── Data loading ────────────────────────────────────────────────

  const loadData = useCallback(async () => {
    if (!agentName) return;

    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setLoading(true);
    setError(null);

    try {
      const [agentData, modelList, icons, policies] = await Promise.all([
        agentConfigService.get(agentName),
        aiModelService.list(),
        loadAgentIcons(),
        routePolicyService.list(agentName),
      ]);

      if (requestIdRef.current !== requestId) return;

      const tenant = policies.find((p) => p.isOverride) ?? null;
      const global = policies.find((p) => !p.isOverride) ?? null;

      setAgent(agentData);
      setModels(modelList);
      setAvailableIcons(icons);
      setTenantPolicy(tenant);
      setGlobalPolicy(global);
      setSelectedModelId(tenant?.primaryModelId ?? null);
      setEditState(createEditState(agentData));
      setLoading(false);
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) return;
      console.error('Failed to load agent:', err);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load agent configuration. Please try again.');
      setLoading(false);
    }
  }, [agentName]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // ── Edit helpers ────────────────────────────────────────────────

  const updateField = <K extends keyof EditState>(key: K, value: EditState[K]) => {
    setEditState((prev) => (prev ? { ...prev, [key]: value } : prev));
  };

  const addTool = () => {
    const name = newToolName.trim();
    if (!name || !editState) return;
    if (editState.tools.includes(name)) {
      toast.error('Tool already exists.');
      return;
    }
    updateField('tools', [...editState.tools, name]);
    setNewToolName('');
  };

  const removeTool = (toolName: string) => {
    if (!editState) return;
    updateField('tools', editState.tools.filter((t) => t !== toolName));
  };

  // ── AI Prompt wizard ───────────────────────────────────────────

  const openPromptWizard = () => {
    setWizardIntent('');
    setWizardPreview(null);
    setShowPromptWizard(true);
  };

  const runPromptImprove = async () => {
    if (!wizardIntent.trim()) return;
    setWizardImproving(true);
    try {
      const improved = await agentConfigService.improvePrompt(
        editState?.instructionsText || null,
        wizardIntent.trim(),
      );
      setWizardPreview(improved);
    } catch (err) {
      console.error('Failed to improve prompt:', err);
      toast.error('Failed to generate improved prompt. Please try again.');
    } finally {
      setWizardImproving(false);
    }
  };

  const acceptWizardPrompt = () => {
    if (wizardPreview) {
      updateField('instructionsText', wizardPreview);
      toast.success('Prompt updated.');
    }
    setShowPromptWizard(false);
    setWizardPreview(null);
    setWizardIntent('');
  };

  // ── Save / Cancel ──────────────────────────────────────────────

  const saveConfig = async () => {
    if (!agent || !editState) return;
    setSaving(true);
    setError(null);
    try {
      // Save agent config (no modelId — model is managed via route policy)
      await agentConfigService.upsert(agent.name, {
        description: editState.description,
        instructionsText: editState.instructionsText,
        riskTier: editState.riskTier,
        isActive: editState.isActive,
        toolsetIdsJson: JSON.stringify(editState.tools),
        iconUrl: editState.iconUrl || null,
      });

      // Save model via route policy (tenant-scoped)
      if (selectedModelId) {
        if (tenantPolicy) {
          await routePolicyService.update(tenantPolicy.id, { primaryModelId: selectedModelId });
        } else {
          await routePolicyService.create({
            useCase: agent.name,
            primaryModelId: selectedModelId,
            riskTier: editState.riskTier ?? 'Standard',
            dataSensitivity: 'Internal',
            costCeiling: 0,
            isActive: true,
          });
        }
      } else if (tenantPolicy) {
        // Model cleared — remove tenant override policy
        await routePolicyService.delete(tenantPolicy.id);
      }

      toast.success('Agent configuration saved.');
      await loadData();
    } catch (err: unknown) {
      console.error('Failed to save agent config:', err);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      toast.error(message || 'Failed to save agent configuration.');
    } finally {
      setSaving(false);
    }
  };

  const cancelEdit = () => {
    if (agent) {
      setEditState(createEditState(agent));
      setSelectedModelId(tenantPolicy?.primaryModelId ?? null);
    }
  };

  // ── Render guards ──────────────────────────────────────────────

  const breadcrumbItems = [
    { label: 'AI & Agents', href: '/ai/agents' },
    { label: 'Agents', href: '/ai/agents', icon: <Bot className="w-3.5 h-3.5" /> },
    { label: agentName || 'Agent' },
  ];

  if (loading) {
    return (
      <div className="flex-1 flex items-center justify-center h-full">
        <div className="text-center">
          <RefreshCw className="w-8 h-8 animate-spin mx-auto mb-3 text-[var(--color-brand-primary)]" />
          <p className="text-[var(--color-text-secondary)]">Loading agent...</p>
        </div>
      </div>
    );
  }

  if (!agent || !editState) {
    return (
      <div className="flex-1 flex items-center justify-center h-full">
        <div className="text-center">
          <AlertCircle className="w-12 h-12 mx-auto mb-3 text-[var(--color-error)]" />
          <h2 className="text-xl font-semibold text-[var(--color-text-primary)] mb-2">Agent Not Found</h2>
          <p className="text-[var(--color-text-secondary)] mb-4">
            {error || "The agent you're looking for doesn't exist or you don't have access."}
          </p>
          <Button onClick={() => navigate('/ai/agents')}>Back to Agents</Button>
        </div>
      </div>
    );
  }

  // ── Derived ────────────────────────────────────────────────────

  const riskTierStyles: Record<string, { text: string; bg: string }> = {
    low: { text: 'text-[var(--color-success)]', bg: 'bg-[var(--color-success-light)]' },
    medium: { text: 'text-[var(--color-warning)]', bg: 'bg-[var(--color-warning-light)]' },
    high: { text: 'text-[var(--color-error)]', bg: 'bg-[var(--color-error-light)]' },
  };

  const currentIcon = editState.iconUrl;
  const selectedModel = models.find((m) => m.id === selectedModelId);
  const globalModel = models.find((m) => m.id === globalPolicy?.primaryModelId);
  const riskStyle = riskTierStyles[editState.riskTier] ?? riskTierStyles.low;

  // ── Render ─────────────────────────────────────────────────────

  return (
    <div className="h-full overflow-auto bg-[var(--color-background)]">
      {/* Header */}
      <div className="px-6 py-4 border-b border-[var(--color-border-light)] bg-[var(--color-surface)]">
        <Breadcrumb items={breadcrumbItems} />
      </div>

      {error && (
        <div className="flex justify-center px-6 pt-4">
          <Card className="border-[var(--color-error)] bg-[var(--color-error-light)] max-w-[64rem] w-full">
            <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
              <AlertCircle className="w-5 h-5" />
              <span className="flex-1">{error}</span>
              <Button variant="ghost" size="sm" onClick={loadData}>Retry</Button>
            </CardContent>
          </Card>
        </div>
      )}

      {/* Centered card container */}
      <div className="flex justify-center px-6 py-8">
        <Card className="w-full max-w-[64rem] shadow-sm">
          <CardContent className="p-0">
            <div className="flex flex-col xl:flex-row">

              {/* ── LEFT COLUMN — Form ── */}
              <div className="flex-1 min-w-0 p-8 space-y-7">

                {/* Agent identity — icon + name */}
                <div className="flex items-start gap-5">
                  <Popover open={iconPickerOpen} onOpenChange={setIconPickerOpen}>
                    <PopoverTrigger asChild>
                      <button
                        type="button"
                        className="w-[88px] h-[88px] rounded-full flex-shrink-0 border-2 border-[var(--color-border-light)] hover:border-[var(--color-brand-primary)] overflow-hidden flex items-center justify-center transition-colors bg-[var(--color-surface-inset)] cursor-pointer"
                        title="Change agent icon"
                      >
                        {currentIcon ? (
                          <img src={currentIcon} alt="" className="w-full h-full object-cover" />
                        ) : (
                          <Bot className="w-10 h-10 text-[var(--color-text-tertiary)]" />
                        )}
                      </button>
                    </PopoverTrigger>
                    <PopoverContent side="bottom" align="start" sideOffset={8} collisionPadding={16} className="!w-[320px] !max-w-[90vw] p-4">
                      <div className="flex items-center justify-between mb-3">
                        <p className="text-sm font-medium text-[var(--color-text-primary)]">Choose a preset avatar</p>
                        <button type="button" onClick={() => setIconPickerOpen(false)} className="text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)]">
                          <X className="w-4 h-4" />
                        </button>
                      </div>
                      <div className="flex flex-wrap gap-2 mb-3">
                        {availableIcons.map((icon) => (
                          <button
                            key={icon.url}
                            type="button"
                            onClick={() => { updateField('iconUrl', icon.url); setIconPickerOpen(false); }}
                            className={`w-12 h-12 rounded-full border-2 overflow-hidden transition-colors ${
                              editState.iconUrl === icon.url
                                ? 'border-[var(--color-brand-primary)] ring-2 ring-[var(--color-brand-primary)]'
                                : 'border-[var(--color-border-light)] hover:border-[var(--color-border)]'
                            }`}
                            title={icon.label}
                          >
                            <img src={icon.url} alt={icon.label} className="w-full h-full object-cover" />
                          </button>
                        ))}
                      </div>
                      <Button
                        variant="outline"
                        size="sm"
                        className="text-xs"
                        onClick={() => { updateField('iconUrl', ''); setIconPickerOpen(false); }}
                      >
                        Remove image
                      </Button>
                    </PopoverContent>
                  </Popover>
                  <div className="flex-1 space-y-1.5 pt-2">
                    <Label className="text-sm font-medium text-[var(--color-text-secondary)]">Agent name</Label>
                    <div className="text-xl font-bold text-[var(--color-text-primary)]">
                      {agent.name}
                    </div>
                    <p className="text-xs text-[var(--color-text-tertiary)]">{agent.name.length} / {NAME_MAX} characters</p>
                  </div>
                </div>

                {/* Description */}
                <div className="space-y-2">
                  <div className="flex items-center justify-between">
                    <Label className="text-sm font-medium text-[var(--color-text-secondary)]">Description (optional)</Label>
                    <Button variant="ghost" size="sm" onClick={openPromptWizard} className="gap-1.5 text-xs h-7 text-[var(--color-brand-primary)] hover:text-[var(--color-brand-primary)]">
                      <Sparkles className="w-3.5 h-3.5" />
                      Improve with AI
                    </Button>
                  </div>
                  <Textarea
                    value={editState.description}
                    onChange={(e) => {
                      if (e.target.value.length <= DESCRIPTION_MAX) updateField('description', e.target.value);
                    }}
                    placeholder="A short description of what this agent does"
                    rows={3}
                  />
                  <p className="text-xs text-[var(--color-text-tertiary)]">
                    {editState.description.length} / {DESCRIPTION_MAX} characters
                  </p>
                </div>

                {/* AI model — tenant override via route policy */}
                <div className="space-y-2">
                  <Label className="text-sm font-medium text-[var(--color-text-secondary)]">AI model</Label>
                  <Select
                    value={selectedModelId ?? '__none__'}
                    onValueChange={(v) => setSelectedModelId(v === '__none__' ? null : v)}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder={globalModel ? `Default: ${globalModel.modelName}` : 'No default set'} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="__none__">
                        {globalModel ? `Use global default (${globalModel.modelName})` : 'No model assigned'}
                      </SelectItem>
                      {models.filter((m) => m.isActive).map((m) => (
                        <SelectItem key={m.id} value={m.id}>
                          {m.modelName}
                          {m.providerName ? ` · ${m.providerName}` : ''}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {selectedModelId ? (
                    <p className="text-xs text-[var(--color-text-tertiary)]">
                      Tenant override — overrides the global default for this agent.
                    </p>
                  ) : globalModel ? (
                    <p className="text-xs text-[var(--color-text-tertiary)]">
                      Using global default: <span className="font-medium">{globalModel.modelName}</span>. Select a model above to override for this tenant.
                    </p>
                  ) : (
                    <p className="text-xs text-[var(--color-text-tertiary)]">
                      No model assigned. Set a global default in Route Policies or select one above.
                    </p>
                  )}
                </div>

                {/* System prompt */}
                <div className="space-y-2">
                  <div className="flex items-center justify-between">
                    <Label className="text-sm font-medium text-[var(--color-text-secondary)]">System prompt</Label>
                    <Button variant="ghost" size="sm" onClick={openPromptWizard} className="gap-1.5 text-xs h-7 text-[var(--color-brand-primary)] hover:text-[var(--color-brand-primary)]">
                      <Sparkles className="w-3.5 h-3.5" />
                      Regenerate with AI
                    </Button>
                  </div>
                  <Textarea
                    value={editState.instructionsText}
                    onChange={(e) => updateField('instructionsText', e.target.value)}
                    placeholder="Enter system instructions for the agent..."
                    rows={6}
                    className="font-mono text-xs leading-relaxed"
                  />

                  {/* Inline AI prompt wizard */}
                  {showPromptWizard && (
                    <div className="space-y-3 pt-1">
                      <Textarea
                        value={wizardIntent}
                        onChange={(e) => setWizardIntent(e.target.value)}
                        placeholder="Describe what you want the prompt to do or how to improve it..."
                        rows={2}
                        disabled={wizardImproving}
                        className="text-sm"
                        onKeyDown={(e) => {
                          if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) {
                            e.preventDefault();
                            runPromptImprove();
                          }
                        }}
                      />
                      <div className="flex items-center gap-2">
                        <Button size="sm" onClick={runPromptImprove} disabled={wizardImproving || !wizardIntent.trim()} className="gap-1.5">
                          {wizardImproving ? (
                            <><Loader2 className="w-3.5 h-3.5 animate-spin" /> Generating...</>
                          ) : (
                            <><Sparkles className="w-3.5 h-3.5" /> Generate prompt</>
                          )}
                        </Button>
                        <Button variant="ghost" size="sm" onClick={() => { setShowPromptWizard(false); setWizardPreview(null); setWizardIntent(''); }} className="gap-1 text-[var(--color-text-tertiary)]">
                          <X className="w-3.5 h-3.5" /> Discard
                        </Button>
                        {wizardPreview && (
                          <>
                            <Button variant="ghost" size="sm" onClick={() => { setWizardPreview(null); }} className="gap-1 text-[var(--color-text-tertiary)]">
                              <RefreshCw className="w-3.5 h-3.5" /> Regenerate
                            </Button>
                            <Button variant="ghost" size="sm" onClick={acceptWizardPrompt} className="gap-1 text-[var(--color-success)]">
                              <Check className="w-3.5 h-3.5" /> Accept
                            </Button>
                          </>
                        )}
                      </div>
                    </div>
                  )}
                </div>

                {/* Tools */}
                <div className="space-y-2">
                  <Label className="text-sm font-medium text-[var(--color-text-secondary)]">Tools ({editState.tools.length})</Label>
                  <div className="flex items-center gap-2">
                    <Input
                      value={newToolName}
                      onChange={(e) => setNewToolName(e.target.value)}
                      onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); addTool(); } }}
                      placeholder="Enter tool name (e.g. finance_get_invoice)"
                      className="font-mono text-xs flex-1"
                    />
                    <Button variant="outline" size="sm" onClick={addTool} disabled={!newToolName.trim()}>
                      <Plus className="w-3.5 h-3.5 mr-1" />
                      Add
                    </Button>
                  </div>
                  {editState.tools.length > 0 && (
                    <div className="flex flex-wrap gap-2 pt-1">
                      {editState.tools.map((tool) => (
                        <span
                          key={tool}
                          className="inline-flex items-center gap-1 text-xs px-2.5 py-1 rounded-full bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)] border border-[var(--color-border-light)]"
                        >
                          {tool}
                          <button
                            type="button"
                            onClick={() => removeTool(tool)}
                            className="ml-0.5 rounded-full hover:bg-[var(--color-error-light)] hover:text-[var(--color-error)] p-0.5 transition-colors"
                          >
                            <X className="w-3 h-3" />
                          </button>
                        </span>
                      ))}
                    </div>
                  )}
                </div>

                {/* Active toggle */}
                <div className="flex items-center justify-between rounded-md border border-[var(--color-border-light)] px-4 py-3">
                  <div>
                    <p className="text-sm font-medium text-[var(--color-text-primary)]">Agent active</p>
                    <p className="text-xs text-[var(--color-text-tertiary)]">When disabled, this agent will not be available for use.</p>
                  </div>
                  <Switch
                    checked={editState.isActive}
                    onCheckedChange={(checked) => updateField('isActive', checked)}
                  />
                </div>

                {/* Action buttons */}
                <div className="flex items-center gap-3 pt-2">
                  <Button variant="ghost" onClick={cancelEdit} className="text-[var(--color-text-secondary)]">
                    Cancel
                  </Button>
                  <Button onClick={saveConfig} disabled={saving} className="px-8">
                    {saving ? (
                      <>
                        <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                        Saving...
                      </>
                    ) : (
                      <>
                        <Save className="w-4 h-4 mr-2" />
                        Save changes
                      </>
                    )}
                  </Button>
                </div>
              </div>

              {/* ── Vertical divider ── */}
              <div className="hidden xl:block w-px bg-[var(--color-border-light)] self-stretch" />

              {/* ── RIGHT COLUMN — Agent summary preview ── */}
              <div className="w-full xl:w-[340px] flex-shrink-0 p-8 bg-[var(--color-surface)]">
                <div className="sticky top-8">
                  <p className="text-xs font-semibold text-[var(--color-brand-primary)] mb-6 text-right tracking-wide uppercase">
                    Agent summary
                  </p>

                  {/* Avatar */}
                  <div className="w-20 h-20 rounded-2xl overflow-hidden mb-5 bg-[var(--color-surface-inset)] flex items-center justify-center border border-[var(--color-border-light)]">
                    {currentIcon ? (
                      <img src={currentIcon} alt="" className="w-full h-full object-cover" />
                    ) : (
                      <Bot className="w-9 h-9 text-[var(--color-text-tertiary)]" />
                    )}
                  </div>

                  {/* Name */}
                  <h3 className="text-xl font-bold text-[var(--color-text-primary)] mb-2">
                    {agent.name}
                  </h3>

                  {/* Description */}
                  <p className="text-sm text-[var(--color-text-secondary)] mb-6 line-clamp-3 leading-relaxed">
                    {editState.description || 'No description provided.'}
                  </p>

                  {/* Model */}
                  <div className="mb-5">
                    <p className="text-[10px] font-semibold text-[var(--color-text-tertiary)] uppercase tracking-wider mb-1.5">
                      AI Model
                    </p>
                    <div className="flex items-center gap-1.5">
                      <Brain className="w-3.5 h-3.5 text-[var(--color-text-tertiary)]" />
                      <span className="text-sm text-[var(--color-text-primary)]">
                        {selectedModel ? selectedModel.modelName : globalModel ? globalModel.modelName : 'No model assigned'}
                      </span>
                    </div>
                    {selectedModel && (
                      <p className="text-[10px] text-[var(--color-brand-primary)] mt-0.5">Tenant override</p>
                    )}
                    {!selectedModel && globalModel && (
                      <p className="text-[10px] text-[var(--color-text-tertiary)] mt-0.5">Global default</p>
                    )}
                  </div>

                  {/* Risk tier */}
                  <div className="mb-5">
                    <p className="text-[10px] font-semibold text-[var(--color-text-tertiary)] uppercase tracking-wider mb-1.5">
                      Risk tier
                    </p>
                    <Badge className={`${riskStyle.bg} ${riskStyle.text} text-xs`}>
                      <Shield className="w-3 h-3 mr-1" />
                      {editState.riskTier}
                    </Badge>
                  </div>

                  {/* Status */}
                  <div className="mb-5">
                    <p className="text-[10px] font-semibold text-[var(--color-text-tertiary)] uppercase tracking-wider mb-1.5">
                      Status
                    </p>
                    {editState.isActive ? (
                      <Badge className="bg-[var(--color-success-light)] text-[var(--color-success)] text-xs">
                        <Check className="w-3 h-3 mr-1" /> Active
                      </Badge>
                    ) : (
                      <Badge className="bg-[var(--color-surface-inset)] text-[var(--color-text-tertiary)] text-xs">
                        <X className="w-3 h-3 mr-1" /> Inactive
                      </Badge>
                    )}
                  </div>

                  {/* Tools */}
                  <div>
                    <p className="text-[10px] font-semibold text-[var(--color-text-tertiary)] uppercase tracking-wider mb-1.5">
                      Tools
                    </p>
                    {editState.tools.length === 0 ? (
                      <p className="text-xs text-[var(--color-text-tertiary)]">None assigned</p>
                    ) : (
                      <div className="flex flex-wrap gap-1.5">
                        {editState.tools.slice(0, 3).map((tool) => (
                          <span
                            key={tool}
                            className="text-xs px-2 py-0.5 rounded bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)] border border-[var(--color-border-light)]"
                          >
                            {tool.length > 20 ? tool.slice(0, 20) + '...' : tool}
                          </span>
                        ))}
                        {editState.tools.length > 3 && (
                          <span className="text-xs px-2 py-0.5 rounded bg-[var(--color-surface-inset)] text-[var(--color-text-tertiary)] border border-[var(--color-border-light)]">
                            +{editState.tools.length - 3}
                          </span>
                        )}
                      </div>
                    )}
                  </div>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
