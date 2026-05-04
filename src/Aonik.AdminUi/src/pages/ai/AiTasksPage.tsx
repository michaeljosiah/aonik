import { useState, useCallback, useEffect, useRef } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
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
  ListChecks,
  Plus,
  Search,
  Pencil,
  Trash2,
  AlertCircle,
  Loader2,
  ExternalLink,
  ChevronLeft,
  ChevronRight,
  HelpCircle,
  RotateCcw,
} from 'lucide-react';
import { aiTaskService, aiRunService, aiModelService, routePolicyService } from '@/services/aiService';
import type { AiModelResponse } from '@/types/ai';
import { SelectGroup } from '@/components/ui/select';
import type {
  AiTaskResponse,
  AiTaskDetailResponse,
  AiRunSummaryResponse,
  CreateAiTaskRequest,
  UpdateAiTaskRequest,
  ListAiRunsResponse,
} from '@/services/aiService';

// ── Helpers ──────────────────────────────────────────────────────────

const getErrorMessage = (err: unknown, fallback: string) => {
  if (err && typeof err === 'object' && 'userMessage' in err) {
    const message = String((err as { userMessage?: string }).userMessage ?? '').trim();
    if (message) return message;
  }
  return fallback;
};

const categoryOptions = ['Finance', 'Conversation', 'Platform'];
const executionModeOptions = ['Realtime', 'Batch'];

const categoryColor = (cat: string) => {
  switch (cat.toLowerCase()) {
    case 'finance': return 'bg-blue-500/10 text-blue-700 border-blue-200';
    case 'platform': return 'bg-purple-500/10 text-purple-700 border-purple-200';
    case 'conversation': return 'bg-green-500/10 text-green-700 border-green-200';
    default: return 'bg-gray-500/10 text-gray-700 border-gray-200';
  }
};

const executionModeColor = (mode: string) => {
  switch (mode.toLowerCase()) {
    case 'batch': return 'bg-amber-500/10 text-amber-700 border-amber-200';
    case 'realtime': return 'bg-emerald-500/10 text-emerald-700 border-emerald-200';
    default: return '';
  }
};

const outcomeColor = (outcome: string) => {
  switch (outcome.toLowerCase()) {
    case 'completed': return 'bg-green-500/10 text-green-700 border-green-200';
    case 'failed': return 'bg-red-500/10 text-red-700 border-red-200';
    default: return 'bg-gray-500/10 text-gray-700 border-gray-200';
  }
};

function relativeTime(dateStr: string): string {
  const now = Date.now();
  const then = new Date(dateStr).getTime();
  const diffMs = now - then;
  const seconds = Math.floor(diffMs / 1000);
  if (seconds < 60) return `${seconds}s ago`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}
// ── Tooltip helper ──────────────────────────────────────────────────

function FieldLabel({ htmlFor, label, tooltip }: { htmlFor: string; label: string; tooltip: string }) {
  return (
    <div className="flex items-center gap-1.5">
      <label className="text-sm font-medium" htmlFor={htmlFor}>{label}</label>
      <TooltipProvider delayDuration={200}>
        <Tooltip>
          <TooltipTrigger asChild>
            <HelpCircle className="h-3.5 w-3.5 text-muted-foreground cursor-help" />
          </TooltipTrigger>
          <TooltipContent side="top" className="max-w-[260px]">
            <p>{tooltip}</p>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>
    </div>
  );
}

// ── Main Page ────────────────────────────────────────────────────────

