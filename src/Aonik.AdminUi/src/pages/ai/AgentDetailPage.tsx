import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { toast } from 'sonner';

import {
  AlertCircle,
  Bot,
  Brain,
  Check,
  Clock,
  MessageSquare,
  MonitorCog,
  Pencil,
  Play,
  Plus,
  RefreshCw,
  RotateCcw,
  Save,
  Server,
  Shield,
  Wrench,
  X,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

import { DataTable, DataTablePagination } from '@/components/ui/data-table';
import type { ColumnDef } from '@/components/ui/data-table';

import type { AgentConfigurationResponse, AgentRunSummary, AiModelResponse } from '@/types/ai';
import { agentConfigService, agentRunService, aiModelService } from '@/services/aiService';

// ── Styles ──────────────────────────────────────────────────────────

const riskTierStyles: Record<string, { text: string; bg: string }> = {
  low: { text: 'text-[var(--color-success)]', bg: 'bg-[var(--color-success-light)]' },
  medium: { text: 'text-[var(--color-warning)]', bg: 'bg-[var(--color-warning-light)]' },
  high: { text: 'text-[var(--color-error)]', bg: 'bg-[var(--color-error-light)]' },
};

const domainStyles: Record<string, { bg: string; text: string }> = {
  finance: { bg: 'bg-emerald-500/10', text: 'text-emerald-600' },
  'personal-finance': { bg: 'bg-blue-500/10', text: 'text-blue-600' },
  platform: { bg: 'bg-violet-500/10', text: 'text-violet-600' },
  custom: { bg: 'bg-amber-500/10', text: 'text-amber-600' },
};

// ── Helpers ─────────────────────────────────────────────────────────

const formatDate = (dateString?: string | null) => {
  if (!dateString) return '—';
  return new Date(dateString).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
};

const formatDateTime = (dateString?: string | null) => {
  if (!dateString) return '—';
  return new Date(dateString).toLocaleString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
};

const parseJsonArray = (json: string): string[] => {
  try {
    const parsed = JSON.parse(json);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
};

// ── Sub-components ──────────────────────────────────────────────────

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-start justify-between gap-4 py-2">
      <span className="text-xs text-[var(--color-text-tertiary)]">{label}</span>
      <span className="text-sm text-[var(--color-text-primary)] text-right">{value}</span>
    </div>
  );
}

function ToggleRow({
  title,
  description,
  checked,
  onCheckedChange,
}: {
  title: string;
  description: string;
  checked: boolean;
  onCheckedChange: (checked: boolean) => void;
}) {
  return (
    <div className="flex items-start justify-between gap-4 rounded-md border border-[var(--color-border-light)] px-4 py-3">
      <div>
        <p className="text-sm font-medium text-[var(--color-text-primary)]">{title}</p>
        <p className="text-xs text-[var(--color-text-tertiary)]">{description}</p>
      </div>
      <Switch checked={checked} onCheckedChange={onCheckedChange} />
    </div>
  );
}

// ── Edit state ──────────────────────────────────────────────────────

interface EditState {
  description: string;
  instructionsText: string;
  riskTier: string;
  isActive: boolean;
  modelId: string;
  tools: string[];
}

