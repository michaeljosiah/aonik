import { useState, useCallback, useEffect, useRef } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
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
import { FileText, Plus, Search, Pencil, Trash2, AlertCircle, Loader2 } from 'lucide-react';
import { promptSpecService } from '@/services/aiService';
import type { PromptSpecResponse, CreatePromptSpecRequest, UpdatePromptSpecRequest } from '@/types/ai';

const getErrorMessage = (err: unknown, fallback: string) => {
  if (err && typeof err === 'object' && 'userMessage' in err) {
    const message = String((err as { userMessage?: string }).userMessage ?? '').trim();
    if (message) return message;
  }
  return fallback;
};

export function PromptTemplatesPage() {
  const [prompts, setPrompts] = useState<PromptSpecResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const requestIdRef = useRef(0);

  // Dialog state
  const [showCreateDialog, setShowCreateDialog] = useState(false);
  const [editingPrompt, setEditingPrompt] = useState<PromptSpecResponse | null>(null);
  const [saving, setSaving] = useState(false);

  // Form state
  const [formName, setFormName] = useState('');
  const [formVersion, setFormVersion] = useState('v1');
  const [formSystemTemplate, setFormSystemTemplate] = useState('');
  const [formUserTemplate, setFormUserTemplate] = useState('');
  const [formDeveloperTemplate, setFormDeveloperTemplate] = useState('');
  const [formVariablesSchema, setFormVariablesSchema] = useState('');
  const [formOutputSchema, setFormOutputSchema] = useState('');
  const [formIsPublished, setFormIsPublished] = useState(false);

  const loadPrompts = useCallback(async () => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);
    try {
      const result = await promptSpecService.list(searchQuery || undefined);
      if (requestIdRef.current !== requestId) return;
      setPrompts(result);
    } catch (err) {
      if (requestIdRef.current !== requestId) return;
      setError(getErrorMessage(err, 'Failed to load prompt templates'));
    } finally {
      if (requestIdRef.current === requestId) setLoading(false);
    }
  }, [searchQuery]);

  useEffect(() => { loadPrompts(); }, [loadPrompts]);

  const resetForm = () => {
    setFormName('');
    setFormVersion('v1');
    setFormSystemTemplate('');
    setFormUserTemplate('');
    setFormDeveloperTemplate('');
    setFormVariablesSchema('');
    setFormOutputSchema('');
    setFormIsPublished(false);
  };

  const openCreate = () => {
    resetForm();
    setEditingPrompt(null);
    setShowCreateDialog(true);
  };

  const openEdit = (prompt: PromptSpecResponse) => {
    setFormName(prompt.name);
    setFormVersion(prompt.version);
    setFormSystemTemplate(prompt.systemTemplate);
    setFormUserTemplate(prompt.userTemplate);
    setFormDeveloperTemplate(prompt.developerTemplate);
    setFormVariablesSchema(prompt.variablesSchemaJson);
    setFormOutputSchema(prompt.outputSchemaJson);
    setFormIsPublished(prompt.isPublished);
    setEditingPrompt(prompt);
    setShowCreateDialog(true);
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      if (editingPrompt) {
        const request: UpdatePromptSpecRequest = {
          systemTemplate: formSystemTemplate,
          userTemplate: formUserTemplate,
          developerTemplate: formDeveloperTemplate,
          variablesSchemaJson: formVariablesSchema,
          outputSchemaJson: formOutputSchema,
          isPublished: formIsPublished,
        };
        await promptSpecService.update(editingPrompt.id, request);
      } else {
        const request: CreatePromptSpecRequest = {
          name: formName,
          version: formVersion,
          systemTemplate: formSystemTemplate,
          userTemplate: formUserTemplate || undefined,
          developerTemplate: formDeveloperTemplate || undefined,
          variablesSchemaJson: formVariablesSchema || undefined,
          outputSchemaJson: formOutputSchema || undefined,
          isPublished: formIsPublished,
        };
        await promptSpecService.create(request);
      }
      setShowCreateDialog(false);
      loadPrompts();
    } catch (err) {
      setError(getErrorMessage(err, 'Failed to save prompt template'));
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await promptSpecService.delete(id);
      loadPrompts();
    } catch (err) {
      setError(getErrorMessage(err, 'Failed to delete prompt template'));
    }
  };

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Prompt Templates</h1>
          <p className="text-muted-foreground text-sm mt-1">
            Manage versioned prompt templates for AI tasks. Tenant-specific overrides take precedence over global defaults.
          </p>
        </div>
        <Button onClick={openCreate}>
          <Plus className="mr-2 h-4 w-4" />
          Create Prompt
        </Button>
      </div>

      <div className="relative max-w-sm">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
        <Input
          placeholder="Search by name..."
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          className="pl-9"
        />
      </div>

      {error && (
        <div className="flex items-center gap-2 text-destructive text-sm">
          <AlertCircle className="h-4 w-4" />
          {error}
        </div>
      )}

      {loading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : prompts.length === 0 ? (
        <div className="text-center py-12 text-muted-foreground">
          <FileText className="h-12 w-12 mx-auto mb-4 opacity-30" />
          <p>No prompt templates found</p>
        </div>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {prompts.map((prompt) => (
            <Card key={prompt.id} className="relative group">
              <CardHeader className="pb-3">
                <div className="flex items-start justify-between">
                  <div className="space-y-1">
                    <CardTitle className="text-base font-semibold">{prompt.name}</CardTitle>
                    <div className="flex items-center gap-2">
                      <Badge variant="outline" className="text-xs">{prompt.version}</Badge>
                      {prompt.isPublished ? (
                        <Badge className="text-xs bg-green-500/10 text-green-700 border-green-200">Published</Badge>
                      ) : (
                        <Badge variant="secondary" className="text-xs">Draft</Badge>
                      )}
                      {prompt.isOverride && (
                        <Badge className="text-xs bg-blue-500/10 text-blue-700 border-blue-200">Override</Badge>
                      )}
                    </div>
                  </div>
                  <div className="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                    <Button variant="ghost" size="icon-sm" onClick={() => openEdit(prompt)}>
                      <Pencil className="h-3.5 w-3.5" />
                    </Button>
                    <Button variant="ghost" size="icon-sm" onClick={() => handleDelete(prompt.id)}>
                      <Trash2 className="h-3.5 w-3.5 text-destructive" />
                    </Button>
                  </div>
                </div>
              </CardHeader>
              <CardContent>
                <p className="text-xs text-muted-foreground font-mono line-clamp-3">
                  {prompt.systemTemplate || 'No system template'}
                </p>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <Dialog open={showCreateDialog} onOpenChange={setShowCreateDialog}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>{editingPrompt ? `Edit: ${editingPrompt.name}` : 'Create Prompt Template'}</DialogTitle>
            <DialogDescription>
              {editingPrompt ? 'Update the prompt template configuration.' : 'Create a new versioned prompt template.'}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2 max-h-[60vh] overflow-y-auto">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <label className="text-sm font-medium" htmlFor="prompt-name">Name</label>
                <Input
                  id="prompt-name"
                  value={formName}
                  onChange={(e) => setFormName(e.target.value)}
                  placeholder="e.g. transaction_classification"
                  disabled={!!editingPrompt}
                />
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium" htmlFor="prompt-version">Version</label>
                <Input
                  id="prompt-version"
                  value={formVersion}
                  onChange={(e) => setFormVersion(e.target.value)}
                  placeholder="e.g. v1"
                  disabled={!!editingPrompt}
                />
              </div>
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium" htmlFor="system-template">System Template</label>
              <Textarea
                id="system-template"
                value={formSystemTemplate}
                onChange={(e) => setFormSystemTemplate(e.target.value)}
                placeholder="System prompt content..."
                rows={8}
                className="font-mono text-sm"
              />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium" htmlFor="user-template">User Template</label>
              <Textarea
                id="user-template"
                value={formUserTemplate}
                onChange={(e) => setFormUserTemplate(e.target.value)}
                placeholder="User prompt template with {{PLACEHOLDER}} variables..."
                rows={4}
                className="font-mono text-sm"
              />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium" htmlFor="developer-template">Developer Template</label>
              <Textarea
                id="developer-template"
                value={formDeveloperTemplate}
                onChange={(e) => setFormDeveloperTemplate(e.target.value)}
                placeholder="Developer prompt template (optional)..."
                rows={3}
                className="font-mono text-sm"
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <label className="text-sm font-medium" htmlFor="variables-schema">Variables Schema (JSON)</label>
                <Textarea
                  id="variables-schema"
                  value={formVariablesSchema}
                  onChange={(e) => setFormVariablesSchema(e.target.value)}
                  placeholder="{}"
                  rows={2}
                  className="font-mono text-sm"
                />
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium" htmlFor="output-schema">Output Schema (JSON)</label>
                <Textarea
                  id="output-schema"
                  value={formOutputSchema}
                  onChange={(e) => setFormOutputSchema(e.target.value)}
                  placeholder="{}"
                  rows={2}
                  className="font-mono text-sm"
                />
              </div>
            </div>

            <div className="flex items-center gap-2">
              <Switch
                id="published"
                checked={formIsPublished}
                onCheckedChange={setFormIsPublished}
              />
              <label className="text-sm font-medium" htmlFor="published">Published</label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowCreateDialog(false)}>Cancel</Button>
            <Button onClick={handleSave} disabled={saving || (!editingPrompt && !formName)}>
              {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {editingPrompt ? 'Save Changes' : 'Create'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
