import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Badge } from '@/components/ui/badge';
import { DataTable, type ColumnDef } from '@/components/ui/data-table/data-table';
import { DataTableHeader, type ViewMode } from '@/components/ui/data-table/data-table-header';
import { DataTableRowActions } from '@/components/ui/data-table/data-table-row-actions';
import {
  AlertCircle,
  Bot,
  Settings2,
  RotateCcw,
  Check,
  X,
  Shield,
  Plus,
} from 'lucide-react';

import type { AgentConfigurationResponse } from '@/types/ai';
import { agentConfigService } from '@/services/aiService';
import { cn } from '@/lib/utils';

// ── Helpers ────────────────────────────────────────────────────────────

function parseToolsetIds(json: string): string[] {
  try { return JSON.parse(json || '[]'); } catch { return []; }
}

function agentTypeLabel(agentType: number): string {
  return agentType === 1 ? 'Orchestrator' : 'Sub-Agent';
}

const riskBadgeClass: Record<string, string> = {
  low: 'bg-[var(--color-success-light)] text-[var(--color-success)]',
  medium: 'bg-[var(--color-warning-light)] text-[var(--color-warning)]',
  high: 'bg-[var(--color-error-light)] text-[var(--color-error)]',
};

// ── Agent Avatar ───────────────────────────────────────────────────────

function AgentAvatarIcon({ iconUrl, size = 85 }: { iconUrl?: string | null; size?: number }) {
  return (
    <div
      style={{ width: size, height: size }}
      className="rounded-full bg-gray-200 border-2 border-gray-300 flex items-center justify-center overflow-hidden"
    >
      {iconUrl ? (
        <img src={iconUrl} alt="" style={{ width: size, height: size }} className="object-cover" />
      ) : (
        <Bot size={Math.round(size * 0.54)} color="#878295" strokeWidth={1.5} />
      )}
    </div>
  );
}

// ── Centrali-style Agent Card ──────────────────────────────────────────

interface AgentCentraliCardProps {
  config: AgentConfigurationResponse;
  onConfigure: () => void;
  onDeleteOverride?: () => void;
}