export function AiTasksPage() {
  const [tasks, setTasks] = useState<AiTaskResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('All');
  const requestIdRef = useRef(0);

  // Detail dialog state
  const [selectedTask, setSelectedTask] = useState<AiTaskDetailResponse | null>(null);
  const [showDetailDialog, setShowDetailDialog] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailTab, setDetailTab] = useState('overview');

  // History tab state
  const [runs, setRuns] = useState<AiRunSummaryResponse[]>([]);
  const [runsLoading, setRunsLoading] = useState(false);
  const [runsPage, setRunsPage] = useState(1);
  const [runsTotalCount, setRunsTotalCount] = useState(0);
  const runsPageSize = 10;

  // Create/Edit dialog state
  const [showFormDialog, setShowFormDialog] = useState(false);
  const [editingTask, setEditingTask] = useState<AiTaskResponse | null>(null);
  const [saving, setSaving] = useState(false);
  const [resettingPrompt, setResettingPrompt] = useState(false);

  // Delete confirmation state
  const [deleteTarget, setDeleteTarget] = useState<AiTaskResponse | null>(null);
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [deleting, setDeleting] = useState(false);

  // Form state
  const [formUseCase, setFormUseCase] = useState('');
  const [formDisplayName, setFormDisplayName] = useState('');
  const [formDescription, setFormDescription] = useState('');
  const [formCategory, setFormCategory] = useState('Finance');
  const [formExecutionMode, setFormExecutionMode] = useState('Realtime');
  const [formPromptName, setFormPromptName] = useState('');
  const [formPromptVersion, setFormPromptVersion] = useState('v1');
  const [formSystemTemplate, setFormSystemTemplate] = useState('');
  const [formUserTemplate, setFormUserTemplate] = useState('');
  const [formVariablesSchema, setFormVariablesSchema] = useState('');
  const [formOutputSchema, setFormOutputSchema] = useState('');
  const [formIsPublished, setFormIsPublished] = useState(false);
  const [formIsActive, setFormIsActive] = useState(true);
  const [formPrimaryModelId, setFormPrimaryModelId] = useState<string | null>(null);
  const [formGlobalModelName, setFormGlobalModelName] = useState<string | null>(null);

  // Model list for dropdown
  const [availableModels, setAvailableModels] = useState<AiModelResponse[]>([]);

  useEffect(() => {
    aiModelService.list().then(setAvailableModels).catch(console.error);
  }, []);

  // ── Data Loading ────────────────────────────────────────────────────

  const loadTasks = useCallback(async () => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);
    try {
      const category = categoryFilter !== 'All' ? categoryFilter : undefined;
      const result = await aiTaskService.list(category);
      if (requestIdRef.current !== requestId) return;
      setTasks(result);
    } catch (err) {
      if (requestIdRef.current !== requestId) return;
      setError(getErrorMessage(err, 'Failed to load LLM tasks'));
    } finally {
      if (requestIdRef.current === requestId) setLoading(false);
    }
  }, [categoryFilter]);

  useEffect(() => { loadTasks(); }, [loadTasks]);

  const filteredTasks = tasks.filter((t) => {
    if (!searchQuery) return true;
    const q = searchQuery.toLowerCase();
    return t.displayName.toLowerCase().includes(q) || t.useCase.toLowerCase().includes(q);
  });

  // ── Detail Dialog ──────────────────────────────────────────────────

  const openDetail = async (task: AiTaskResponse) => {
    setDetailTab('overview');
    setShowDetailDialog(true);
    setDetailLoading(true);
    setSelectedTask(null);
    setRuns([]);
    setRunsPage(1);
    try {
      const detail = await aiTaskService.getDetail(task.id);
      setSelectedTask(detail);
    } catch (err) {
      setError(getErrorMessage(err, 'Failed to load task details'));
      setShowDetailDialog(false);
    } finally {
      setDetailLoading(false);
    }
  };

  const loadRuns = useCallback(async (useCase: string, page: number) => {
    setRunsLoading(true);
    try {
      const result: ListAiRunsResponse = await aiRunService.list({
        useCase,
        page,
        pageSize: runsPageSize,
      });
      setRuns(result.items);
      setRunsTotalCount(result.totalCount);
      setRunsPage(result.page);
    } catch {
      // Silently handle — runs are supplementary
      setRuns([]);
    } finally {
      setRunsLoading(false);
    }
  }, []);

  // Load runs when switching to history tab
  useEffect(() => {
    if (detailTab === 'history' && selectedTask) {
      loadRuns(selectedTask.useCase, runsPage);
    }
  }, [detailTab, selectedTask, runsPage, loadRuns]);

  // ── Create/Edit ────────────────────────────────────────────────────

  const resetForm = () => {
    setFormUseCase('');
    setFormDisplayName('');
    setFormDescription('');
    setFormCategory('Finance');
    setFormExecutionMode('Realtime');
    setFormPromptName('');
    setFormPromptVersion('v1');
    setFormSystemTemplate('');
    setFormUserTemplate('');
    setFormVariablesSchema('');
    setFormOutputSchema('');
    setFormIsPublished(false);
    setFormIsActive(true);
    setFormPrimaryModelId(null);
    setFormGlobalModelName(null);
  };

  const openCreate = () => {
    resetForm();
    setEditingTask(null);
    setShowFormDialog(true);
  };

  const openEdit = async (task: AiTaskResponse, e?: React.MouseEvent) => {
    e?.stopPropagation();
    setFormUseCase(task.useCase);
    setFormDisplayName(task.displayName);
    setFormDescription(task.description);
    setFormCategory(task.category);
    setFormExecutionMode(task.executionMode);
    setFormPromptName(task.promptName);
    setFormPromptVersion(task.promptVersion);
    setFormSystemTemplate(task.systemTemplate);
    setFormUserTemplate(task.userTemplate);
    setFormVariablesSchema(task.variablesSchemaJson);
    setFormOutputSchema(task.outputSchemaJson);
    setFormIsPublished(task.isPublished);
    setFormIsActive(task.isActive);
    setFormPrimaryModelId(task.primaryModelId ?? null);
    setFormGlobalModelName(null);
    setEditingTask(task);
    setShowFormDialog(true);

    // Load global default in background for hint text
    routePolicyService.list(task.useCase).then((policies) => {
      const globalPolicy = policies.find((p) => !p.isOverride);
      setFormGlobalModelName(globalPolicy?.primaryModelName ?? null);
    }).catch(() => {/* silent */});
  };

  const handleResetPrompt = async () => {
    if (!editingTask) return;
    const confirmed = window.confirm(
      'Reset this task\u2019s System and User templates back to the hard-coded defaults? Your current prompt edits will be overwritten.',
    );
    if (!confirmed) return;

    setResettingPrompt(true);
    setError(null);
    try {
      const updated = await aiTaskService.resetPrompt(editingTask.id);
      setFormSystemTemplate(updated.systemTemplate);
      setFormUserTemplate(updated.userTemplate);
      setEditingTask(updated);
    } catch (err) {
      setError(getErrorMessage(err, 'Failed to reset prompt'));
    } finally {
      setResettingPrompt(false);
    }
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      if (editingTask) {
        const request: UpdateAiTaskRequest = {
          displayName: formDisplayName,
          description: formDescription,
          category: formCategory,
          executionMode: formExecutionMode,
          promptName: formPromptName,
          promptVersion: formPromptVersion,
          systemTemplate: formSystemTemplate,
          userTemplate: formUserTemplate,
          variablesSchemaJson: formVariablesSchema || undefined,
          outputSchemaJson: formOutputSchema || undefined,
          isPublished: formIsPublished,
          isActive: formIsActive,
          primaryModelId: formPrimaryModelId || undefined,
        };
        await aiTaskService.update(editingTask.id, request);
      } else {
        const request: CreateAiTaskRequest = {
          useCase: formUseCase,
          displayName: formDisplayName,
          description: formDescription,
          category: formCategory,
          executionMode: formExecutionMode,
          promptName: formPromptName,
          promptVersion: formPromptVersion,
          systemTemplate: formSystemTemplate,
          userTemplate: formUserTemplate,
          variablesSchemaJson: formVariablesSchema || undefined,
          outputSchemaJson: formOutputSchema || undefined,
          isPublished: formIsPublished,
          isActive: formIsActive,
          primaryModelId: formPrimaryModelId || undefined,
        };
        await aiTaskService.create(request);
      }
      setShowFormDialog(false);
      loadTasks();
    } catch (err) {
      setError(getErrorMessage(err, 'Failed to save LLM task'));
    } finally {
      setSaving(false);
    }
  };

  // ── Delete ─────────────────────────────────────────────────────────

  const confirmDelete = (task: AiTaskResponse, e?: React.MouseEvent) => {
    e?.stopPropagation();
    setDeleteTarget(task);
    setShowDeleteDialog(true);
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await aiTaskService.delete(deleteTarget.id);
      setShowDeleteDialog(false);
      setDeleteTarget(null);
      loadTasks();
    } catch (err) {
      setError(getErrorMessage(err, 'Failed to delete LLM task'));
    } finally {
      setDeleting(false);
    }
  };

  // ── Render ─────────────────────────────────────────────────────────

  return (
    <div className="p-6 space-y-6">

      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">LLM Tasks</h1>
          <p className="text-muted-foreground text-sm mt-1">
            Manage non-agent LLM task configurations, prompts, and model routing.
          </p>
        </div>
        <Button onClick={openCreate}>
          <Plus className="mr-2 h-4 w-4" />
          Add Task
        </Button>
      </div>

      {/* Filters */}
      <div className="flex items-center gap-4">
        <div className="relative max-w-[24rem] flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Search by name or use case..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="pl-9"
          />
        </div>
        <Select value={categoryFilter} onValueChange={setCategoryFilter}>
          <SelectTrigger className="w-[240px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="All">All Categories</SelectItem>
            {categoryOptions.map((cat) => (
              <SelectItem key={cat} value={cat}>{cat}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Error */}
      {error && (
        <div className="flex items-center gap-2 text-destructive text-sm">
          <AlertCircle className="h-4 w-4" />
          {error}
        </div>
      )}

      {/* Card Grid */}
      {loading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : filteredTasks.length === 0 ? (
        <div className="text-center py-12 text-muted-foreground">
          <ListChecks className="h-12 w-12 mx-auto mb-4 opacity-30" />
          <p>No LLM tasks found</p>
        </div>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {filteredTasks.map((task) => (
            <Card
              key={task.id}
              className="relative group cursor-pointer hover:shadow-md transition-shadow"
              onClick={() => openDetail(task)}
            >
              <CardHeader className="pb-3">
                <div className="flex items-start justify-between">
                  <div className="space-y-1 min-w-0 flex-1">
                    <CardTitle className="text-base font-semibold truncate">{task.displayName}</CardTitle>
                    <p className="text-xs text-muted-foreground line-clamp-2">{task.description || 'No description'}</p>
                  </div>
                  <div className="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity ml-2 shrink-0">
                    <Button variant="ghost" size="icon-sm" onClick={(e) => openEdit(task, e)}>
                      <Pencil className="h-3.5 w-3.5" />
                    </Button>
                    <Button variant="ghost" size="icon-sm" onClick={(e) => confirmDelete(task, e)}>
                      <Trash2 className="h-3.5 w-3.5 text-destructive" />
                    </Button>
                  </div>
                </div>
              </CardHeader>
              <CardContent>
                <div className="flex items-center gap-2 flex-wrap mb-3">
                  <Badge className={`text-xs ${categoryColor(task.category)}`}>
                    {task.category}
                  </Badge>
                  <Badge className={`text-xs ${executionModeColor(task.executionMode)}`}>
                    {task.executionMode}
                  </Badge>
                  {task.isActive ? (
                    <Badge className="text-xs bg-green-500/10 text-green-700 border-green-200">Active</Badge>
                  ) : (
                    <Badge variant="secondary" className="text-xs">Inactive</Badge>
                  )}
                </div>
                <div className="text-sm space-y-1">
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Model</span>
                    {task.primaryModelName ? (
                      <Badge variant="outline" className="text-xs font-mono">{task.primaryModelName}</Badge>
                    ) : (
                      <span className="text-xs text-muted-foreground italic">No model assigned</span>
                    )}
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Prompt</span>
                    <span className="text-xs font-mono">{task.promptName} {task.promptVersion}</span>
                  </div>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {/* ── Detail Dialog ──────────────────────────────────────────────── */}
      <Dialog open={showDetailDialog} onOpenChange={setShowDetailDialog}>
        <DialogContent className="max-w-[750px]">
          {detailLoading || !selectedTask ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : (
            <>
              <DialogHeader>
                <DialogTitle>{selectedTask.displayName}</DialogTitle>
                <DialogDescription>{selectedTask.useCase}</DialogDescription>
              </DialogHeader>

              <Tabs value={detailTab} onValueChange={setDetailTab}>
                <TabsList>
                  <TabsTrigger value="overview">Overview</TabsTrigger>
                  <TabsTrigger value="prompt">Prompt</TabsTrigger>
                  <TabsTrigger value="history">History</TabsTrigger>
                </TabsList>

                {/* ── Overview Tab ──────────────────────────────────────── */}
                <TabsContent value="overview">
                  <div className="space-y-4 max-h-[60vh] overflow-y-auto pr-1">
                    <div className="grid grid-cols-2 gap-4">
                      <div>
                        <p className="text-xs text-muted-foreground mb-1">Display Name</p>
                        <p className="text-sm font-medium">{selectedTask.displayName}</p>
                      </div>
                      <div>
                        <p className="text-xs text-muted-foreground mb-1">Category</p>
                        <Badge className={`text-xs ${categoryColor(selectedTask.category)}`}>{selectedTask.category}</Badge>
                      </div>
                      <div>
                        <p className="text-xs text-muted-foreground mb-1">Execution Mode</p>
                        <Badge className={`text-xs ${executionModeColor(selectedTask.executionMode)}`}>{selectedTask.executionMode}</Badge>
                      </div>
                      <div>
                        <p className="text-xs text-muted-foreground mb-1">Model</p>
                        <p className="text-sm">{selectedTask.primaryModelName ?? 'Not configured'}</p>
                      </div>
                    </div>

                    {selectedTask.description && (
                      <div>
                        <p className="text-xs text-muted-foreground mb-1">Description</p>
                        <p className="text-sm">{selectedTask.description}</p>
                      </div>
                    )}

                    {/* Route policy info */}
                    {(selectedTask.routePolicyRiskTier || selectedTask.routePolicyDataSensitivity) && (
                      <div className="grid grid-cols-2 gap-4">
                        {selectedTask.routePolicyRiskTier && (
                          <div>
                            <p className="text-xs text-muted-foreground mb-1">Risk Tier</p>
                            <p className="text-sm">{selectedTask.routePolicyRiskTier}</p>
                          </div>
                        )}
                        {selectedTask.routePolicyDataSensitivity && (
                          <div>
                            <p className="text-xs text-muted-foreground mb-1">Data Sensitivity</p>
                            <p className="text-sm">{selectedTask.routePolicyDataSensitivity}</p>
                          </div>
                        )}
                      </div>
                    )}

                    {/* Stats */}
                    <div>
                      <p className="text-xs text-muted-foreground mb-2 font-medium uppercase tracking-wider">Statistics</p>
                      <div className="grid grid-cols-3 gap-3">
                        <div className="rounded-lg border p-3">
                          <p className="text-xs text-muted-foreground">Total Runs</p>
                          <p className="text-lg font-semibold">{selectedTask.stats.totalRuns.toLocaleString()}</p>
                        </div>
                        <div className="rounded-lg border p-3">
                          <p className="text-xs text-muted-foreground">Last 24h</p>
                          <p className="text-lg font-semibold">{selectedTask.stats.last24hRuns.toLocaleString()}</p>
                        </div>
                        <div className="rounded-lg border p-3">
                          <p className="text-xs text-muted-foreground">Success Rate</p>
                          <p className="text-lg font-semibold">{(selectedTask.stats.successRate * 100).toFixed(1)}%</p>
                        </div>
                        <div className="rounded-lg border p-3">
                          <p className="text-xs text-muted-foreground">Avg Latency</p>
                          <p className="text-lg font-semibold">{selectedTask.stats.avgLatencyMs.toFixed(0)}ms</p>
                        </div>
                        <div className="rounded-lg border p-3">
                          <p className="text-xs text-muted-foreground">Avg Cost</p>
                          <p className="text-lg font-semibold">${selectedTask.stats.avgCost.toFixed(4)}</p>
                        </div>
                        <div className="rounded-lg border p-3">
                          <p className="text-xs text-muted-foreground">Last Run</p>
                          <p className="text-sm font-medium">{selectedTask.stats.lastRunAt ? relativeTime(selectedTask.stats.lastRunAt) : 'Never'}</p>
                        </div>
                      </div>
                    </div>

                    <Button variant="outline" size="sm" asChild>
                      <a href={`/ai/playground?taskId=${selectedTask.id}`}>
                        <ExternalLink className="mr-2 h-3.5 w-3.5" />
                        Test in Playground
                      </a>
                    </Button>
                  </div>
                </TabsContent>

                {/* ── Prompt Tab ────────────────────────────────────────── */}
                <TabsContent value="prompt">
                  <div className="space-y-4 max-h-[60vh] overflow-y-auto pr-1">
                    <div>
                      <p className="text-xs text-muted-foreground mb-1 font-medium">System Template</p>
                      <pre className="bg-muted rounded-md p-3 text-xs font-mono whitespace-pre-wrap max-h-48 overflow-y-auto">
                        {selectedTask.systemTemplate || '(empty)'}
                      </pre>
                    </div>
                    <div>
                      <p className="text-xs text-muted-foreground mb-1 font-medium">User Template</p>
                      <pre className="bg-muted rounded-md p-3 text-xs font-mono whitespace-pre-wrap max-h-48 overflow-y-auto">
                        {selectedTask.userTemplate || '(empty)'}
                      </pre>
                    </div>
                    {selectedTask.variablesSchemaJson && (
                      <div>
                        <p className="text-xs text-muted-foreground mb-1 font-medium">Variables Schema</p>
                        <pre className="bg-muted rounded-md p-3 text-xs font-mono whitespace-pre-wrap max-h-32 overflow-y-auto">
                          {selectedTask.variablesSchemaJson}
                        </pre>
                      </div>
                    )}
                    {selectedTask.outputSchemaJson && (
                      <div>
                        <p className="text-xs text-muted-foreground mb-1 font-medium">Output Schema</p>
                        <pre className="bg-muted rounded-md p-3 text-xs font-mono whitespace-pre-wrap max-h-32 overflow-y-auto">
                          {selectedTask.outputSchemaJson}
                        </pre>
                      </div>
                    )}
                  </div>
                </TabsContent>

                {/* ── History Tab ───────────────────────────────────────── */}
                <TabsContent value="history">
                  <div className="space-y-3 max-h-[60vh] overflow-y-auto pr-1">
                    {runsLoading ? (
                      <div className="flex items-center justify-center py-8">
                        <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                      </div>
                    ) : runs.length === 0 ? (
                      <p className="text-center py-8 text-sm text-muted-foreground">No run history found</p>
                    ) : (
                      <>
                        <div className="rounded-md border">
                          <table className="w-full text-sm">
                            <thead>
                              <tr className="border-b bg-muted/50">
                                <th className="px-3 py-2 text-left font-medium text-muted-foreground">Time</th>
                                <th className="px-3 py-2 text-left font-medium text-muted-foreground">Model</th>
                                <th className="px-3 py-2 text-right font-medium text-muted-foreground">Tokens</th>
                                <th className="px-3 py-2 text-right font-medium text-muted-foreground">Cost</th>
                                <th className="px-3 py-2 text-right font-medium text-muted-foreground">Latency</th>
                                <th className="px-3 py-2 text-left font-medium text-muted-foreground">Outcome</th>
                              </tr>
                            </thead>
                            <tbody>
                              {runs.map((run) => (
                                <tr key={run.id} className="border-b last:border-0">
                                  <td className="px-3 py-2 text-muted-foreground">{relativeTime(run.createdAt)}</td>
                                  <td className="px-3 py-2 font-mono text-xs">{run.modelName ?? '-'}</td>
                                  <td className="px-3 py-2 text-right">{run.tokensUsed.toLocaleString()}</td>
                                  <td className="px-3 py-2 text-right">${run.costEstimate.toFixed(4)}</td>
                                  <td className="px-3 py-2 text-right">{run.latencyMs}ms</td>
                                  <td className="px-3 py-2">
                                    <Badge className={`text-xs ${outcomeColor(run.outcome)}`}>{run.outcome}</Badge>
                                  </td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>

                        {/* Pagination */}
                        <div className="flex items-center justify-between">
                          <p className="text-xs text-muted-foreground">
                            {runsTotalCount} total run{runsTotalCount !== 1 ? 's' : ''}
                          </p>
                          <div className="flex items-center gap-2">
                            <Button
                              variant="outline"
                              size="sm"
                              disabled={runsPage <= 1}
                              onClick={() => setRunsPage((p) => p - 1)}
                            >
                              <ChevronLeft className="h-4 w-4" />
                              Previous
                            </Button>
                            <span className="text-sm text-muted-foreground">
                              Page {runsPage} of {Math.max(1, Math.ceil(runsTotalCount / runsPageSize))}
                            </span>
                            <Button
                              variant="outline"
                              size="sm"
                              disabled={runsPage >= Math.ceil(runsTotalCount / runsPageSize)}
                              onClick={() => setRunsPage((p) => p + 1)}
                            >
                              Next
                              <ChevronRight className="h-4 w-4" />
                            </Button>
                          </div>
                        </div>
                      </>
                    )}
                  </div>
                </TabsContent>
              </Tabs>
            </>
          )}
        </DialogContent>
      </Dialog>

      {/* ── Create/Edit Dialog ─────────────────────────────────────────── */}
      <Dialog open={showFormDialog} onOpenChange={setShowFormDialog}>
        <DialogContent className="max-w-[800px]">
          <DialogHeader>
            <DialogTitle>{editingTask ? `Edit: ${editingTask.displayName}` : 'Add LLM Task'}</DialogTitle>
            <DialogDescription>
              {editingTask ? 'Update the LLM task configuration.' : 'Create a new LLM task configuration.'}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2 max-h-[70vh] overflow-y-auto pr-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <FieldLabel
                  htmlFor="task-use-case"
                  label="Use Case"
                  tooltip="A unique identifier for this task, used for model routing and prompt resolution. Use kebab-case, e.g. 'transaction-classification'."
                />
                <Input
                  id="task-use-case"
                  value={formUseCase}
                  onChange={(e) => setFormUseCase(e.target.value)}
                  placeholder="e.g. transaction-classification"
                  disabled={!!editingTask}
                />
              </div>
              <div className="space-y-2">
                <FieldLabel
                  htmlFor="task-display-name"
                  label="Display Name"
                  tooltip="A human-readable name shown in the admin UI and task listings."
                />
                <Input
                  id="task-display-name"
                  value={formDisplayName}
                  onChange={(e) => setFormDisplayName(e.target.value)}
                  placeholder="e.g. Transaction Classification"
                />
              </div>
            </div>

            <div className="space-y-2">
              <FieldLabel
                htmlFor="task-description"
                label="Description"
                tooltip="A brief explanation of what this LLM task does and when it runs."
              />
              <Textarea
                id="task-description"
                value={formDescription}
                onChange={(e) => setFormDescription(e.target.value)}
                placeholder="Brief description of this task..."
                rows={2}
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <FieldLabel
                  htmlFor="task-category"
                  label="Category"
                  tooltip="Groups related tasks together. Finance = financial analysis tasks, Conversation = chat/dialogue tasks, Platform = system-level tasks."
                />
                <Select value={formCategory} onValueChange={setFormCategory}>
                  <SelectTrigger id="task-category">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {categoryOptions.map((cat) => (
                      <SelectItem key={cat} value={cat}>{cat}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <FieldLabel
                  htmlFor="task-exec-mode"
                  label="Execution Mode"
                  tooltip="Realtime = runs during a user request with low-latency requirements. Batch = runs asynchronously in background jobs."
                />
                <Select value={formExecutionMode} onValueChange={setFormExecutionMode}>
                  <SelectTrigger id="task-exec-mode">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {executionModeOptions.map((mode) => (
                      <SelectItem key={mode} value={mode}>{mode}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="space-y-2">
              <FieldLabel
                htmlFor="task-model"
                label="LLM Model"
                tooltip="Your tenant's model override for this task. Selecting a model creates a tenant-scoped route policy. Leave unset to use the global default."
              />
              <Select
                value={formPrimaryModelId ?? '__none__'}
                onValueChange={(v) => setFormPrimaryModelId(v === '__none__' ? null : v)}
              >
                <SelectTrigger id="task-model">
                  <SelectValue placeholder={formGlobalModelName ? `Default: ${formGlobalModelName}` : 'No default set'} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__none__">
                    {formGlobalModelName ? `Use global default (${formGlobalModelName})` : 'No model assigned'}
                  </SelectItem>
                  {(() => {
                    const activeModels = availableModels.filter((m) => m.isActive);
                    const grouped = activeModels.reduce<Record<string, AiModelResponse[]>>((acc, m) => {
                      const provider = m.providerName ?? 'Unknown';
                      if (!acc[provider]) acc[provider] = [];
                      acc[provider].push(m);
                      return acc;
                    }, {});
                    return Object.entries(grouped).map(([provider, providerModels]) => (
                      <SelectGroup key={provider}>
                        <div className="px-2 py-1.5 text-[11px] font-semibold tracking-wider text-[var(--color-text-tertiary)]">
                          {provider}
                        </div>
                        {providerModels.map((m) => (
                          <SelectItem key={m.id} value={m.id}>
                            {m.modelName}
                          </SelectItem>
                        ))}
                      </SelectGroup>
                    ));
                  })()}
                </SelectContent>
              </Select>
              {formPrimaryModelId ? (
                <p className="text-xs text-[var(--color-text-tertiary)]">
                  Tenant override — overrides the global default for this tenant.
                </p>
              ) : formGlobalModelName ? (
                <p className="text-xs text-[var(--color-text-tertiary)]">
                  Using global default: <span className="font-medium">{formGlobalModelName}</span>. Select a model above to override for this tenant.
                </p>
              ) : null}
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <FieldLabel
                  htmlFor="task-prompt-name"
                  label="Prompt Name"
                  tooltip="Internal key used by the prompt resolver to look up this task's templates. Typically matches the use case in snake_case."
                />
                <Input
                  id="task-prompt-name"
                  value={formPromptName}
                  onChange={(e) => setFormPromptName(e.target.value)}
                  placeholder="e.g. transaction_classification"
                />
              </div>
              <div className="space-y-2">
                <FieldLabel
                  htmlFor="task-prompt-version"
                  label="Prompt Version"
                  tooltip="Version tag for the prompt template, e.g. 'v1', 'v2'. Used for tracking prompt iterations."
                />
                <Input
                  id="task-prompt-version"
                  value={formPromptVersion}
                  onChange={(e) => setFormPromptVersion(e.target.value)}
                  placeholder="e.g. v1"
                />
              </div>
            </div>

            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <FieldLabel
                  htmlFor="task-system-template"
                  label="System Template"
                  tooltip="The system prompt sent to the LLM. Sets the AI's role, rules, and output format. Supports {{variable}} placeholders."
                />
                {editingTask && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={handleResetPrompt}
                    disabled={resettingPrompt || saving}
                    className="gap-1.5 text-xs h-7 text-muted-foreground"
                    title="Reset System and User templates back to the hard-coded defaults for this task"
                  >
                    {resettingPrompt ? (
                      <Loader2 className="w-3.5 h-3.5 animate-spin" />
                    ) : (
                      <RotateCcw className="w-3.5 h-3.5" />
                    )}
                    Reset to default
                  </Button>
                )}
              </div>
              <Textarea
                id="task-system-template"
                value={formSystemTemplate}
                onChange={(e) => setFormSystemTemplate(e.target.value)}
                placeholder="System prompt content..."
                rows={6}
                className="font-mono text-sm"
              />
            </div>

            <div className="space-y-2">
              <FieldLabel
                htmlFor="task-user-template"
                label="User Template"
                tooltip="The user message template sent to the LLM. Use {{variable}} placeholders for dynamic content that gets substituted at runtime."
              />
              <Textarea
                id="task-user-template"
                value={formUserTemplate}
                onChange={(e) => setFormUserTemplate(e.target.value)}
                placeholder="User prompt template with {{PLACEHOLDER}} variables..."
                rows={4}
                className="font-mono text-sm"
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <FieldLabel
                  htmlFor="task-variables-schema"
                  label="Variables Schema (JSON)"
                  tooltip="A JSON schema describing the template variables. Defines their types and descriptions for the playground variables form."
                />
                <Textarea
                  id="task-variables-schema"
                  value={formVariablesSchema}
                  onChange={(e) => setFormVariablesSchema(e.target.value)}
                  placeholder='{"properties": {"name": {"type": "string"}}}'
                  rows={3}
                  className="font-mono text-sm"
                />
              </div>
              <div className="space-y-2">
                <FieldLabel
                  htmlFor="task-output-schema"
                  label="Output Schema (JSON)"
                  tooltip="A JSON schema for validating the LLM's structured output. Used when the task expects a specific JSON response format."
                />
                <Textarea
                  id="task-output-schema"
                  value={formOutputSchema}
                  onChange={(e) => setFormOutputSchema(e.target.value)}
                  placeholder='{"type": "object", "properties": {...}}'
                  rows={3}
                  className="font-mono text-sm"
                />
              </div>
            </div>

            <div className="flex items-center gap-6">
              <div className="flex items-center gap-2">
                <Switch
                  id="task-published"
                  checked={formIsPublished}
                  onCheckedChange={setFormIsPublished}
                />
                <TooltipProvider delayDuration={200}>
                  <Tooltip>
                    <TooltipTrigger asChild>
                      <label className="text-sm font-medium cursor-help" htmlFor="task-published">Published</label>
                    </TooltipTrigger>
                    <TooltipContent side="top" className="max-w-[260px]">
                      <p>When published, this task's prompts are used by the runtime resolver. Unpublished tasks are hidden from production.</p>
                    </TooltipContent>
                  </Tooltip>
                </TooltipProvider>
              </div>
              <div className="flex items-center gap-2">
                <Switch
                  id="task-active"
                  checked={formIsActive}
                  onCheckedChange={setFormIsActive}
                />
                <TooltipProvider delayDuration={200}>
                  <Tooltip>
                    <TooltipTrigger asChild>
                      <label className="text-sm font-medium cursor-help" htmlFor="task-active">Active</label>
                    </TooltipTrigger>
                    <TooltipContent side="top" className="max-w-[260px]">
                      <p>Controls whether this task is enabled. Inactive tasks are skipped during execution but remain configured.</p>
                    </TooltipContent>
                  </Tooltip>
                </TooltipProvider>
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowFormDialog(false)}>Cancel</Button>
            <Button onClick={handleSave} disabled={saving || (!editingTask && (!formUseCase || !formDisplayName || !formPromptName))}>
              {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {editingTask ? 'Save Changes' : 'Create'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Delete Confirmation Dialog ─────────────────────────────────── */}
      <Dialog open={showDeleteDialog} onOpenChange={setShowDeleteDialog}>
        <DialogContent className="max-w-[450px]">
          <DialogHeader>
            <DialogTitle>Delete LLM Task</DialogTitle>
            <DialogDescription>
              Are you sure you want to delete <strong>{deleteTarget?.displayName}</strong>? This action cannot be undone.
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
