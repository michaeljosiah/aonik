import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Input } from '@/components/ui/input';
import {
  AlertCircle,
  Bot,
  Pencil,
  RotateCcw,
} from 'lucide-react';

import { AgentCard } from '@/components/dashboard/AgentCard';
import type { AgentCard as AgentCardType } from '@/types';
import type { AgentConfigurationResponse } from '@/types/ai';
import { agentConfigService } from '@/services/aiService';

function mapAgentConfigToCard(cfg: AgentConfigurationResponse): AgentCardType {
  let toolsetIds: string[] = [];
  try { toolsetIds = JSON.parse(cfg.toolsetIdsJson || '[]'); } catch { /* ignore */ }
  return {
    id: cfg.id,
    name: cfg.name,
    description: cfg.description || 'No description.',
    avatar: cfg.iconUrl ?? undefined,
    visibility: 'team',
    source: cfg.domain || 'Agent',
    skills: [],
    plugins: toolsetIds,
    riskTier: (cfg.riskTier as 'low' | 'medium' | 'high') ?? 'low',
    isActive: cfg.isActive,
    isOverride: cfg.isOverride,
    modelName: cfg.modelName ?? (cfg.modelId ? `ID: ${cfg.modelId.slice(0, 8)}...` : null),
  };
}

export function AgentConfigPage() {
  const navigate = useNavigate();

  // ── State ──────────────────────────────────────────────────────────
  const [configs, setConfigs] = useState<AgentConfigurationResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const requestIdRef = useRef(0);

  // ── Data loading ───────────────────────────────────────────────────

  const loadData = useCallback(async () => {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setLoading(true);
    setError(null);

    try {
      const configList = await agentConfigService.list();

      if (requestIdRef.current !== requestId) return;

      setConfigs(configList);
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

  // ── Render ─────────────────────────────────────────────────────────

  const breadcrumbItems = [
    { label: 'AI & Agents', href: '/ai' },
    { label: 'Agents', icon: <Bot className="w-3.5 h-3.5" /> },
  ];

  const handleChatAgent = (agentId: string) => {
    void agentId;
    navigate('/ai/chat');
  };

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
        <div className="grid grid-cols-1 gap-5 md:grid-cols-2 xl:grid-cols-3">
          {displayConfigs.map((config) => {
            const agentCard = mapAgentConfigToCard(config);

            return (
              <AgentCard
                key={config.id}
                agent={agentCard}
                showConfigMeta
                onClick={() => navigate(`/ai/agents/${config.name}`)}
                onChat={handleChatAgent}
                actions={
                  <div className="flex items-center gap-0.5">
                    <Button
                      variant="ghost"
                      size="icon-sm"
                      className="h-6 w-6 text-[var(--color-text-tertiary)]"
                      onClick={(e) => { e.stopPropagation(); navigate(`/ai/agents/${config.name}`); }}
                    >
                      <Pencil className="w-3.5 h-3.5" />
                    </Button>
                    {config.isOverride && (
                      <Button
                        variant="ghost"
                        size="icon-sm"
                        className="h-6 w-6 text-[var(--color-warning)]"
                        onClick={(e) => { e.stopPropagation(); deleteOverride(config); }}
                        title="Delete override (revert to global)"
                      >
                        <RotateCcw className="w-3.5 h-3.5" />
                      </Button>
                    )}
                  </div>
                }
              />
            );
          })}
        </div>
      )}

    </div>
  );
}
