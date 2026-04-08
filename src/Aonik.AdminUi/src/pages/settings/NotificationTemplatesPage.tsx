import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  Bell,
  ChevronDown,
  ChevronRight,
  Eye,
  Loader2,
  Mail,
  MessageSquare,
  Pencil,
  Plus,
  Search,
  Smartphone,
  Sparkles,
  Trash2,
} from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { DataTable, type ColumnDef, DataTableRowActions } from '@/components/ui/data-table';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogFooter,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/dialog';
import { notificationTemplateService } from '@/services/notificationTemplateService';
import { api } from '@/lib/api';
import type {
  NotificationTemplateSummary,
  NotificationTemplateResponse,
  NotificationTemplateBindingResponse,
} from '@/types';

// ═══════════════════════════════════════════════════════════════════════════════
// Constants & helpers
// ═══════════════════════════════════════════════════════════════════════════════
const CHANNELS = ['Email', 'SMS', 'Push'] as const;

const channelMeta: Record<string, { icon: typeof Mail; label: string; color: string }> = {
  Email: { icon: Mail, label: 'Email', color: 'bg-blue-100 text-blue-800 dark:bg-blue-900/40 dark:text-blue-300' },
  SMS: { icon: Smartphone, label: 'SMS', color: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300' },
  Push: { icon: MessageSquare, label: 'Push', color: 'bg-purple-100 text-purple-800 dark:bg-purple-900/40 dark:text-purple-300' },
};

function ChannelBadge({ channel }: { channel: string }) {
  const meta = channelMeta[channel] ?? channelMeta.Email;
  const Icon = meta.icon;
  return (
    <Badge className={`${meta.color} gap-1 font-medium`}>
      <Icon className="h-3 w-3" />
      {meta.label}
    </Badge>
  );
}

// ═══════════════════════════════════════════════════════════════════════════════
// Types
// ═══════════════════════════════════════════════════════════════════════════════
interface TemplateForm {
  name: string;
  channel: string;
  subjectTemplate: string;
  bodyTemplate: string;
  description: string;
  isShared: boolean;
  isActive: boolean;
}

const emptyForm: TemplateForm = {
  name: '',
  channel: 'Email',
  subjectTemplate: '',
  bodyTemplate: '',
  description: '',
  isShared: false,
  isActive: true,
};

interface BindingForm {
  templateName: string;
  channel: string;
  baseTemplateId: string;
  overrideTemplateId: string;
  isEnabled: boolean;
}

const emptyBindingForm: BindingForm = {
  templateName: '',
  channel: 'Email',
  baseTemplateId: '',
  overrideTemplateId: '',
  isEnabled: true,
};

// ═══════════════════════════════════════════════════════════════════════════════
// Page
// ═══════════════════════════════════════════════════════════════════════════════
export function NotificationTemplatesPage() {
  const [activeTab, setActiveTab] = useState('templates');

  // ── Template data ──────────────────────────────────────────────────────
  const [templates, setTemplates] = useState<NotificationTemplateSummary[]>([]);
  const [loadingTemplates, setLoadingTemplates] = useState(true);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [selectedDetail, setSelectedDetail] = useState<NotificationTemplateResponse | null>(null);
  const [loadingDetail, setLoadingDetail] = useState(false);

  // ── Editor state ──────────────────────────────────────────────────────
  const [isCreating, setIsCreating] = useState(false);
  const [form, setForm] = useState<TemplateForm>(emptyForm);
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [collapsedChannels, setCollapsedChannels] = useState<Record<string, boolean>>({});

  // ── Preview state ──────────────────────────────────────────────────────
  const [previewOpen, setPreviewOpen] = useState(false);
  const [sampleJson, setSampleJson] = useState('{\n  "first_name": "Amara",\n  "tenant_name": "Payabo",\n  "otp_code": "482910",\n  "confirmation_url": "https://app.payabo.com/confirm?token=abc123",\n  "expiry_hours": 24,\n  "expiry_minutes": 10\n}');
  const [previewResult, setPreviewResult] = useState<{ subject: string; body: string } | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const previewTimerRef = useRef<ReturnType<typeof setTimeout>>(undefined);

  // ── AI generation ──────────────────────────────────────────────────────
  const [generatingDescription, setGeneratingDescription] = useState(false);

  // ── Binding state ──────────────────────────────────────────────────────
  const [bindings, setBindings] = useState<NotificationTemplateBindingResponse[]>([]);
  const [loadingBindings, setLoadingBindings] = useState(true);
  const [bindingDialogOpen, setBindingDialogOpen] = useState(false);
  const [editingBindingId, setEditingBindingId] = useState<string | null>(null);
  const [bindingForm, setBindingForm] = useState<BindingForm>(emptyBindingForm);
  const [savingBinding, setSavingBinding] = useState(false);

  // ── Load templates ─────────────────────────────────────────────────────
  const loadTemplates = useCallback(async () => {
    try {
      setLoadingTemplates(true);
      const data = await notificationTemplateService.list();
      setTemplates(data);
    } catch {
      toast.error('Failed to load notification templates');
    } finally {
      setLoadingTemplates(false);
    }
  }, []);

  const loadBindings = useCallback(async () => {
    try {
      setLoadingBindings(true);
      const data = await notificationTemplateService.listBindings();
      setBindings(data);
    } catch {
      toast.error('Failed to load template bindings');
    } finally {
      setLoadingBindings(false);
    }
  }, []);

  useEffect(() => {
    loadTemplates();
    loadBindings();
  }, [loadTemplates, loadBindings]);

  // ── Select template ────────────────────────────────────────────────────
  const selectTemplate = useCallback(async (id: string) => {
    if (dirty) {
      const ok = window.confirm('You have unsaved changes. Discard them?');
      if (!ok) return;
    }
    try {
      setLoadingDetail(true);
      setIsCreating(false);
      setSelectedId(id);
      const detail = await notificationTemplateService.get(id);
      setSelectedDetail(detail);
      setForm({
        name: detail.name,
        channel: detail.channel,
        subjectTemplate: detail.subjectTemplate,
        bodyTemplate: detail.bodyTemplate,
        description: detail.description,
        isShared: detail.isShared,
        isActive: detail.isActive,
      });
      setDirty(false);
      setPreviewResult(null);
    } catch {
      toast.error('Failed to load template');
    } finally {
      setLoadingDetail(false);
    }
  }, [dirty]);

  // ── Create new ─────────────────────────────────────────────────────────
  function startCreate() {
    if (dirty) {
      const ok = window.confirm('You have unsaved changes. Discard them?');
      if (!ok) return;
    }
    setSelectedId(null);
    setSelectedDetail(null);
    setIsCreating(true);
    setForm(emptyForm);
    setDirty(false);
    setPreviewResult(null);
  }

  // ── Form update helper ─────────────────────────────────────────────────
  function updateForm(patch: Partial<TemplateForm>) {
    setForm((prev) => ({ ...prev, ...patch }));
    setDirty(true);
  }

  // ── Save ───────────────────────────────────────────────────────────────
  async function save() {
    try {
      setSaving(true);
      if (isCreating) {
        const created = await notificationTemplateService.create({
          name: form.name,
          channel: form.channel,
          subjectTemplate: form.subjectTemplate,
          bodyTemplate: form.bodyTemplate,
          description: form.description,
          isShared: form.isShared,
          isActive: form.isActive,
        });
        toast.success('Template created');
        await loadTemplates();
        setIsCreating(false);
        setSelectedId(created.id);
        setSelectedDetail(created);
        setDirty(false);
      } else if (selectedId) {
        const updated = await notificationTemplateService.update(selectedId, {
          subjectTemplate: form.subjectTemplate,
          bodyTemplate: form.bodyTemplate,
          description: form.description,
          isShared: form.isShared,
          isActive: form.isActive,
        });
        toast.success('Template saved');
        setSelectedDetail(updated);
        setDirty(false);
        await loadTemplates();
      }
    } catch {
      toast.error('Failed to save template');
    } finally {
      setSaving(false);
    }
  }

  // ── Delete ─────────────────────────────────────────────────────────────
  async function deleteSelected() {
    if (!selectedId) return;
    const ok = window.confirm(`Delete "${form.name}"? This cannot be undone.`);
    if (!ok) return;
    try {
      await notificationTemplateService.delete(selectedId);
      toast.success('Template deleted');
      setSelectedId(null);
      setSelectedDetail(null);
      setIsCreating(false);
      setForm(emptyForm);
      setDirty(false);
      await loadTemplates();
    } catch {
      toast.error('Failed to delete template');
    }
  }

  // ── Preview ────────────────────────────────────────────────────────────
  async function runPreview() {
    try {
      setPreviewing(true);
      const result = await notificationTemplateService.preview({
        subjectTemplate: form.subjectTemplate,
        bodyTemplate: form.bodyTemplate,
        sampleModelJson: sampleJson,
      });
      setPreviewResult(result);
    } catch {
      toast.error('Preview failed — check template syntax and JSON');
    } finally {
      setPreviewing(false);
    }
  }

  // Auto-preview on changes (debounced)
  useEffect(() => {
    if (!previewOpen || !form.bodyTemplate) return;
    if (previewTimerRef.current) clearTimeout(previewTimerRef.current);
    previewTimerRef.current = setTimeout(() => {
      runPreview();
    }, 800);
    return () => {
      if (previewTimerRef.current) clearTimeout(previewTimerRef.current);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [form.subjectTemplate, form.bodyTemplate, sampleJson, previewOpen]);

  // ── AI description ─────────────────────────────────────────────────────
  async function generateDescription() {
    if (!form.name && !form.bodyTemplate) {
      toast.error('Enter a template name or body first');
      return;
    }
    try {
      setGeneratingDescription(true);
      const prompt = [
        'Generate a short, clear description (1-2 sentences) for a notification template.',
        `Template name: ${form.name || '(not set)'}`,
        `Channel: ${form.channel}`,
        form.bodyTemplate ? `Body template:\n${form.bodyTemplate}` : null,
        'Reply with ONLY the description text, no quotes or extra formatting.',
      ].filter(Boolean).join('\n');
      const response = await api.post<{ message: string }>('/ai/chat', { message: prompt });
      if (response.message) {
        updateForm({ description: response.message.trim() });
        toast.success('Description generated');
      }
    } catch {
      toast.error('Failed to generate description');
    } finally {
      setGeneratingDescription(false);
    }
  }

  // ── Grouped template list ──────────────────────────────────────────────
  const filteredTemplates = useMemo(() => {
    if (!searchQuery.trim()) return templates;
    const q = searchQuery.toLowerCase();
    return templates.filter(
      (t) => t.name.toLowerCase().includes(q) || t.description?.toLowerCase().includes(q)
    );
  }, [templates, searchQuery]);

  const groupedTemplates = useMemo(() => {
    const groups: Record<string, NotificationTemplateSummary[]> = {};
    for (const ch of CHANNELS) groups[ch] = [];
    for (const t of filteredTemplates) {
      const ch = t.channel ?? 'Email';
      if (!groups[ch]) groups[ch] = [];
      groups[ch].push(t);
    }
    return groups;
  }, [filteredTemplates]);

  function toggleChannel(ch: string) {
    setCollapsedChannels((prev) => ({ ...prev, [ch]: !prev[ch] }));
  }

  // ── Has editor open ────────────────────────────────────────────────────
  const editorActive = isCreating || selectedId !== null;

  // ── Binding handlers ───────────────────────────────────────────────────
  function openCreateBinding() {
    setEditingBindingId(null);
    setBindingForm(emptyBindingForm);
    setBindingDialogOpen(true);
  }

  function openEditBinding(binding: NotificationTemplateBindingResponse) {
    setEditingBindingId(binding.id);
    setBindingForm({
      templateName: binding.templateName,
      channel: binding.channel,
      baseTemplateId: binding.baseTemplateId ?? '',
      overrideTemplateId: binding.overrideTemplateId ?? '',
      isEnabled: binding.isEnabled,
    });
    setBindingDialogOpen(true);
  }

  async function saveBinding() {
    try {
      setSavingBinding(true);
      if (editingBindingId) {
        await notificationTemplateService.updateBinding(editingBindingId, {
          baseTemplateId: bindingForm.baseTemplateId || null,
          overrideTemplateId: bindingForm.overrideTemplateId || null,
          isEnabled: bindingForm.isEnabled,
        });
        toast.success('Binding updated');
      } else {
        await notificationTemplateService.createBinding({
          templateName: bindingForm.templateName,
          channel: bindingForm.channel,
          baseTemplateId: bindingForm.baseTemplateId || null,
          overrideTemplateId: bindingForm.overrideTemplateId || null,
          isEnabled: bindingForm.isEnabled,
        });
        toast.success('Binding created');
      }
      setBindingDialogOpen(false);
      await loadBindings();
    } catch {
      toast.error('Failed to save binding');
    } finally {
      setSavingBinding(false);
    }
  }

  async function deleteBinding(id: string) {
    try {
      await notificationTemplateService.deleteBinding(id);
      toast.success('Binding deleted');
      await loadBindings();
    } catch {
      toast.error('Failed to delete binding');
    }
  }

  // Template name lookup for bindings
  const templateNameMap = useMemo(() => {
    const map = new Map<string, string>();
    for (const t of templates) map.set(t.id, `${t.name} (${t.channel})`);
    return map;
  }, [templates]);

  // ── Binding columns ────────────────────────────────────────────────────
  const bindingColumns = useMemo<ColumnDef<NotificationTemplateBindingResponse>[]>(
    () => [
      { id: 'templateName', accessorKey: 'templateName', header: 'Template Name' },
      {
        id: 'channel',
        accessorKey: 'channel',
        header: 'Channel',
        cell: (row) => <ChannelBadge channel={row.channel} />,
      },
      {
        id: 'baseTemplateId',
        accessorKey: 'baseTemplateId',
        header: 'Base Template',
        cell: (row) => row.baseTemplateId
          ? <span className="text-sm">{templateNameMap.get(row.baseTemplateId) ?? row.baseTemplateId.slice(0, 8) + '...'}</span>
          : <span className="text-[var(--color-text-tertiary)]">None</span>,
      },
      {
        id: 'overrideTemplateId',
        accessorKey: 'overrideTemplateId',
        header: 'Override',
        cell: (row) => row.overrideTemplateId
          ? <span className="text-sm">{templateNameMap.get(row.overrideTemplateId) ?? row.overrideTemplateId.slice(0, 8) + '...'}</span>
          : <span className="text-[var(--color-text-tertiary)]">None</span>,
      },
      {
        id: 'isEnabled',
        accessorKey: 'isEnabled',
        header: 'Status',
        cell: (row) => (
          <Badge variant={row.isEnabled ? 'success' : 'secondary'}>
            {row.isEnabled ? 'Enabled' : 'Disabled'}
          </Badge>
        ),
      },
      {
        id: 'actions',
        header: '',
        cell: (row) => (
          <DataTableRowActions
            actions={[
              { label: 'Edit', icon: <Pencil className="w-4 h-4" />, onClick: () => openEditBinding(row) },
              { label: 'Delete', icon: <Trash2 className="w-4 h-4" />, onClick: () => deleteBinding(row.id), variant: 'danger' },
            ]}
          />
        ),
      },
    ],
    [templateNameMap],
  );

  // ═══════════════════════════════════════════════════════════════════════
  // Render
  // ═══════════════════════════════════════════════════════════════════════
  return (
    <div className="h-full flex flex-col overflow-hidden">
      {/* Header */}
      <div className="flex items-center justify-between px-6 py-4 border-b border-[var(--color-border)]">
        <div className="flex items-center gap-3">
          <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-[var(--color-brand-primary-light)]">
            <Bell className="h-4.5 w-4.5 text-[var(--color-brand-primary)]" />
          </div>
          <div>
            <h1 className="text-lg font-semibold text-[var(--color-text-primary)]">Notification Templates</h1>
            <p className="text-xs text-[var(--color-text-secondary)]">
              Manage email, SMS, and push notification templates with Scriban syntax
            </p>
          </div>
        </div>
        <Tabs value={activeTab} onValueChange={setActiveTab}>
          <TabsList className="h-8">
            <TabsTrigger value="templates" className="text-xs px-3 h-7">Templates</TabsTrigger>
            <TabsTrigger value="bindings" className="text-xs px-3 h-7">Bindings</TabsTrigger>
          </TabsList>
        </Tabs>
      </div>

      {/* ── Templates Tab ──────────────────────────────────────────────── */}
      {activeTab === 'templates' && (
        <div className="flex-1 flex overflow-hidden">
          {/* ─── Left sidebar: template list ───────────────────────────── */}
          <div className="w-64 flex-shrink-0 border-r border-[var(--color-border)] flex flex-col bg-[var(--color-surface-inset)]">
            <div className="p-3 space-y-2">
              <Button size="sm" className="w-full rounded-sm" onClick={startCreate}>
                <Plus className="mr-1.5 h-3.5 w-3.5" />
                New Template
              </Button>
              <div className="relative">
                <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-[var(--color-text-tertiary)]" />
                <Input
                  placeholder="Search templates..."
                  className="h-8 pl-8 text-xs"
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                />
              </div>
            </div>

            <div className="flex-1 overflow-y-auto px-2 pb-3">
              {loadingTemplates ? (
                <div className="flex items-center justify-center py-10">
                  <Loader2 className="h-5 w-5 animate-spin text-[var(--color-text-tertiary)]" />
                </div>
              ) : (
                CHANNELS.map((ch) => {
                  const items = groupedTemplates[ch] ?? [];
                  if (items.length === 0 && searchQuery) return null;
                  const isCollapsed = collapsedChannels[ch];
                  const meta = channelMeta[ch];
                  const Icon = meta.icon;

                  return (
                    <div key={ch} className="mb-1">
                      <button
                        onClick={() => toggleChannel(ch)}
                        className="flex items-center gap-1.5 w-full px-2 py-1.5 text-xs font-medium text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)] rounded transition-colors"
                      >
                        {isCollapsed ? (
                          <ChevronRight className="h-3 w-3" />
                        ) : (
                          <ChevronDown className="h-3 w-3" />
                        )}
                        <Icon className="h-3 w-3" />
                        {ch}
                        <span className="ml-auto text-[var(--color-text-tertiary)]">{items.length}</span>
                      </button>

                      {!isCollapsed && (
                        <div className="ml-3 space-y-0.5">
                          {items.length === 0 ? (
                            <p className="text-xs text-[var(--color-text-tertiary)] px-2 py-1 italic">No templates</p>
                          ) : (
                            items.map((t) => (
                              <button
                                key={t.id}
                                onClick={() => selectTemplate(t.id)}
                                className={`w-full text-left px-2.5 py-1.5 rounded-sm text-xs transition-colors ${
                                  selectedId === t.id
                                    ? 'bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)] font-medium'
                                    : 'text-[var(--color-text-primary)] hover:bg-[var(--color-gray-100)]'
                                }`}
                              >
                                <div className="flex items-center justify-between gap-2">
                                  <span className="truncate">{t.name}</span>
                                  {!t.isActive && (
                                    <span className="flex-shrink-0 w-1.5 h-1.5 rounded-full bg-[var(--color-text-tertiary)]" />
                                  )}
                                </div>
                              </button>
                            ))
                          )}
                        </div>
                      )}
                    </div>
                  );
                })
              )}
            </div>
          </div>

          {/* ─── Right panel: editor ───────────────────────────────────── */}
          <div className="flex-1 flex flex-col overflow-hidden">
            {!editorActive ? (
              /* Empty state */
              <div className="flex-1 flex items-center justify-center">
                <div className="text-center space-y-3">
                  <Mail className="h-12 w-12 mx-auto text-[var(--color-text-tertiary)] opacity-40" />
                  <div>
                    <p className="text-sm font-medium text-[var(--color-text-secondary)]">
                      Select a template to edit
                    </p>
                    <p className="text-xs text-[var(--color-text-tertiary)] mt-1">
                      Or create a new one to get started
                    </p>
                  </div>
                  <Button variant="outline" size="sm" onClick={startCreate}>
                    <Plus className="mr-1.5 h-3.5 w-3.5" />
                    New Template
                  </Button>
                </div>
              </div>
            ) : loadingDetail ? (
              <div className="flex-1 flex items-center justify-center">
                <Loader2 className="h-6 w-6 animate-spin text-[var(--color-text-tertiary)]" />
              </div>
            ) : (
              <>
                {/* Editor toolbar */}
                <div className="flex items-center justify-between px-5 py-3 border-b border-[var(--color-border)] bg-[var(--color-surface)]">
                  <div className="flex items-center gap-3">
                    <h2 className="text-sm font-semibold text-[var(--color-text-primary)]">
                      {isCreating ? 'New Template' : form.name}
                    </h2>
                    {!isCreating && <ChannelBadge channel={form.channel} />}
                    {dirty && (
                      <span className="text-xs text-[var(--color-warning)] font-medium">Unsaved</span>
                    )}
                  </div>
                  <div className="flex items-center gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setPreviewOpen(!previewOpen)}
                      className="gap-1.5"
                    >
                      <Eye className="h-3.5 w-3.5" />
                      Preview
                    </Button>
                    {!isCreating && selectedId && (
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={deleteSelected}
                        className="text-[var(--color-error)] hover:text-[var(--color-error)] hover:bg-[var(--color-error-light)]"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </Button>
                    )}
                    <Button
                      size="sm"
                      onClick={save}
                      disabled={saving || !form.name || !form.bodyTemplate}
                      className="gap-1.5"
                    >
                      {saving && <Loader2 className="h-3.5 w-3.5 animate-spin" />}
                      {saving ? 'Saving...' : isCreating ? 'Create' : 'Save'}
                    </Button>
                  </div>
                </div>

                {/* Editor body */}
                <div className="flex-1 flex overflow-hidden">
                  {/* Main editor area */}
                  <div className="flex-1 overflow-y-auto p-5 space-y-5">
                    {/* Identity fields (name + channel — only on create) */}
                    {isCreating && (
                      <div className="grid grid-cols-2 gap-4">
                        <div className="space-y-1.5">
                          <Label htmlFor="tpl-name" className="text-xs">Template Name</Label>
                          <Input
                            id="tpl-name"
                            placeholder="e.g. registration.welcome-email"
                            value={form.name}
                            onChange={(e) => updateForm({ name: e.target.value })}
                            className="h-9"
                          />
                        </div>
                        <div className="space-y-1.5">
                          <Label htmlFor="tpl-channel" className="text-xs">Channel</Label>
                          <Select
                            value={form.channel}
                            onValueChange={(v) => updateForm({ channel: v })}
                          >
                            <SelectTrigger id="tpl-channel" className="h-9">
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              {CHANNELS.map((ch) => {
                                const Icon = channelMeta[ch].icon;
                                return (
                                  <SelectItem key={ch} value={ch}>
                                    <span className="flex items-center gap-2">
                                      <Icon className="h-3.5 w-3.5" />
                                      {ch}
                                    </span>
                                  </SelectItem>
                                );
                              })}
                            </SelectContent>
                          </Select>
                        </div>
                      </div>
                    )}

                    {/* Description */}
                    <div className="space-y-1.5">
                      <div className="flex items-center justify-between">
                        <Label htmlFor="tpl-desc" className="text-xs">Description</Label>
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          className="h-6 gap-1 text-[10px] text-[var(--color-text-tertiary)] hover:text-[var(--color-brand-primary)]"
                          onClick={generateDescription}
                          disabled={generatingDescription}
                        >
                          {generatingDescription ? (
                            <Loader2 className="h-3 w-3 animate-spin" />
                          ) : (
                            <Sparkles className="h-3 w-3" />
                          )}
                          {generatingDescription ? 'Generating...' : 'AI Generate'}
                        </Button>
                      </div>
                      <Input
                        id="tpl-desc"
                        placeholder="Brief description of this template's purpose"
                        value={form.description}
                        onChange={(e) => updateForm({ description: e.target.value })}
                        className="h-9 text-sm"
                      />
                    </div>

                    {/* Subject template (Email and Push only) */}
                    {(form.channel === 'Email' || form.channel === 'Push') && (
                      <div className="space-y-1.5">
                        <Label htmlFor="tpl-subject" className="text-xs">Subject Line</Label>
                        <Input
                          id="tpl-subject"
                          placeholder="e.g. Welcome to {{ tenant_name }}!"
                          value={form.subjectTemplate}
                          onChange={(e) => updateForm({ subjectTemplate: e.target.value })}
                          className="h-9 font-mono text-sm"
                        />
                      </div>
                    )}

                    {/* Body template */}
                    <div className="space-y-1.5 flex-1">
                      <div className="flex items-center justify-between">
                        <Label htmlFor="tpl-body" className="text-xs">Body Template</Label>
                        <span className="text-[10px] text-[var(--color-text-tertiary)]">
                          Scriban syntax: {'{{ variable }}'} {'{{ if condition }}...{{ end }}'} {'{{ for item in list }}...{{ end }}'}
                        </span>
                      </div>
                      <Textarea
                        id="tpl-body"
                        placeholder={form.channel === 'SMS'
                          ? '{{ tenant_name }}: Your verification code is {{ otp_code }}. It expires in {{ expiry_minutes }} minutes.'
                          : '<h1>Welcome, {{ first_name }}!</h1>\n<p>Your account at {{ tenant_name }} is ready.</p>'
                        }
                        className="min-h-[320px] font-mono text-sm leading-relaxed resize-y"
                        value={form.bodyTemplate}
                        onChange={(e) => updateForm({ bodyTemplate: e.target.value })}
                      />
                    </div>

                    {/* Settings row */}
                    <Card>
                      <CardContent className="p-4">
                        <div className="flex items-center gap-8">
                          <div className="flex items-center gap-2.5">
                            <Switch
                              id="tpl-active"
                              checked={form.isActive}
                              onCheckedChange={(v) => updateForm({ isActive: v })}
                            />
                            <Label htmlFor="tpl-active" className="text-sm cursor-pointer">Active</Label>
                          </div>
                          <div className="flex items-center gap-2.5">
                            <Switch
                              id="tpl-shared"
                              checked={form.isShared}
                              onCheckedChange={(v) => updateForm({ isShared: v })}
                            />
                            <div>
                              <Label htmlFor="tpl-shared" className="text-sm cursor-pointer">Shared</Label>
                              <p className="text-[10px] text-[var(--color-text-tertiary)]">Available to all tenants</p>
                            </div>
                          </div>
                          {selectedDetail && (
                            <div className="ml-auto text-xs text-[var(--color-text-tertiary)]">
                              Last updated {selectedDetail.updatedAt ? new Date(selectedDetail.updatedAt).toLocaleDateString() : 'N/A'}
                            </div>
                          )}
                        </div>
                      </CardContent>
                    </Card>
                  </div>

                  {/* Preview panel (collapsible right side) */}
                  {previewOpen && (
                    <div className="w-[380px] flex-shrink-0 border-l border-[var(--color-border)] flex flex-col bg-[var(--color-surface-inset)]">
                      <div className="flex items-center justify-between px-4 py-3 border-b border-[var(--color-border)]">
                        <h3 className="text-xs font-semibold text-[var(--color-text-primary)] uppercase tracking-wide">
                          Live Preview
                        </h3>
                        <Button variant="ghost" size="sm" className="h-6 text-xs" onClick={runPreview} disabled={previewing}>
                          {previewing ? <Loader2 className="h-3 w-3 animate-spin" /> : 'Refresh'}
                        </Button>
                      </div>

                      <div className="flex-1 overflow-y-auto p-4 space-y-4">
                        {/* Sample data input */}
                        <div className="space-y-1.5">
                          <Label className="text-[10px] uppercase tracking-wide text-[var(--color-text-tertiary)]">
                            Sample Data (JSON)
                          </Label>
                          <Textarea
                            className="min-h-[120px] font-mono text-xs leading-relaxed resize-y"
                            value={sampleJson}
                            onChange={(e) => setSampleJson(e.target.value)}
                          />
                        </div>

                        {/* Rendered output */}
                        {previewResult && (
                          <div className="space-y-3">
                            {previewResult.subject && (
                              <div>
                                <p className="text-[10px] uppercase tracking-wide text-[var(--color-text-tertiary)] mb-1.5 font-medium">
                                  Subject
                                </p>
                                <div className="bg-[var(--color-surface)] rounded-sm border border-[var(--color-border)] px-3 py-2 text-sm text-[var(--color-text-primary)]">
                                  {previewResult.subject}
                                </div>
                              </div>
                            )}
                            <div>
                              <p className="text-[10px] uppercase tracking-wide text-[var(--color-text-tertiary)] mb-1.5 font-medium">
                                Body
                              </p>
                              {form.channel === 'SMS' ? (
                                /* SMS phone-style preview */
                                <div className="bg-[var(--color-surface)] rounded-lg border border-[var(--color-border)] p-4">
                                  <div className="bg-[var(--color-brand-primary-light)] rounded-2xl rounded-bl-sm px-4 py-3 max-w-[280px]">
                                    <p className="text-sm text-[var(--color-text-primary)] whitespace-pre-wrap">
                                      {previewResult.body}
                                    </p>
                                  </div>
                                </div>
                              ) : (
                                /* Email/Push HTML preview */
                                <div
                                  className="bg-white rounded-sm border border-[var(--color-border)] p-4 text-sm prose prose-sm max-w-none"
                                  dangerouslySetInnerHTML={{ __html: previewResult.body }}
                                />
                              )}
                            </div>
                          </div>
                        )}

                        {!previewResult && !previewing && (
                          <div className="text-center py-8">
                            <Eye className="h-8 w-8 mx-auto text-[var(--color-text-tertiary)] opacity-30 mb-2" />
                            <p className="text-xs text-[var(--color-text-tertiary)]">
                              Preview will render automatically as you type
                            </p>
                          </div>
                        )}

                        {previewing && (
                          <div className="flex items-center justify-center py-8">
                            <Loader2 className="h-5 w-5 animate-spin text-[var(--color-text-tertiary)]" />
                          </div>
                        )}
                      </div>
                    </div>
                  )}
                </div>
              </>
            )}
          </div>
        </div>
      )}

      {/* ── Bindings Tab ──────────────────────────────────────────────── */}
      {activeTab === 'bindings' && (
        <div className="flex-1 overflow-auto p-6">
          <Card>
            <CardContent className="p-0">
              <div className="flex items-center justify-between px-5 py-3 border-b border-[var(--color-border)]">
                <div>
                  <h2 className="text-sm font-semibold text-[var(--color-text-primary)]">Template Bindings</h2>
                  <p className="text-xs text-[var(--color-text-secondary)]">
                    Map notification templates to tenants with optional base and override templates
                  </p>
                </div>
                <Button size="sm" onClick={openCreateBinding}>
                  <Plus className="mr-1.5 h-3.5 w-3.5" />
                  New Binding
                </Button>
              </div>
              <div className="p-5">
                <DataTable
                  columns={bindingColumns}
                  data={bindings}
                  loading={loadingBindings}
                  getRowId={(row) => row.id}
                />
              </div>
            </CardContent>
          </Card>
        </div>
      )}

      {/* ═══════════════════════════════════════════════════════════════════
          Binding Dialog
          ═══════════════════════════════════════════════════════════════ */}
      <Dialog open={bindingDialogOpen} onOpenChange={setBindingDialogOpen}>
        <DialogContent className="max-w-[500px]">
          <DialogHeader>
            <DialogTitle>{editingBindingId ? 'Edit Binding' : 'Create Binding'}</DialogTitle>
            <DialogDescription>
              Map a notification template to this tenant with optional base and override templates.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-2">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <Label htmlFor="bind-name" className="text-xs">Template Name</Label>
                <Input
                  id="bind-name"
                  placeholder="e.g. registration.welcome-email"
                  value={bindingForm.templateName}
                  onChange={(e) => setBindingForm({ ...bindingForm, templateName: e.target.value })}
                  disabled={!!editingBindingId}
                  className="h-9"
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="bind-channel" className="text-xs">Channel</Label>
                <Select
                  value={bindingForm.channel}
                  onValueChange={(v) => setBindingForm({ ...bindingForm, channel: v })}
                  disabled={!!editingBindingId}
                >
                  <SelectTrigger id="bind-channel" className="h-9">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {CHANNELS.map((ch) => (
                      <SelectItem key={ch} value={ch}>{ch}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="bind-base" className="text-xs">Base Template (Layout Wrapper)</Label>
              <Select
                value={bindingForm.baseTemplateId || '_none'}
                onValueChange={(v) => setBindingForm({ ...bindingForm, baseTemplateId: v === '_none' ? '' : v })}
              >
                <SelectTrigger id="bind-base" className="h-9">
                  <SelectValue placeholder="None (no wrapper)" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="_none">None</SelectItem>
                  {templates.map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name} ({t.channel})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="bind-override" className="text-xs">Override Template</Label>
              <Select
                value={bindingForm.overrideTemplateId || '_none'}
                onValueChange={(v) => setBindingForm({ ...bindingForm, overrideTemplateId: v === '_none' ? '' : v })}
              >
                <SelectTrigger id="bind-override" className="h-9">
                  <SelectValue placeholder="None (use default)" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="_none">None</SelectItem>
                  {templates.map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name} ({t.channel})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex items-center gap-2">
              <Switch
                id="bind-enabled"
                checked={bindingForm.isEnabled}
                onCheckedChange={(v) => setBindingForm({ ...bindingForm, isEnabled: v })}
              />
              <Label htmlFor="bind-enabled">Enabled</Label>
            </div>
          </div>

          <DialogFooter>
            <Button onClick={saveBinding} disabled={savingBinding || !bindingForm.templateName}>
              {savingBinding ? 'Saving...' : editingBindingId ? 'Update' : 'Create'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