function AgentCentraliCard({ config, onConfigure, onDeleteOverride }: AgentCentraliCardProps) {
  const tools = parseToolsetIds(config.toolsetIdsJson);
  const riskStyle = riskBadgeClass[config.riskTier?.toLowerCase() ?? 'low'] ?? riskBadgeClass.low;

  const menuActions = [
    {
      label: 'Configure',
      icon: <Settings2 className="w-4 h-4" />,
      onClick: onConfigure,
    },
    ...(config.isOverride && onDeleteOverride
      ? [{
          label: 'Remove Override',
          icon: <RotateCcw className="w-4 h-4" />,
          onClick: onDeleteOverride,
          variant: 'danger' as const,
        }]
      : []),
  ];

  return (
    /* Wrapper: provides top padding to accommodate the floating avatar */
    <div className="relative pt-[3rem] min-w-[320px]">
      {/* Floating avatar */}
      <div className="absolute top-0 left-6 z-10">
        <AgentAvatarIcon iconUrl={config.iconUrl} size={85} />
      </div>

      {/* Card */}
      <Card
        className={cn(
          'h-full flex flex-col overflow-hidden cursor-pointer',
          'shadow-md border border-[var(--color-border-light)]',
          'transition-all duration-300 hover:scale-[1.02]',
          'hover:border-[var(--color-brand-primary)] hover:shadow-lg',
        )}
        onClick={onConfigure}
      >
        {/* ── Top section — pt-10 clears the ~37px avatar overhang ── */}
        <div className="px-5 pt-10 pb-3 relative">
          {/* Actions menu — top-right, stop propagation */}
          <div
            className="absolute top-3 right-3 z-[3]"
            onClick={(e) => e.stopPropagation()}
          >
            <DataTableRowActions actions={menuActions} />
          </div>

          {/* Name row */}
          <div className="flex items-center gap-1.5 mt-1 pr-8">
            <h3 className="font-bold text-[var(--color-text-heading)] text-[16px] line-clamp-1">
              {config.name}
            </h3>
            {config.isOverride && (
              <span className="px-1.5 py-0.5 rounded text-[9px] font-semibold bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)] uppercase tracking-wide flex-shrink-0">
                override
              </span>
            )}
          </div>

          {/* Description */}
          <p className="text-[13px] text-gray-500 line-clamp-2 min-h-[36px] mt-1 leading-[1.5]">
            {config.description || 'No description provided.'}
          </p>
        </div>

        {/* ── Bottom section ──────────────────────────────── */}
        <div className="bg-[#f6f6f9] flex flex-col flex-1 px-6 pt-5 pb-5 rounded-b-lg">
          {/* Metadata grid: TYPE | DOMAIN */}
          <div className="grid grid-cols-2 gap-x-6 gap-y-3 mb-4">
            <div>
              <p className="font-bold text-[10px] uppercase tracking-wider text-[var(--color-text-heading)] mb-1">
                Type
              </p>
              <Badge
                variant={config.agentType === 1 ? 'enterprise' : 'team'}
                className="text-[11px] font-semibold"
              >
                {agentTypeLabel(config.agentType)}
              </Badge>
            </div>
            <div>
              <p className="font-bold text-[10px] uppercase tracking-wider text-[var(--color-text-heading)] mb-1">
                Domain
              </p>
              <span className="font-semibold text-[14px] text-gray-800">
                {config.domain || '—'}
              </span>
            </div>
          </div>

          {/* Risk tier + active status */}
          <div className="grid grid-cols-2 gap-x-6 gap-y-3 mb-4">
            <div>
              <p className="font-bold text-[10px] uppercase tracking-wider text-[var(--color-text-heading)] mb-1">
                Risk
              </p>
              <span className={cn('inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-medium', riskStyle)}>
                <Shield className="w-3 h-3" />
                {config.riskTier ?? 'low'}
              </span>
            </div>
            <div>
              <p className="font-bold text-[10px] uppercase tracking-wider text-[var(--color-text-heading)] mb-1">
                Status
              </p>
              {config.isActive ? (
                <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-medium bg-[var(--color-success-light)] text-[var(--color-success)]">
                  <Check className="w-3 h-3" /> Active
                </span>
              ) : (
                <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-medium bg-[var(--color-surface-inset)] text-[var(--color-text-tertiary)]">
                  <X className="w-3 h-3" /> Inactive
                </span>
              )}
            </div>
          </div>

          {/* Tools pills */}
          {tools.length > 0 && (
            <div className="mb-4">
              <p className="font-bold text-[10px] uppercase tracking-wider text-[var(--color-text-heading)] mb-1.5">
                Tools
              </p>
              <div className="flex flex-wrap gap-1.5">
                {tools.slice(0, 3).map((tool) => (
                  <span
                    key={tool}
                    className="bg-[#e2e1e8] text-[#3f3b47] px-2.5 py-1 rounded-full text-[11px] font-medium"
                  >
                    {tool}
                  </span>
                ))}
                {tools.length > 3 && (
                  <span className="bg-[var(--color-brand-primary)]/10 text-[var(--color-brand-primary)] px-2.5 py-1 rounded-full text-[11px] font-medium">
                    +{tools.length - 3}
                  </span>
                )}
              </div>
            </div>
          )}

          {/* Configure button */}
          <Button
            variant="default"
            className="w-full mt-1 gap-2 rounded-sm"
            onClick={(e) => {
              e.stopPropagation();
              onConfigure();
            }}
          >
            <Settings2 className="w-4 h-4" />
            Configure Agent
          </Button>
        </div>
      </Card>
    </div>
  );
}

// ── Main Page ──────────────────────────────────────────────────────────

