import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { DataTable, type ColumnDef } from '@/components/ui/data-table';
import { DataTableRowActions } from '@/components/ui/data-table';
import { Plus, Layers, Image, CheckCircle, XCircle, Sparkles } from 'lucide-react';
import { getContentBlocks, deleteContentBlock, type ContentBlock } from '@/services/contentBlockService';

export function ContentBlocksListPage() {
  const navigate = useNavigate();
  const [contentBlocks, setContentBlocks] = useState<ContentBlock[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  useEffect(() => {
    loadContentBlocks();
  }, []);

  async function loadContentBlocks() {
    try {
      setLoading(true);
      const blocks = await getContentBlocks();
      setContentBlocks(blocks);
    } catch (error) {
      console.error('Failed to load content blocks:', error);
    } finally {
      setLoading(false);
    }
  }

  async function handleDelete(id: string) {
    if (!confirm('Are you sure you want to delete this content block?')) return;
    
    try {
      await deleteContentBlock(id);
      await loadContentBlocks();
    } catch (error) {
      console.error('Failed to delete content block:', error);
      alert('Failed to delete content block');
    }
  }

  const columns: ColumnDef<ContentBlock>[] = [
    {
      id: 'contentKey',
      header: 'Content Key',
      accessorKey: 'contentKey',
      sortable: true,
    },
    {
      id: 'title',
      header: 'Title',
      accessorKey: 'title',
      sortable: true,
    },
    {
      id: 'area',
      header: 'Area',
      accessorKey: 'area',
      sortable: true,
    },
    {
      id: 'format',
      header: 'Format',
      accessorKey: 'format',
      sortable: true,
    },
    {
      id: 'locale',
      header: 'Locale',
      accessorKey: 'locale',
      sortable: true,
    },
    {
      id: 'status',
      header: 'Status',
      accessorFn: (row) => row.isEnabled,
      cell: (row) => (
        <div className="flex items-center gap-2">
          {row.isEnabled ? (
            <>
              <CheckCircle className="w-4 h-4 text-[var(--color-success)]" />
              <span className="text-sm text-[var(--color-success)]">Enabled</span>
            </>
          ) : (
            <>
              <XCircle className="w-4 h-4 text-[var(--color-danger)]" />
              <span className="text-sm text-[var(--color-danger)]">Disabled</span>
            </>
          )}
        </div>
      ),
      sortable: true,
    },
    {
      id: 'priority',
      header: 'Priority',
      accessorKey: 'priority',
      sortable: true,
    },
    {
      id: 'media',
      header: 'Media',
      accessorFn: (row) => row.media.length,
      cell: (row) => (
        <div className="flex items-center gap-1">
          <Image className="w-4 h-4 text-[var(--color-text-secondary)]" />
          <span className="text-sm">{row.media.length}</span>
        </div>
      ),
      sortable: true,
    },
  ];

  const rowActions = (row: ContentBlock) => (
    <DataTableRowActions
      actions={[
        {
          label: 'Edit',
          onClick: () => navigate(`/cms/content-blocks/${row.id}`),
        },
        {
          label: 'Delete',
          onClick: () => handleDelete(row.id),
          variant: 'danger',
        },
      ]}
    />
  );

  return (
    <div className="flex-1 overflow-auto">
      <div className="p-6">
        {/* Page Header */}
        <div className="mb-6">
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Content Blocks</h1>
          <p className="text-[var(--color-text-secondary)]">
            Manage dynamic content blocks for your application including banners, heroes, and marketing content.
          </p>
        </div>

        {/* Content Blocks Table */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-4">
            <div className="flex items-center gap-3">
              <div className="p-2 rounded-md bg-[var(--color-brand-primary)]">
                <Layers className="w-5 h-5 text-white" />
              </div>
              <div>
                <CardTitle className="text-base font-semibold">Content Blocks</CardTitle>
                <p className="text-sm text-[var(--color-text-secondary)]">
                  {contentBlocks.length} content block{contentBlocks.length !== 1 ? 's' : ''}
                </p>
              </div>
            </div>
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                className="gap-1.5"
                onClick={() => navigate('/cms/content-wizard')}
              >
                <Sparkles className="w-4 h-4" />
                AI Wizard
              </Button>
              <Button
                variant="default"
                size="sm"
                className="gap-1.5"
                onClick={() => navigate('/cms/content-blocks/new')}
              >
                <Plus className="w-4 h-4" />
                Create Content Block
              </Button>
            </div>
          </CardHeader>
          <CardContent>
            <DataTable
              data={contentBlocks}
              columns={columns}
              getRowId={(row) => row.id}
              selectedIds={selectedIds}
              onSelectionChange={setSelectedIds}
              loading={loading}
              loadingMessage="Loading content blocks..."
              emptyIcon={<Layers className="w-12 h-12" />}
              emptyTitle="No content blocks found"
              emptyDescription="Create your first content block to get started with dynamic content management."
              rowActions={rowActions}
            />
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
