import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { DataTable, ColumnDef } from '@/components/ui/data-table';
import { DataTableRowActions } from '@/components/ui/data-table';
import { ArrowLeft, Save, Plus, Trash2, Layers, Image, GripVertical } from 'lucide-react';
import {
  getContentBlock,
  createContentBlock,
  updateContentBlock,
  addContentBlockMedia,
  removeContentBlockMedia,
  reorderContentBlockMedia,
  type ContentBlock,
  type ContentBlockMedia,
} from '@/services/contentBlockService';

const AREA_OPTIONS = ['General', 'Banner', 'Hero', 'Sidebar', 'Footer', 'MySpaceBanner'];
const FORMAT_OPTIONS = ['Markdown', 'Html', 'ImageSet', 'Json'];

export function ContentBlockEditPage() {
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const isNew = id === 'new';

  const [loading, setLoading] = useState(!isNew);
  const [saving, setSaving] = useState(false);
  const [contentBlock, setContentBlock] = useState<ContentBlock | null>(null);
  const [selectedMediaIds, setSelectedMediaIds] = useState<Set<string>>(new Set());

  // Form state
  const [contentKey, setContentKey] = useState('');
  const [title, setTitle] = useState('');
  const [slug, setSlug] = useState('');
  const [area, setArea] = useState('General');
  const [format, setFormat] = useState('ImageSet');
  const [body, setBody] = useState('');
  const [locale, setLocale] = useState('en');
  const [isEnabled, setIsEnabled] = useState(true);
  const [priority, setPriority] = useState(100);

  // Media form state
  const [newMediaUrl, setNewMediaUrl] = useState('');
  const [newMediaAlt, setNewMediaAlt] = useState('');
  const [newMediaCaption, setNewMediaCaption] = useState('');
  const [newMediaLinkUrl, setNewMediaLinkUrl] = useState('');

  useEffect(() => {
    if (!isNew && id) {
      loadContentBlock(id);
    }
  }, [id, isNew]);

  async function loadContentBlock(contentBlockId: string) {
    try {
      setLoading(true);
      const block = await getContentBlock(contentBlockId);
      if (block) {
        setContentBlock(block);
        setContentKey(block.contentKey);
        setTitle(block.title);
        setSlug(block.slug || '');
        setArea(block.area);
        setFormat(block.format);
        setBody(block.body || '');
        setLocale(block.locale);
        setIsEnabled(block.isEnabled);
        setPriority(block.priority);
      }
    } catch (error) {
      console.error('Failed to load content block:', error);
      alert('Failed to load content block');
    } finally {
      setLoading(false);
    }
  }

  async function handleSave() {
    try {
      setSaving(true);

      const request = {
        contentKey,
        title,
        slug: slug || undefined,
        area,
        format,
        body: body || undefined,
        locale,
        isEnabled,
        startAt: undefined as string | undefined,
        endAt: undefined as string | undefined,
        priority,
        targetingJson: undefined as string | undefined,
      };

      if (isNew) {
        await createContentBlock(request);
      } else if (id) {
        await updateContentBlock(id, request);
      }

      navigate('/cms/content-blocks');
    } catch (error) {
      console.error('Failed to save content block:', error);
      alert('Failed to save content block');
    } finally {
      setSaving(false);
    }
  }

  async function handleAddMedia() {
    if (!id || id === 'new' || !newMediaUrl.trim()) return;

    try {
      await addContentBlockMedia(id, {
        url: newMediaUrl.trim(),
        alt: newMediaAlt.trim() || undefined,
        caption: newMediaCaption.trim() || undefined,
        linkUrl: newMediaLinkUrl.trim() || undefined,
      });

      // Clear form
      setNewMediaUrl('');
      setNewMediaAlt('');
      setNewMediaCaption('');
      setNewMediaLinkUrl('');

      // Reload
      await loadContentBlock(id);
    } catch (error) {
      console.error('Failed to add media:', error);
      alert('Failed to add media');
    }
  }

  async function handleRemoveMedia(mediaId: string) {
    if (!id || id === 'new') return;

    try {
      await removeContentBlockMedia(id, mediaId);
      await loadContentBlock(id);
    } catch (error) {
      console.error('Failed to remove media:', error);
      alert('Failed to remove media');
    }
  }

  const mediaColumns: ColumnDef<ContentBlockMedia>[] = [
    {
      id: 'preview',
      header: 'Preview',
      accessorFn: (row) => row.url,
      cell: (row) => (
        <div className="w-16 h-10 bg-gray-100 rounded overflow-hidden">
          {row.url ? (
            <img
              src={row.url}
              alt={row.alt || ''}
              className="w-full h-full object-cover"
              onError={(e) => {
                (e.target as HTMLImageElement).src = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 24 24" fill="none" stroke="%23999" stroke-width="2"%3E%3Crect x="3" y="3" width="18" height="18" rx="2" ry="2"/%3E%3Ccircle cx="8.5" cy="8.5" r="1.5"/%3E%3Cpolyline points="21 15 16 10 5 21"/%3E%3C/svg%3E';
              }}
            />
          ) : (
            <div className="w-full h-full flex items-center justify-center bg-gray-200">
              <Image className="w-6 h-6 text-gray-400" />
            </div>
          )}
        </div>
      ),
      sortable: false,
    },
    {
      id: 'url',
      header: 'URL',
      accessorKey: 'url',
      sortable: true,
    },
    {
      id: 'alt',
      header: 'Alt Text',
      accessorKey: 'alt',
      sortable: true,
    },
    {
      id: 'order',
      header: 'Order',
      accessorKey: 'order',
      sortable: true,
    },
  ];

  const mediaRowActions = (row: ContentBlockMedia) => (
    <DataTableRowActions
      actions={[
        {
          label: 'Remove',
          onClick: () => handleRemoveMedia(row.id),
          destructive: true,
        },
      ]}
    />
  );

  if (loading) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-[var(--color-brand-primary)]" />
      </div>
    );
  }

  return (
    <div className="flex-1 overflow-auto">
      <div className="p-6">
        {/* Page Header */}
        <div className="mb-6 flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">
              {isNew ? 'Create Content Block' : 'Edit Content Block'}
            </h1>
            <p className="text-[var(--color-text-secondary)]">
              {isNew
                ? 'Create a new content block for dynamic content management.'
                : `Editing: ${contentBlock?.title}`}
            </p>
          </div>
          <div className="flex gap-2">
            <Button variant="outline" onClick={() => navigate('/cms/content-blocks')}>
              <ArrowLeft className="w-4 h-4 mr-2" />
              Back
            </Button>
            <Button onClick={handleSave} disabled={saving}>
              <Save className="w-4 h-4 mr-2" />
              {saving ? 'Saving...' : 'Save'}
            </Button>
          </div>
        </div>

        <Tabs defaultValue="general" className="space-y-6">
          <TabsList>
            <TabsTrigger value="general">General</TabsTrigger>
            <TabsTrigger value="media" disabled={isNew}>
              Media ({contentBlock?.media.length || 0})
            </TabsTrigger>
          </TabsList>

          <TabsContent value="general">
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Layers className="w-5 h-5 text-[var(--color-brand-primary)]" />
                  Content Block Details
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-6">
                {/* Content Key */}
                <div className="space-y-2">
                  <Label htmlFor="contentKey">Content Key *</Label>
                  <Input
                    id="contentKey"
                    value={contentKey}
                    onChange={(e) => setContentKey(e.target.value)}
                    placeholder="e.g., myspace.banner"
                    disabled={!isNew}
                  />
                  <p className="text-xs text-[var(--color-text-secondary)]">
                    Unique identifier for this content block. Cannot be changed after creation.
                  </p>
                </div>

                {/* Title */}
                <div className="space-y-2">
                  <Label htmlFor="title">Title *</Label>
                  <Input
                    id="title"
                    value={title}
                    onChange={(e) => setTitle(e.target.value)}
                    placeholder="e.g., My Space Banner"
                  />
                </div>

                {/* Slug */}
                <div className="space-y-2">
                  <Label htmlFor="slug">Slug</Label>
                  <Input
                    id="slug"
                    value={slug}
                    onChange={(e) => setSlug(e.target.value)}
                    placeholder="URL-friendly identifier"
                  />
                </div>

                {/* Area */}
                <div className="space-y-2">
                  <Label htmlFor="area">Area *</Label>
                  <Select value={area} onValueChange={setArea}>
                    <SelectTrigger id="area">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {AREA_OPTIONS.map((option) => (
                        <SelectItem key={option} value={option}>
                          {option}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <p className="text-xs text-[var(--color-text-secondary)]">
                    Determines where this content block can be used in the application.
                  </p>
                </div>

                {/* Format */}
                <div className="space-y-2">
                  <Label htmlFor="format">Format *</Label>
                  <Select value={format} onValueChange={setFormat}>
                    <SelectTrigger id="format">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {FORMAT_OPTIONS.map((option) => (
                        <SelectItem key={option} value={option}>
                          {option}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                {/* Locale */}
                <div className="space-y-2">
                  <Label htmlFor="locale">Locale *</Label>
                  <Input
                    id="locale"
                    value={locale}
                    onChange={(e) => setLocale(e.target.value)}
                    placeholder="e.g., en"
                  />
                  <p className="text-xs text-[var(--color-text-secondary)]">
                    ISO language code (e.g., en, fr, es)
                  </p>
                </div>

                {/* Priority */}
                <div className="space-y-2">
                  <Label htmlFor="priority">Priority</Label>
                  <Input
                    id="priority"
                    type="number"
                    value={priority}
                    onChange={(e) => setPriority(parseInt(e.target.value) || 0)}
                  />
                  <p className="text-xs text-[var(--color-text-secondary)]">
                    Lower numbers display first when multiple blocks are in the same area.
                  </p>
                </div>

                {/* Body */}
                <div className="space-y-2">
                  <Label htmlFor="body">Body Content</Label>
                  <Textarea
                    id="body"
                    value={body}
                    onChange={(e) => setBody(e.target.value)}
                    placeholder="Markdown, HTML, or JSON content..."
                    rows={6}
                  />
                </div>

                {/* Enabled Switch */}
                <div className="flex items-center justify-between rounded-lg border border-[var(--color-border-light)] p-4">
                  <div className="space-y-0.5">
                    <Label htmlFor="isEnabled">Enabled</Label>
                    <p className="text-sm text-[var(--color-text-secondary)]">
                      Content block will be visible when enabled.
                    </p>
                  </div>
                  <Switch
                    id="isEnabled"
                    checked={isEnabled}
                    onCheckedChange={setIsEnabled}
                  />
                </div>
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="media">
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Image className="w-5 h-5 text-[var(--color-brand-primary)]" />
                  Media Library
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-6">
                {/* Add Media Form */}
                <div className="space-y-4 rounded-lg border border-[var(--color-border-light)] p-4">
                  <h3 className="font-medium">Add New Media</h3>
                  <div className="grid gap-4 md:grid-cols-2">
                    <div className="space-y-2">
                      <Label htmlFor="mediaUrl">Image URL *</Label>
                      <Input
                        id="mediaUrl"
                        value={newMediaUrl}
                        onChange={(e) => setNewMediaUrl(e.target.value)}
                        placeholder="/images/banners/banner-01.png"
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="mediaAlt">Alt Text</Label>
                      <Input
                        id="mediaAlt"
                        value={newMediaAlt}
                        onChange={(e) => setNewMediaAlt(e.target.value)}
                        placeholder="Accessibility description"
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="mediaCaption">Caption</Label>
                      <Input
                        id="mediaCaption"
                        value={newMediaCaption}
                        onChange={(e) => setNewMediaCaption(e.target.value)}
                        placeholder="Optional caption"
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="mediaLinkUrl">Link URL</Label>
                      <Input
                        id="mediaLinkUrl"
                        value={newMediaLinkUrl}
                        onChange={(e) => setNewMediaLinkUrl(e.target.value)}
                        placeholder="Click-through URL"
                      />
                    </div>
                  </div>
                  <Button
                    onClick={handleAddMedia}
                    disabled={!newMediaUrl.trim()}
                    className="gap-1.5"
                  >
                    <Plus className="w-4 h-4" />
                    Add Media
                  </Button>
                </div>

                {/* Media Table */}
                <div className="space-y-2">
                  <h3 className="font-medium">Current Media</h3>
                  <DataTable
                    data={contentBlock?.media || []}
                    columns={mediaColumns}
                    getRowId={(row) => row.id}
                    selectedIds={selectedMediaIds}
                    onSelectionChange={setSelectedMediaIds}
                    emptyIcon={<Image className="w-12 h-12" />}
                    emptyTitle="No media items"
                    emptyDescription="Add media items above to use in this content block."
                    rowActions={mediaRowActions}
                    showCheckboxes={false}
                  />
                </div>
              </CardContent>
            </Card>
          </TabsContent>
        </Tabs>
      </div>
    </div>
  );
}