export function AgentConfigPage() {
  const navigate = useNavigate();

  const [configs, setConfigs] = useState<AgentConfigurationResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [viewMode, setViewMode] = useState<ViewMode>('grid');
  const [deleteConfirm, setDeleteConfirm] = useState<AgentConfigurationResponse | null>(null);
  const requestIdRef = useRef(0);

  // ── Data loading ─────────────────────────────────────────────────────

  const loadData = useCallback(async () => {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setLoading(true);
    setError(null);
    try {
      const configList = await agentConfigService.list();
      if (requestIdRef.current !== requestId) return;
      setConfigs(configList);
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) return;
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load agent configurations. Please try again.');
    } finally {
      if (requestIdRef.current === requestId) setLoading(false);
    }
  }, []);

  useEffect(() => { loadData(); }, [loadData]);

  // ── Filtering ────────────────────────────────────────────────────────

  const filtered = configs.filter((c) => {
    if (!searchQuery) return true;
    const q = searchQuery.toLowerCase();
    return (
      c.name.toLowerCase().includes(q) ||
      c.domain.toLowerCase().includes(q) ||
      c.description.toLowerCase().includes(q)
    );
  });

  // Prefer tenant override per agent name
  const uniqueAgents = new Map<string, AgentConfigurationResponse>();
  for (const config of filtered) {
    const existing = uniqueAgents.get(config.name);
    if (!existing || config.isOverride) {
      uniqueAgents.set(config.name, config);
    }
  }
  const displayConfigs = Array.from(uniqueAgents.values());

  // ── Actions ──────────────────────────────────────────────────────────

  const handleDeleteOverride = async (config: AgentConfigurationResponse) => {
    try {
      await agentConfigService.delete(config.name);
      setDeleteConfirm(null);
      await loadData();
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to remove agent override.');
    }
  };

  const confirmDeleteOverride = (config: AgentConfigurationResponse) => {
    if (!config.isOverride) return;
    setDeleteConfirm(config);
  };

  // ── List view columns ─────────────────────────────────────────────────

  const columns: ColumnDef<AgentConfigurationResponse>[] = [
    {
      id: 'name',
      header: 'Agent',
      sortable: true,
      accessorKey: 'name',
      className: 'max-w-[400px]',
      cell: (row) => (
        <div className="flex flex-col min-w-0">
          <div className="flex items-center gap-1.5">
            <span className="font-semibold text-sm text-[var(--color-text-primary)] truncate">{row.name}</span>
            {row.isOverride && (
              <span className="px-1.5 py-0.5 rounded text-[9px] font-semibold bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)] uppercase tracking-wide flex-shrink-0">
                override
              </span>
            )}
          </div>
          <span className="text-xs text-[var(--color-text-tertiary)] truncate">{row.description}</span>
        </div>
      ),
    },
    {
      id: 'agentType',
      header: 'Type',
      sortable: false,
      accessorKey: 'agentType',
      cell: (row) => (
        <Badge variant={row.agentType === 1 ? 'enterprise' : 'team'} className="text-xs whitespace-nowrap">
          {agentTypeLabel(row.agentType)}
        </Badge>
      ),
    },
    {
      id: 'domain',
      header: 'Domain',
      sortable: true,
      accessorKey: 'domain',
      cell: (row) => (
        <span className="text-sm text-[var(--color-text-primary)] whitespace-nowrap">{row.domain || '—'}</span>
      ),
    },
    {
      id: 'riskTier',
      header: 'Risk',
      sortable: true,
      accessorKey: 'riskTier',
      cell: (row) => {
        const style = riskBadgeClass[row.riskTier?.toLowerCase() ?? 'low'] ?? riskBadgeClass.low;
        return (
          <span className={cn('inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium', style)}>
            <Shield className="w-3 h-3" />
            {row.riskTier ?? 'low'}
          </span>
        );
      },
    },
    {
      id: 'isActive',
      header: 'Status',
      sortable: false,
      accessorKey: 'isActive',
      cell: (row) =>
        row.isActive ? (
          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-[var(--color-success-light)] text-[var(--color-success)]">
            <Check className="w-3 h-3" /> Active
          </span>
        ) : (
          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-[var(--color-surface-inset)] text-[var(--color-text-tertiary)]">
            <X className="w-3 h-3" /> Inactive
          </span>
        ),
    },
  ];

  // ── Breadcrumb ───────────────────────────────────────────────────────

  const breadcrumbItems = [
    { label: 'AI & Agents', href: '/ai' },
    { label: 'Agents', icon: <Bot className="w-3.5 h-3.5" /> },
  ];

  // ── Render ───────────────────────────────────────────────────────────

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Agents</h1>
          <p className="text-[var(--color-text-secondary)]">
            Configure domain agents, assign models, and manage tenant overrides.
          </p>
        </div>
      </div>

      {/* Error banner */}
      {error && (
        <div className="mb-6 flex items-center gap-3 px-4 py-3 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] text-[var(--color-error)]">
          <AlertCircle className="w-5 h-5 flex-shrink-0" />
          <span className="text-sm flex-1">{error}</span>
          <Button variant="outline" size="sm" onClick={loadData}>Retry</Button>
        </div>
      )}

      {/* Delete override confirmation */}
      {deleteConfirm && (
        <div className="mb-6 flex items-center gap-3 px-4 py-3 rounded-md border border-[var(--color-warning)] bg-[var(--color-warning-light)] text-[var(--color-warning)]">
          <RotateCcw className="w-5 h-5 flex-shrink-0" />
          <span className="text-sm flex-1">
            Remove tenant override for <strong>{deleteConfirm.name}</strong>? The agent will revert to the global default.
          </span>
          <Button
            variant="outline"
            size="sm"
            className="border-[var(--color-warning)] text-[var(--color-warning)] hover:bg-[var(--color-warning-light)]"
            onClick={() => handleDeleteOverride(deleteConfirm)}
          >
            Remove
          </Button>
          <Button variant="ghost" size="sm" onClick={() => setDeleteConfirm(null)}>Cancel</Button>
        </div>
      )}

      {/* ── Outer content card ─────────────────────────────────────── */}
      <Card className="overflow-visible">
        {/* Toolbar — inside card header */}
        <DataTableHeader
          searchValue={searchQuery}
          onSearchChange={setSearchQuery}
          searchPlaceholder="Search agents..."
          viewMode={viewMode}
          onViewModeChange={setViewMode}
          showViewToggle
          actions={
            <Button size="sm" className="gap-1.5" onClick={() => navigate('/ai/playground')}>
              <Plus className="w-4 h-4" />
              New Agent
            </Button>
          }
        />

        {/* Content area */}
        {loading && configs.length === 0 ? (
          <div className="flex items-center justify-center py-20 text-[var(--color-text-tertiary)]">
            <span className="text-sm">Loading agents...</span>
          </div>
        ) : displayConfigs.length === 0 ? (
          /* Empty state */
          <div className="text-center py-16">
            <div className="text-4xl mb-3">🤖</div>
            <h3 className="text-lg font-semibold text-[var(--color-text-primary)] mb-1">
              {searchQuery ? 'No agents found' : 'No agents configured'}
            </h3>
            <p className="text-sm text-[var(--color-text-secondary)]">
              {searchQuery
                ? 'Try adjusting your search terms.'
                : 'Agents will appear here once they are registered in the system.'}
            </p>
          </div>
        ) : viewMode === 'grid' ? (
          /* ── Grid view ──────────────────────────────────────────── */
          /* CSS grid for equal-height cards; pt-14 clears first-row floating avatars */
          <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4 gap-x-6 gap-y-14 px-6 pt-14 pb-8 overflow-visible">
            {displayConfigs.map((config) => (
              <AgentCentraliCard
                key={config.id}
                config={config}
                onConfigure={() => navigate(`/ai/agents/${config.name}`)}
                onDeleteOverride={config.isOverride ? () => confirmDeleteOverride(config) : undefined}
              />
            ))}
          </div>
        ) : (
          /* ── List view ──────────────────────────────────────────── */
          <DataTable
            data={displayConfigs}
            columns={columns}
            getRowId={(row) => row.id}
            onRowClick={(row) => navigate(`/ai/agents/${row.name}`)}
            showCheckboxes={false}
            loading={loading}
            loadingMessage="Loading agents..."
            emptyIcon={<Bot className="w-10 h-10" />}
            emptyTitle="No agents found"
            emptyDescription={searchQuery ? 'Try adjusting your search.' : 'Agents will appear here once registered.'}
            rowIcon={(row) => (
              <div className="pl-3">
                <AgentAvatarIcon iconUrl={row.iconUrl} size={32} />
              </div>
            )}
            rowActions={(row) => (
              <DataTableRowActions
                actions={[
                  {
                    label: 'Configure',
                    icon: <Settings2 className="w-4 h-4" />,
                    onClick: () => navigate(`/ai/agents/${row.name}`),
                  },
                  ...(row.isOverride
                    ? [{
                        label: 'Remove Override',
                        icon: <RotateCcw className="w-4 h-4" />,
                        onClick: () => confirmDeleteOverride(row),
                        variant: 'danger' as const,
                      }]
                    : []),
                ]}
              />
            )}
          />
        )}
      </Card>
    </div>
  );
}
