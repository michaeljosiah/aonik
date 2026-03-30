import { useCallback, useEffect, useMemo, useState } from 'react';
import { Bell, Plus, Pencil, Trash2, Eye, Sparkles, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { DataTable, type ColumnDef } from '@/components/ui/data-table';
import { DataTableRowActions } from '@/components/ui/data-table';
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
  NotificationTemplateBindingResponse,
} from '@/types';

const CHANNELS = ['Email', 'SMS', 'Push'];

// ═════════════════════════════════════════════════════════════════════════════
// Template Form State
// ═════════════════════════════════════════════════════════════════════════════
interface TemplateForm {
  name: string;
  channel: string;
  subjectTemplate: string;
  bodyTemplate: string;
  description: string;
  isShared: boolean;
  isActive: boolean;
}

const emptyTemplateForm: TemplateForm = {
  name: '',
  channel: 'Email',
  subjectTemplate: '',
  bodyTemplate: '',
  description: '',
  isShared: false,
  isActive: true,
};

// ═════════════════════════════════════════════════════════════════════════════
// Binding Form State
// ═════════════════════════════════════════════════════════════════════════════
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

// ═════════════════════════════════════════════════════════════════════════════
// Page
// ═════════════════════════════════════════════════════════════════════════════
export function NotificationTemplatesPage() {
  const [activeTab, setActiveTab] = useState('templates');

  // ── Template state ─────────────────────────────────────────────────────
  const [templates, setTemplates] = useState<NotificationTemplateSummary[]>([]);
  const [loadingTemplates, setLoadingTemplates] = useState(true);
  const [templateDialogOpen, setTemplateDialogOpen] = useState(false);
  const [editingTemplateId, setEditingTemplateId] = useState<string | null>(null);
  const [templateForm, setTemplateForm] = useState<TemplateForm>(emptyTemplateForm);
  const [savingTemplate, setSavingTemplate] = useState(false);

  // ── AI generation state ─────────────────────────────────────────────
  const [generatingDescription, setGeneratingDescription] = useState(false);

  // ── Preview state ──────────────────────────────────────────────────────
  const [previewDialogOpen, setPreviewDialogOpen] = useState(false);
  const [previewSubjectTemplate, setPreviewSubjectTemplate] = useState('');
  const [previewBodyTemplate, setPreviewBodyTemplate] = useState('');
  const [previewSampleJson, setPreviewSampleJson] = useState('{\n  "name": "John Doe",\n  "amount": "100.00"\n}');
  const [previewResult, setPreviewResult] = useState<{ subject: string; body: string } | null>(null);
  const [previewing, setPreviewing] = useState(false);

  // ── Binding state ──────────────────────────────────────────────────────
  const [bindings, setBindings] = useState<NotificationTemplateBindingResponse[]>([]);
  const [loadingBindings, setLoadingBindings] = useState(true);
  const [bindingDialogOpen, setBindingDialogOpen] = useState(false);
  const [editingBindingId, setEditingBindingId] = useState<string | null>(null);
  const [bindingForm, setBindingForm] = useState<BindingForm>(emptyBindingForm);
  const [savingBinding, setSavingBinding] = useState(false);

  // ── Load data ──────────────────────────────────────────────────────────
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

  // ── Template CRUD handlers ─────────────────────────────────────────────
  function openCreateTemplate() {
    setEditingTemplateId(null);
    setTemplateForm(emptyTemplateForm);
    setTemplateDialogOpen(true);
  }

  async function openEditTemplate(id: string) {
    try {
      const detail = await notificationTemplateService.get(id);
      setEditingTemplateId(id);
      setTemplateForm({
        name: detail.name,
        channel: detail.channel,
        subjectTemplate: detail.subjectTemplate,
        bodyTemplate: detail.bodyTemplate,
        description: detail.description,
        isShared: detail.isShared,
        isActive: detail.isActive,
      });
      setTemplateDialogOpen(true);
    } catch {
      toast.error('Failed to load template details');
    }
  }

  async function saveTemplate() {
    try {
      setSavingTemplate(true);
      if (editingTemplateId) {
        await notificationTemplateService.update(editingTemplateId, {
          subjectTemplate: templateForm.subjectTemplate,
          bodyTemplate: templateForm.bodyTemplate,
          description: templateForm.description,
          isShared: templateForm.isShared,
          isActive: templateForm.isActive,
        });
        toast.success('Template updated');
      } else {
        await notificationTemplateService.create({
          name: templateForm.name,
          channel: templateForm.channel,
          subjectTemplate: templateForm.subjectTemplate,
          bodyTemplate: templateForm.bodyTemplate,
          description: templateForm.description,
          isShared: templateForm.isShared,
          isActive: templateForm.isActive,
        });
        toast.success('Template created');
      }
      setTemplateDialogOpen(false);
      await loadTemplates();
    } catch {
      toast.error('Failed to save template');
    } finally {
      setSavingTemplate(false);
    }
  }

  async function deleteTemplate(id: string) {
    try {
      await notificationTemplateService.delete(id);
      toast.success('Template deleted');
      await loadTemplates();
    } catch {
      toast.error('Failed to delete template');
    }
  }

  // ── Preview handler ────────────────────────────────────────────────────
  function openPreview(template?: NotificationTemplateSummary) {
    if (template) {
      // Load full template to get the body/subject
      notificationTemplateService.get(template.id).then((detail) => {
        setPreviewSubjectTemplate(detail.subjectTemplate);
        setPreviewBodyTemplate(detail.bodyTemplate);
        setPreviewResult(null);
        setPreviewDialogOpen(true);
      }).catch(() => toast.error('Failed to load template for preview'));
    } else {
      setPreviewSubjectTemplate(templateForm.subjectTemplate);
      setPreviewBodyTemplate(templateForm.bodyTemplate);
      setPreviewResult(null);
      setPreviewDialogOpen(true);
    }
  }

  async function runPreview() {
    try {
      setPreviewing(true);
      const result = await notificationTemplateService.preview({
        subjectTemplate: previewSubjectTemplate,
        bodyTemplate: previewBodyTemplate,
        sampleModelJson: previewSampleJson,
      });
      setPreviewResult(result);
    } catch {
      toast.error('Preview failed — check your template syntax');
    } finally {
      setPreviewing(false);
    }
  }

  // ── AI description generation ──────────────────────────────────────
  async function generateDescription() {
    const { name, channel, bodyTemplate } = templateForm;
    if (!name && !bodyTemplate) {
      toast.error('Enter a template name or body first so AI has context to work with');
      return;
    }

    try {
      setGeneratingDescription(true);
      const prompt = [
        'Generate a short, clear description (1-2 sentences) for a notification template.',
        `Template name: ${name || '(not set)'}`,
        `Channel: ${channel}`,
        bodyTemplate ? `Body template:\n${bodyTemplate}` : null,
        'Reply with ONLY the description text, no quotes or extra formatting.',
      ]
        .filter(Boolean)
        .join('\n');

      const response = await api.post<{ message: string }>('/ai/chat', { message: prompt });
      if (response.message) {
        setTemplateForm((prev) => ({ ...prev, description: response.message.trim() }));
        toast.success('Description generated');
      }
    } catch {
      toast.error('Failed to generate description');
    } finally {
      setGeneratingDescription(false);
    }
  }

  // ── Binding CRUD handlers ─────────────────────────────────────────────
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

  // ── Template columns ──────────────────────────────────────────────────
  const templateColumns = useMemo<ColumnDef<NotificationTemplateSummary>[]>(
    () => [
      { id: 'name', accessorKey: 'name', header: 'Name' },
      {
        id: 'channel',
        accessorKey: 'channel',
        header: 'Channel',
        cell: (row) => (
          <Badge variant="outline">{row.channel}</Badge>
        ),
      },
      { id: 'description', accessorKey: 'description', header: 'Description' },
      {
        id: 'isShared',
        accessorKey: 'isShared',
        header: 'Shared',
        cell: (row) =>
          row.isShared ? (
            <Badge className="bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200">Shared</Badge>
          ) : null,
      },
      {
        id: 'isActive',
        accessorKey: 'isActive',
        header: 'Status',
        cell: (row) => (
          <Badge className={row.isActive
            ? 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200'
            : 'bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400'
          }>
            {row.isActive ? 'Active' : 'Inactive'}
          </Badge>
        ),
      },
      {
        id: 'actions',
        header: '',
        cell: (row) => (
          <DataTableRowActions
            actions={[
              { label: 'Preview', icon: <Eye className="w-4 h-4" />, onClick: () => openPreview(row) },
              { label: 'Edit', icon: <Pencil className="w-4 h-4" />, onClick: () => openEditTemplate(row.id) },
              { label: 'Delete', icon: <Trash2 className="w-4 h-4" />, onClick: () => deleteTemplate(row.id), variant: 'danger' },
            ]}
          />
        ),
      },
    ],
    [],
  );

  // ── Binding columns ────────────────────────────────────────────────────
  const bindingColumns = useMemo<ColumnDef<NotificationTemplateBindingResponse>[]>(
    () => [
      { id: 'templateName', accessorKey: 'templateName', header: 'Template Name' },
      {
        id: 'channel',
        accessorKey: 'channel',
        header: 'Channel',
        cell: (row) => (
          <Badge variant="outline">{row.channel}</Badge>
        ),
      },
      {
        id: 'baseTemplateId',
        accessorKey: 'baseTemplateId',
        header: 'Base Template',
        cell: (row) => row.baseTemplateId
          ? <span className="font-mono text-xs">{row.baseTemplateId.slice(0, 8)}...</span>
          : <span className="text-[var(--color-text-tertiary)]">None</span>,
      },
      {
        id: 'overrideTemplateId',
        accessorKey: 'overrideTemplateId',
        header: 'Override Template',
        cell: (row) => row.overrideTemplateId
          ? <span className="font-mono text-xs">{row.overrideTemplateId.slice(0, 8)}...</span>
          : <span className="text-[var(--color-text-tertiary)]">None</span>,
      },
      {
        id: 'isEnabled',
        accessorKey: 'isEnabled',
        header: 'Status',
        cell: (row) => (
          <Badge className={row.isEnabled
            ? 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200'
            : 'bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400'
          }>
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
    [],
  );

  // ═════════════════════════════════════════════════════════════════════════
  // Render
  // ═════════════════════════════════════════════════════════════════════════
  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-[var(--color-brand-primary-light)]">
            <Bell className="h-5 w-5 text-[var(--color-brand-primary)]" />
          </div>
          <div>
            <h1 className="text-xl font-semibold text-[var(--color-text-primary)]">Notification Templates</h1>
            <p className="text-sm text-[var(--color-text-secondary)]">
              Manage email, SMS, and push notification templates
            </p>
          </div>
        </div>
      </div>

      <Tabs value={activeTab} onValueChange={setActiveTab}>
        <TabsList>
          <TabsTrigger value="templates">Templates</TabsTrigger>
          <TabsTrigger value="bindings">Bindings</TabsTrigger>
        </TabsList>

        {/* ── Templates Tab ──────────────────────────────────────────── */}
        <TabsContent value="templates">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle>Templates</CardTitle>
              <Button size="sm" onClick={openCreateTemplate}>
                <Plus className="mr-1.5 h-4 w-4" />
                New Template
              </Button>
            </CardHeader>
            <CardContent>
              <DataTable
                columns={templateColumns}
                data={templates}
                loading={loadingTemplates}
                getRowId={(row) => row.id}
              />
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Bindings Tab ───────────────────────────────────────────── */}
        <TabsContent value="bindings">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle>Template Bindings</CardTitle>
              <Button size="sm" onClick={openCreateBinding}>
                <Plus className="mr-1.5 h-4 w-4" />
                New Binding
              </Button>
            </CardHeader>
            <CardContent>
              <DataTable
                columns={bindingColumns}
                data={bindings}
                loading={loadingBindings}
                getRowId={(row) => row.id}
              />
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* ═══════════════════════════════════════════════════════════════════
          Template Dialog
          ═══════════════════════════════════════════════════════════════ */}
      <Dialog open={templateDialogOpen} onOpenChange={setTemplateDialogOpen}>
        <DialogContent className="max-w-[700px] max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{editingTemplateId ? 'Edit Template' : 'Create Template'}</DialogTitle>
            <DialogDescription>
              {editingTemplateId
                ? 'Update the notification template content and settings.'
                : 'Create a new notification template using Scriban syntax.'}
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-2">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <Label htmlFor="tpl-name">Name</Label>
                <Input
                  id="tpl-name"
                  placeholder="e.g. WelcomeEmail"
                  value={templateForm.name}
                  onChange={(e) => setTemplateForm({ ...templateForm, name: e.target.value })}
                  disabled={!!editingTemplateId}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="tpl-channel">Channel</Label>
                <Select
                  value={templateForm.channel}
                  onValueChange={(v) => setTemplateForm({ ...templateForm, channel: v })}
                  disabled={!!editingTemplateId}
                >
                  <SelectTrigger id="tpl-channel">
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
              <div className="flex items-center justify-between">
                <Label htmlFor="tpl-description">Description</Label>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="h-7 gap-1.5 text-xs text-[var(--color-text-secondary)] hover:text-[var(--color-brand-primary)]"
                  onClick={generateDescription}
                  disabled={generatingDescription}
                >
                  {generatingDescription ? (
                    <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  ) : (
                    <Sparkles className="h-3.5 w-3.5" />
                  )}
                  {generatingDescription ? 'Generating...' : 'AI Generate'}
                </Button>
              </div>
              <Input
                id="tpl-description"
                placeholder="Brief description of this template"
                value={templateForm.description}
                onChange={(e) => setTemplateForm({ ...templateForm, description: e.target.value })}
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="tpl-subject">Subject Template</Label>
              <Input
                id="tpl-subject"
                placeholder={'e.g. Welcome, {{ name }}!'}
                value={templateForm.subjectTemplate}
                onChange={(e) => setTemplateForm({ ...templateForm, subjectTemplate: e.target.value })}
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="tpl-body">Body Template</Label>
              <Textarea
                id="tpl-body"
                placeholder={'Hello {{ name }},\n\nYour account has been created.'}
                className="min-h-[200px] font-mono text-sm"
                value={templateForm.bodyTemplate}
                onChange={(e) => setTemplateForm({ ...templateForm, bodyTemplate: e.target.value })}
              />
            </div>

            <div className="flex items-center gap-6">
              <div className="flex items-center gap-2">
                <Switch
                  id="tpl-shared"
                  checked={templateForm.isShared}
                  onCheckedChange={(v) => setTemplateForm({ ...templateForm, isShared: v })}
                />
                <Label htmlFor="tpl-shared">Shared (available to all tenants)</Label>
              </div>
              <div className="flex items-center gap-2">
                <Switch
                  id="tpl-active"
                  checked={templateForm.isActive}
                  onCheckedChange={(v) => setTemplateForm({ ...templateForm, isActive: v })}
                />
                <Label htmlFor="tpl-active">Active</Label>
              </div>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => openPreview()}>
              <Eye className="mr-1.5 h-4 w-4" />
              Preview
            </Button>
            <Button onClick={saveTemplate} disabled={savingTemplate || !templateForm.name || !templateForm.bodyTemplate}>
              {savingTemplate ? 'Saving...' : editingTemplateId ? 'Update' : 'Create'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ═══════════════════════════════════════════════════════════════════
          Preview Dialog
          ═══════════════════════════════════════════════════════════════ */}
      <Dialog open={previewDialogOpen} onOpenChange={setPreviewDialogOpen}>
        <DialogContent className="max-w-[700px] max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Template Preview</DialogTitle>
            <DialogDescription>
              Enter sample data as JSON to preview the rendered output.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <Label htmlFor="preview-json">Sample Model (JSON)</Label>
              <Textarea
                id="preview-json"
                className="min-h-[100px] font-mono text-sm"
                value={previewSampleJson}
                onChange={(e) => setPreviewSampleJson(e.target.value)}
              />
            </div>

            <Button onClick={runPreview} disabled={previewing}>
              {previewing ? 'Rendering...' : 'Render Preview'}
            </Button>

            {previewResult && (
              <div className="space-y-3 rounded-md border border-[var(--color-border)] p-4">
                {previewResult.subject && (
                  <div>
                    <p className="text-xs font-medium text-[var(--color-text-tertiary)] uppercase mb-1">Subject</p>
                    <p className="text-sm text-[var(--color-text-primary)]">{previewResult.subject}</p>
                  </div>
                )}
                <div>
                  <p className="text-xs font-medium text-[var(--color-text-tertiary)] uppercase mb-1">Body</p>
                  <div
                    className="prose prose-sm max-w-none text-[var(--color-text-primary)] bg-[var(--color-surface-inset)] rounded p-3"
                    dangerouslySetInnerHTML={{ __html: previewResult.body }}
                  />
                </div>
              </div>
            )}
          </div>
        </DialogContent>
      </Dialog>

      {/* ═══════════════════════════════════════════════════════════════════
          Binding Dialog
          ═══════════════════════════════════════════════════════════════ */}
      <Dialog open={bindingDialogOpen} onOpenChange={setBindingDialogOpen}>
        <DialogContent className="max-w-[500px]">
          <DialogHeader>
            <DialogTitle>{editingBindingId ? 'Edit Binding' : 'Create Binding'}</DialogTitle>
            <DialogDescription>
              Bind a notification template to this tenant with optional base/override templates.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-2">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <Label htmlFor="bind-name">Template Name</Label>
                <Input
                  id="bind-name"
                  placeholder="e.g. WelcomeEmail"
                  value={bindingForm.templateName}
                  onChange={(e) => setBindingForm({ ...bindingForm, templateName: e.target.value })}
                  disabled={!!editingBindingId}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="bind-channel">Channel</Label>
                <Select
                  value={bindingForm.channel}
                  onValueChange={(v) => setBindingForm({ ...bindingForm, channel: v })}
                  disabled={!!editingBindingId}
                >
                  <SelectTrigger id="bind-channel">
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
              <Label htmlFor="bind-base">Base Template</Label>
              <Select
                value={bindingForm.baseTemplateId || '_none'}
                onValueChange={(v) => setBindingForm({ ...bindingForm, baseTemplateId: v === '_none' ? '' : v })}
              >
                <SelectTrigger id="bind-base">
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
              <Label htmlFor="bind-override">Override Template</Label>
              <Select
                value={bindingForm.overrideTemplateId || '_none'}
                onValueChange={(v) => setBindingForm({ ...bindingForm, overrideTemplateId: v === '_none' ? '' : v })}
              >
                <SelectTrigger id="bind-override">
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