function createEditState(agent: AgentConfigurationResponse): EditState {
  return {
    description: agent.description,
    instructionsText: agent.instructionsText,
    riskTier: agent.riskTier,
    isActive: agent.isActive,
    modelId: agent.modelId ?? '',
    tools: parseJsonArray(agent.toolsetIdsJson),
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
  const [activeTab, setActiveTab] = useState('details');
  const requestIdRef = useRef(0);

  // Edit mode
  const [isEditing, setIsEditing] = useState(false);
  const [editState, setEditState] = useState<EditState | null>(null);
  const [saving, setSaving] = useState(false);
  const [newToolName, setNewToolName] = useState('');

  // Runs tab
  const [runs, setRuns] = useState<AgentRunSummary[]>([]);
  const [runsLoading, setRunsLoading] = useState(false);
  const [runsPage, setRunsPage] = useState(1);
  const [runsPageSize] = useState(20);
  const [runsTotalCount, setRunsTotalCount] = useState(0);

  // ── Data loading ────────────────────────────────────────────────

  const loadData = useCallback(async () => {
    if (!agentName) return;

    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setLoading(true);
    setError(null);

    try {
      const [agentData, modelList] = await Promise.all([
        agentConfigService.get(agentName),
        aiModelService.list(),
      ]);

      if (requestIdRef.current !== requestId) return;

      setAgent(agentData);
      setModels(modelList);
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

  // ── Runs loading ────────────────────────────────────────────────

  const loadRuns = useCallback(async () => {
    if (!agent) return;
    setRunsLoading(true);
    try {
      const result = await agentRunService.list(agent.id, runsPage, runsPageSize);
      setRuns(result.items);
      setRunsTotalCount(result.totalCount);
    } catch (err: unknown) {
      console.error('Failed to load agent runs:', err);
    } finally {
      setRunsLoading(false);
    }
  }, [agent, runsPage, runsPageSize]);

  useEffect(() => {
    if (activeTab === 'runs' && agent) {
      loadRuns();
    }
  }, [activeTab, loadRuns, agent]);

  // ── Runs columns ────────────────────────────────────────────────

  const runStatusStyles: Record<string, { text: string; bg: string }> = {
    completed: { text: 'text-[var(--color-success)]', bg: 'bg-[var(--color-success-light)]' },
    failed: { text: 'text-[var(--color-error)]', bg: 'bg-[var(--color-error-light)]' },
    running: { text: 'text-[var(--color-brand-primary)]', bg: 'bg-[var(--color-brand-primary-light)]' },
    started: { text: 'text-[var(--color-brand-primary)]', bg: 'bg-[var(--color-brand-primary-light)]' },
  };

  const runsColumns: ColumnDef<AgentRunSummary>[] = [
    {
      id: 'status',
      header: 'Status',
      accessorKey: 'status',
      cell: (row) => {
        const style = runStatusStyles[row.status.toLowerCase()] ?? runStatusStyles.started;
        return (
          <Badge className={`${style.bg} ${style.text} text-xs capitalize`}>
            {row.status}
          </Badge>
        );
      },
    },
    {
      id: 'goal',
      header: 'Goal',
      accessorKey: 'goal',
      cell: (row) => (
        <span className="text-sm text-[var(--color-text-primary)] line-clamp-1" title={row.goal}>
          {row.goal || '—'}
        </span>
      ),
    },
    {
      id: 'steps',
      header: 'Steps',
      accessorKey: 'stepCount',
      cell: (row) => (
        <span className="text-sm text-[var(--color-text-secondary)]">{row.stepCount}</span>
      ),
    },
    {
      id: 'aiRuns',
      header: 'AI Runs',
      accessorKey: 'linkedAiRunCount',
      cell: (row) => (
        <span className="text-sm text-[var(--color-text-secondary)]">{row.linkedAiRunCount}</span>
      ),
    },
    {
      id: 'createdAt',
      header: 'Started',
      accessorKey: 'createdAt',
      sortable: true,
      cell: (row) => (
        <span className="text-sm text-[var(--color-text-secondary)]">
          {formatDateTime(row.createdAt)}
        </span>
      ),
    },
  ];

  // ── Edit helpers ────────────────────────────────────────────────

  const enterEditMode = () => {
    if (!agent) return;
    setEditState(createEditState(agent));
    setNewToolName('');
    setIsEditing(true);
    setActiveTab('details');
  };

  const cancelEdit = () => {
    setIsEditing(false);
    setEditState(null);
    setNewToolName('');
  };

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

  const saveConfig = async () => {
    if (!agent || !editState) return;
    setSaving(true);
    setError(null);
    try {
      await agentConfigService.upsert(agent.name, {
        description: editState.description,
        instructionsText: editState.instructionsText,
        riskTier: editState.riskTier,
        isActive: editState.isActive,
        modelId: editState.modelId || null,
        toolsetIdsJson: JSON.stringify(editState.tools),
      });
      setIsEditing(false);
      setEditState(null);
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

  const deleteOverride = async () => {
    if (!agent?.isOverride) return;
    if (!confirm(`Delete tenant override for "${agent.name}"? The agent will revert to the global default.`)) return;
    try {
      await agentConfigService.delete(agent.name);
      toast.success('Override deleted. Reverted to global default.');
      await loadData();
    } catch (err: unknown) {
      console.error('Failed to delete override:', err);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      toast.error(message || 'Failed to delete agent override.');
    }
  };

  // ── Render guards ───────────────────────────────────────────────

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

  if (!agent) {
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

  // ── Derived data ────────────────────────────────────────────────

  const tools = parseJsonArray(agent.toolsetIdsJson);
  const domainStyle = domainStyles[agent.domain] ?? domainStyles.custom;

  // ── Render ──────────────────────────────────────────────────────

  return (
    <div className="h-full overflow-auto bg-[var(--color-background)]">
      {/* Header */}
      <div className="px-6 py-4 flex items-center justify-between border-b border-[var(--color-border-light)] bg-[var(--color-surface)]">
        <div>
          <h1 className="text-lg font-semibold text-[var(--color-text-primary)]">Agent Details</h1>
          <Breadcrumb items={breadcrumbItems} className="mt-1" />
        </div>
        <div className="flex items-center gap-2">
          {isEditing ? (
            <>
              <Button variant="outline" size="sm" onClick={cancelEdit}>
                <X className="w-3.5 h-3.5 mr-1.5" />
                Cancel
              </Button>
              <Button size="sm" onClick={saveConfig} disabled={saving}>
                <Save className="w-3.5 h-3.5 mr-1.5" />
                {saving ? 'Saving...' : 'Save Configuration'}
              </Button>
            </>
          ) : (
            <>
              {agent.isOverride && (
                <Button variant="outline" size="sm" onClick={deleteOverride} title="Revert to global default">
                  <RotateCcw className="w-3.5 h-3.5 mr-1.5" />
                  Revert Override
                </Button>
              )}
              <Button variant="outline" size="sm" onClick={enterEditMode}>
                <Pencil className="w-3.5 h-3.5 mr-1.5" />
                Edit
              </Button>
              <Button variant="outline" size="sm" onClick={loadData}>
                Refresh
              </Button>
            </>
          )}
        </div>
      </div>

      {error && (
        <div className="px-6 pt-4">
          <Card className="border-[var(--color-error)] bg-[var(--color-error-light)]">
            <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
              <AlertCircle className="w-5 h-5" />
              <span className="flex-1">{error}</span>
              <Button variant="ghost" size="sm" onClick={loadData}>
                Retry
              </Button>
            </CardContent>
          </Card>
        </div>
      )}

      {/* Editing banner */}
      {isEditing && (
        <div className="px-6 pt-4">
          <div className="rounded-md border border-[var(--color-brand-primary)] bg-[var(--color-brand-primary-light)] px-4 py-2.5 flex items-center gap-2">
            <Pencil className="w-4 h-4 text-[var(--color-brand-primary)]" />
            <span className="text-sm text-[var(--color-brand-primary)] font-medium">
              Editing mode — changes will create a tenant-level override
            </span>
          </div>
        </div>
      )}

      <div className="p-6">
        <div className="flex flex-col xl:flex-row gap-6">
          {/* Left sidebar */}
          <div className="w-full xl:w-80 flex-shrink-0 space-y-6">
            <Card>
              <CardContent className="p-6">
                <div className="text-center mb-6">
                  <div className={`w-16 h-16 rounded-2xl mx-auto mb-3 flex items-center justify-center ${domainStyle.bg}`}>
                    <Bot className={`w-8 h-8 ${domainStyle.text}`} />
                  </div>
                  <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">
                    {agent.name}
                  </h2>
                  <p className="text-sm text-[var(--color-text-tertiary)] mt-1 line-clamp-2">
                    {isEditing ? editState?.description : agent.description || 'No description'}
                  </p>
                  <div className="mt-3 flex items-center justify-center gap-2 flex-wrap">
                    {(isEditing ? editState?.isActive : agent.isActive) ? (
                      <Badge className="bg-[var(--color-success-light)] text-[var(--color-success)] text-xs">
                        <Check className="w-3 h-3 mr-1" /> Active
                      </Badge>
                    ) : (
                      <Badge className="bg-[var(--color-surface-inset)] text-[var(--color-text-tertiary)] text-xs">
                        <X className="w-3 h-3 mr-1" /> Inactive
                      </Badge>
                    )}
                    {(() => {
                      const tier = isEditing ? editState?.riskTier ?? agent.riskTier : agent.riskTier;
                      const rs = riskTierStyles[tier] ?? riskTierStyles.low;
                      return (
                        <Badge className={`${rs.bg} ${rs.text} text-xs`}>
                          <Shield className="w-3 h-3 mr-1" /> {tier}
                        </Badge>
                      );
                    })()}
                    {agent.isOverride && (
                      <Badge className="bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)] text-xs">
                        Override
                      </Badge>
                    )}
                  </div>
                </div>

                <div className="space-y-3 border-t border-[var(--color-border-light)] pt-4">
                  <div className="flex items-center gap-2 text-sm text-[var(--color-text-secondary)]">
                    <MonitorCog className="w-4 h-4 text-[var(--color-text-tertiary)]" />
                    <span>Domain: </span>
                    <span className={`px-1.5 py-0.5 rounded text-xs font-medium ${domainStyle.bg} ${domainStyle.text}`}>
                      {agent.domain}
                    </span>
                  </div>
                  <div className="flex items-center gap-2 text-sm text-[var(--color-text-secondary)]">
                    <Brain className="w-4 h-4 text-[var(--color-text-tertiary)]" />
                    <span>Model: {agent.modelName ?? 'Platform default'}</span>
                  </div>
                  <div className="flex items-center gap-2 text-sm text-[var(--color-text-secondary)]">
                    <Wrench className="w-4 h-4 text-[var(--color-text-tertiary)]" />
                    <span>
                      {(isEditing ? editState?.tools.length : tools.length) ?? 0} tool
                      {(isEditing ? editState?.tools.length : tools.length) !== 1 ? 's' : ''} assigned
                    </span>
                  </div>
                </div>

                <div className="mt-6">
                  <div className="flex items-center gap-1.5 mb-3">
                    <Clock className="w-3.5 h-3.5 text-[var(--color-text-tertiary)]" />
                    <span className="text-xs font-medium text-[var(--color-text-tertiary)]">Timeline</span>
                  </div>
                  <div className="space-y-2">
                    <div className="rounded-lg border border-[var(--color-border-light)] p-3">
                      <p className="text-xs text-[var(--color-text-tertiary)]">Created</p>
                      <p className="text-sm font-medium text-[var(--color-text-primary)]">
                        {formatDateTime(agent.createdAt)}
                      </p>
                    </div>
                    {agent.updatedAt && (
                      <div className="rounded-lg border border-[var(--color-border-light)] p-3">
                        <p className="text-xs text-[var(--color-text-tertiary)]">Last updated</p>
                        <p className="text-sm font-medium text-[var(--color-text-primary)]">
                          {formatDateTime(agent.updatedAt)}
                        </p>
                      </div>
                    )}
                  </div>
                </div>
              </CardContent>
            </Card>
          </div>

          {/* Right content area — tabs */}
          <div className="flex-1 min-w-0">
            <Card>
              <CardContent className="p-0">
                <Tabs value={activeTab} onValueChange={setActiveTab}>
                  <div className="flex items-center justify-between border-b border-[var(--color-border-light)] px-4">
                    <TabsList className="bg-transparent p-0 h-auto flex flex-wrap gap-0">
                      {[
                        { value: 'details', label: 'Details' },
                        { value: 'mcp', label: 'MCP Servers' },
                        { value: 'conversations', label: 'Conversations' },
                        { value: 'runs', label: 'Runs' },
                      ].map((tab) => (
                        <TabsTrigger
                          key={tab.value}
                          value={tab.value}
                          className="px-4 py-3 text-sm rounded-none border-b-2 border-transparent data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent data-[state=active]:text-[var(--color-brand-primary)]"
                        >
                          {tab.label}
                        </TabsTrigger>
                      ))}
                    </TabsList>
                  </div>

                  <div className="p-6">
                    {/* ── Details Tab ── */}
                    <TabsContent value="details" className="mt-0">
                      {isEditing && editState ? (
                        /* ── EDIT MODE ── */
                        <div className="space-y-6">
                          {/* General Configuration */}
                          <Card>
                            <CardHeader>
                              <CardTitle className="text-sm">General Configuration</CardTitle>
                              <CardDescription>Core identity and behaviour settings for this agent.</CardDescription>
                            </CardHeader>
                            <CardContent className="space-y-4">
                              <div className="space-y-2">
                                <Label htmlFor="edit-description">Description</Label>
                                <Input
                                  id="edit-description"
                                  value={editState.description}
                                  onChange={(e) => updateField('description', e.target.value)}
                                  placeholder="A short description of what this agent does"
                                />
                              </div>
                              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                <div className="space-y-2">
                                  <Label>Risk Tier</Label>
                                  <Select value={editState.riskTier} onValueChange={(v) => updateField('riskTier', v)}>
                                    <SelectTrigger>
                                      <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent>
                                      <SelectItem value="low">Low</SelectItem>
                                      <SelectItem value="medium">Medium</SelectItem>
                                      <SelectItem value="high">High</SelectItem>
                                    </SelectContent>
                                  </Select>
                                </div>
                                <div className="space-y-2">
                                  <Label>AI Model</Label>
                                  <Select
                                    value={editState.modelId || '__none__'}
                                    onValueChange={(v) => updateField('modelId', v === '__none__' ? '' : v)}
                                  >
                                    <SelectTrigger>
                                      <SelectValue placeholder="Platform default" />
                                    </SelectTrigger>
                                    <SelectContent>
                                      <SelectItem value="__none__">Platform default</SelectItem>
                                      {models.filter((m) => m.isActive).map((m) => (
                                        <SelectItem key={m.id} value={m.id}>
                                          {m.modelName}
                                          {m.providerName ? ` (${m.providerName})` : ''}
                                        </SelectItem>
                                      ))}
                                    </SelectContent>
                                  </Select>
                                </div>
                              </div>
                              <ToggleRow
                                title="Agent active"
                                description="When disabled, this agent will not be available for orchestration or direct invocation."
                                checked={editState.isActive}
                                onCheckedChange={(checked) => updateField('isActive', checked)}
                              />
                            </CardContent>
                          </Card>

                          {/* System Prompt */}
                          <Card>
                            <CardHeader>
                              <CardTitle className="text-sm">System Prompt</CardTitle>
                              <CardDescription>The instructions that define this agent's behaviour and capabilities.</CardDescription>
                            </CardHeader>
                            <CardContent>
                              <Textarea
                                value={editState.instructionsText}
                                onChange={(e) => updateField('instructionsText', e.target.value)}
                                placeholder="Enter system instructions for the agent..."
                                rows={14}
                                className="font-mono text-xs"
                              />
                            </CardContent>
                          </Card>

                          {/* Tools */}
                          <Card>
                            <CardHeader>
                              <CardTitle className="text-sm">Tools ({editState.tools.length})</CardTitle>
                              <CardDescription>Functions this agent can invoke. Add or remove tool bindings.</CardDescription>
                            </CardHeader>
                            <CardContent className="space-y-4">
                              {/* Add tool input */}
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

                              {/* Tool chips */}
                              {editState.tools.length === 0 ? (
                                <p className="text-sm text-[var(--color-text-tertiary)]">No tools assigned.</p>
                              ) : (
                                <div className="flex flex-wrap gap-2">
                                  {editState.tools.map((tool) => (
                                    <span
                                      key={tool}
                                      className="inline-flex items-center gap-1.5 text-xs px-2.5 py-1 rounded-md bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)] font-mono border border-[var(--color-border-light)]"
                                    >
                                      <Wrench className="w-3 h-3 text-[var(--color-text-tertiary)]" />
                                      {tool}
                                      <button
                                        type="button"
                                        onClick={() => removeTool(tool)}
                                        className="ml-1 rounded-sm hover:bg-[var(--color-error-light)] hover:text-[var(--color-error)] p-0.5 transition-colors"
                                        title={`Remove ${tool}`}
                                      >
                                        <X className="w-3 h-3" />
                                      </button>
                                    </span>
                                  ))}
                                </div>
                              )}
                            </CardContent>
                          </Card>

                          {/* Bottom save bar */}
                          <div className="flex items-center justify-end gap-2 pt-2">
                            <Button variant="outline" onClick={cancelEdit}>
                              Cancel
                            </Button>
                            <Button onClick={saveConfig} disabled={saving}>
                              <Save className="w-4 h-4 mr-2" />
                              {saving ? 'Saving...' : 'Save Configuration'}
                            </Button>
                          </div>
                        </div>
                      ) : (
                        /* ── VIEW MODE ── */
                        <div className="space-y-6">
                          {/* Configuration */}
                          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                            <Card>
                              <CardHeader>
                                <CardTitle className="text-sm">Configuration</CardTitle>
                              </CardHeader>
                              <CardContent className="text-sm">
                                <div className="space-y-1">
                                  <DetailRow label="Name" value={agent.name} />
                                  <DetailRow label="Domain" value={agent.domain} />
                                  <DetailRow label="Risk tier" value={agent.riskTier} />
                                  <DetailRow label="Status" value={agent.isActive ? 'Active' : 'Inactive'} />
                                  <DetailRow label="Override" value={agent.isOverride ? 'Yes (tenant-level)' : 'No (global default)'} />
                                  <DetailRow label="Created" value={formatDate(agent.createdAt)} />
                                  {agent.updatedAt && <DetailRow label="Updated" value={formatDate(agent.updatedAt)} />}
                                </div>
                              </CardContent>
                            </Card>

                            <Card>
                              <CardHeader>
                                <CardTitle className="text-sm">AI Model</CardTitle>
                              </CardHeader>
                              <CardContent className="text-sm">
                                {agent.modelId ? (
                                  <div className="space-y-1">
                                    <DetailRow label="Model" value={agent.modelName || 'Unknown'} />
                                    <DetailRow label="Model ID" value={agent.modelId.slice(0, 8) + '...'} />
                                  </div>
                                ) : (
                                  <div className="flex items-center gap-2 text-[var(--color-text-tertiary)]">
                                    <Brain className="w-5 h-5" />
                                    <span>Using platform default model</span>
                                  </div>
                                )}
                              </CardContent>
                            </Card>
                          </div>

                          {/* System prompt */}
                          <Card>
                            <CardHeader>
                              <CardTitle className="text-sm">System Prompt</CardTitle>
                            </CardHeader>
                            <CardContent>
                              {agent.instructionsText ? (
                                <pre className="text-xs text-[var(--color-text-secondary)] bg-[var(--color-surface-inset)] rounded-lg p-4 overflow-auto max-h-64 whitespace-pre-wrap font-mono">
                                  {agent.instructionsText}
                                </pre>
                              ) : (
                                <p className="text-sm text-[var(--color-text-tertiary)]">No system prompt configured.</p>
                              )}
                            </CardContent>
                          </Card>

                          {/* Tools */}
                          <Card>
                            <CardHeader>
                              <CardTitle className="text-sm">Tools ({tools.length})</CardTitle>
                            </CardHeader>
                            <CardContent>
                              {tools.length === 0 ? (
                                <p className="text-sm text-[var(--color-text-tertiary)]">No tools assigned.</p>
                              ) : (
                                <div className="flex flex-wrap gap-2">
                                  {tools.map((tool) => (
                                    <span
                                      key={tool}
                                      className="inline-flex items-center gap-1.5 text-xs px-2.5 py-1 rounded-md bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)] font-mono border border-[var(--color-border-light)]"
                                    >
                                      <Wrench className="w-3 h-3 text-[var(--color-text-tertiary)]" />
                                      {tool}
                                    </span>
                                  ))}
                                </div>
                              )}
                            </CardContent>
                          </Card>

                        </div>
                      )}
                    </TabsContent>

                    {/* ── MCP Servers Tab ── */}
                    <TabsContent value="mcp" className="mt-0">
                      <Card>
                        <CardHeader>
                          <CardTitle className="text-sm">MCP Servers</CardTitle>
                        </CardHeader>
                        <CardContent className="text-center py-10">
                          <Server className="w-10 h-10 mx-auto mb-3 text-[var(--color-text-tertiary)]" />
                          <p className="text-sm font-medium text-[var(--color-text-primary)] mb-1">
                            MCP Server Configuration
                          </p>
                          <p className="text-xs text-[var(--color-text-tertiary)]">
                            Configure Model Context Protocol servers that provide<br />additional tools and context to this agent. Coming soon.
                          </p>
                        </CardContent>
                      </Card>
                    </TabsContent>

                    {/* ── Conversations Tab ── */}
                    <TabsContent value="conversations" className="mt-0">
                      <Card>
                        <CardHeader>
                          <CardTitle className="text-sm">Conversations</CardTitle>
                        </CardHeader>
                        <CardContent className="text-center py-10">
                          <MessageSquare className="w-10 h-10 mx-auto mb-3 text-[var(--color-text-tertiary)]" />
                          <p className="text-sm font-medium text-[var(--color-text-primary)] mb-1">
                            Conversation History
                          </p>
                          <p className="text-xs text-[var(--color-text-tertiary)]">
                            View conversation threads and messages<br />exchanged with this agent. Coming soon.
                          </p>
                        </CardContent>
                      </Card>
                    </TabsContent>

                    {/* ── Runs Tab ── */}
                    <TabsContent value="runs" className="mt-0">
                      <div className="space-y-4">
                        <DataTable<AgentRunSummary>
                          data={runs}
                          columns={runsColumns}
                          getRowId={(row) => row.id}
                          loading={runsLoading}
                          loadingMessage="Loading agent runs..."
                          emptyIcon={<Play className="w-10 h-10 text-[var(--color-text-tertiary)]" />}
                          emptyTitle="No runs recorded yet"
                          emptyDescription="Agent execution history will appear here as the agent processes requests."
                        />
                        {runsTotalCount > runsPageSize && (
                          <DataTablePagination
                            pageNumber={runsPage}
                            pageSize={runsPageSize}
                            totalCount={runsTotalCount}
                            onPageChange={setRunsPage}
                            onPageSizeChange={() => {}}
                          />
                        )}
                      </div>
                    </TabsContent>
                  </div>
                </Tabs>
              </CardContent>
            </Card>
          </div>
        </div>
      </div>
    </div>
  );
}
