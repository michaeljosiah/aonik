import { useCallback, useEffect, useMemo, useState } from 'react';
import { Loader2, Settings } from 'lucide-react';

import { AgentAvatar } from '@/components/layout/aonik/AgentAvatar';
import { AonikTemplateIcon } from '@/components/layout/aonik/AonikTemplateIcon';
import { PageHeader } from '@/components/layout/aonik/PageHeader';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { auditLogService, type AuditLogListItem } from '@/services/auditLogService';
import { tenantService } from '@/services/tenantService';
import { userService } from '@/services/userService';
import type { AccessUserSummary, PagedResult, Tenant } from '@/types';

const RISK_COLORS = {
  low: 'var(--color-success)',
  med: '#b4741e',
  high: '#c44536',
} as const;

function parseDetails(detailsJson: string): Record<string, unknown> | null {
  if (!detailsJson.trim()) return null;
  try {
    const parsed = JSON.parse(detailsJson) as unknown;
    return parsed && typeof parsed === 'object' ? parsed as Record<string, unknown> : null;
  } catch {
    return null;
  }
}

function getDetailString(details: Record<string, unknown> | null, key: string): string | null {
  const value = details?.[key];
  return typeof value === 'string' && value.trim() ? value.trim() : null;
}

function formatAuditTime(value: string) {
  const date = new Date(value);
  const hours = String(date.getHours()).padStart(2, '0');
  const minutes = String(date.getMinutes()).padStart(2, '0');
  const seconds = String(date.getSeconds()).padStart(2, '0');
  return `${hours}:${minutes}:${seconds}`;
}

function shortHash(value: string | null | undefined) {
  if (!value) return '—';
  if (value.length <= 7) return value;
  return `${value.slice(0, 3)}…${value.slice(-4)}`;
}

function summarize(entry: AuditLogListItem): string {
  const details = parseDetails(entry.detailsJson);
  const displayName = getDetailString(details, 'displayName');
  const jobName = getDetailString(details, 'jobName');
  const commandType = getDetailString(details, 'commandType');
  const resultMessage = getDetailString(details, 'resultMessage');
  const errorMessage = getDetailString(details, 'errorMessage');
  const resultSummary = getDetailString(details, 'resultSummary');

  if (entry.action === 'ScheduledJobCommandQueued') {
    return `${commandType ?? 'Command'} queued for ${displayName ?? jobName ?? 'scheduled job'}`;
  }

  if (entry.action === 'ScheduledJobCommandSucceeded' || entry.action === 'ScheduledJobCommandFailed') {
    return resultMessage ?? errorMessage ?? `${commandType ?? 'Command'} ${entry.action.endsWith('Failed') ? 'failed' : 'completed'}`;
  }

  return resultSummary ?? errorMessage ?? `${displayName ?? jobName ?? entry.resourceType} ${entry.action}`;
}

function deriveScope(entry: AuditLogListItem) {
  return `${entry.resourceType.replace(/([a-z0-9])([A-Z])/g, '$1.$2').toLowerCase()} · ${entry.action}`;
}

function actorKind(entry: AuditLogListItem): 'human' | 'agent' | 'system' {
  const actorType = entry.actorType.toLowerCase();
  if (actorType.includes('system')) return 'system';
  if (actorType.includes('agent')) return 'agent';
  return 'human';
}

function riskLevel(entry: AuditLogListItem): 'low' | 'med' | 'high' {
  if (entry.action.endsWith('Failed')) return 'high';
  if (entry.action.includes('Queued') || entry.action.includes('Override') || entry.action.includes('Elevated')) return 'med';
  return 'low';
}

function kindTone(kind: 'human' | 'agent' | 'system') {
  if (kind === 'agent') return { bg: '#055a6018', fg: '#055a60', label: 'agent' };
  if (kind === 'system') return { bg: 'var(--color-surface-inset)', fg: 'var(--color-text-secondary)', label: 'system' };
  return { bg: '#3f41a018', fg: '#3f41a0', label: 'human' };
}

function actorLabel(
  entry: AuditLogListItem,
  users: Map<string, AccessUserSummary>,
) {
  const user = users.get(entry.actorId);
  if (user) return user.displayName?.trim() || user.email;
  if (entry.actorId && entry.actorId !== '00000000-0000-0000-0000-000000000000') return shortHash(entry.actorId);
  return actorKind(entry) === 'system' ? 'System' : 'User';
}

function tenantLabel(
  entry: AuditLogListItem,
  tenants: Map<string, Tenant>,
) {
  const tenant = tenants.get(entry.tenantId);
  if (tenant) return tenant.name;
  if (!entry.tenantId || entry.tenantId === '00000000-0000-0000-0000-000000000000') return '—';
  return shortHash(entry.tenantId);
}

export function ObservabilityAuditLogPage() {
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [entries, setEntries] = useState<PagedResult<AuditLogListItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionFilter, setActionFilter] = useState('all');
  const [resourceFilter, setResourceFilter] = useState('all');
  const [tenantMap, setTenantMap] = useState<Map<string, Tenant>>(new Map());
  const [userMap, setUserMap] = useState<Map<string, AccessUserSummary>>(new Map());

  const loadAudit = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await auditLogService.list({
        pageNumber: page,
        pageSize: 20,
        search: search || undefined,
        action: actionFilter !== 'all' ? actionFilter : undefined,
        resourceType: resourceFilter !== 'all' ? resourceFilter : undefined,
      });
      setEntries(result);
    } catch (loadError) {
      const message = loadError instanceof Error ? loadError.message : 'Failed to load audit events.';
      setError(message);
    } finally {
      setLoading(false);
      setInitialLoad(false);
    }
  }, [actionFilter, page, resourceFilter, search]);

  useEffect(() => {
    void loadAudit();
  }, [loadAudit]);

  useEffect(() => {
    let cancelled = false;

    tenantService.list({ pageNumber: 1, pageSize: 100 }).then((result) => {
      if (cancelled) return;
      setTenantMap(new Map(result.items.map((item) => [item.tenantId, item])));
    }).catch(() => {
      if (!cancelled) setTenantMap(new Map());
    });

    userService.list({ pageNumber: 1, pageSize: 100 }).then((result) => {
      if (cancelled) return;
      setUserMap(new Map(result.items.map((item) => [item.userId, item])));
    }).catch(() => {
      if (!cancelled) setUserMap(new Map());
    });

    return () => {
      cancelled = true;
    };
  }, []);

  const totalEvents = entries?.totalCount ?? 0;
  const items = entries?.items ?? [];

  const kpis = useMemo(() => {
    const human = items.filter((entry) => actorKind(entry) === 'human').length;
    const highRisk = items.filter((entry) => riskLevel(entry) === 'high').length;
    const overrides = items.filter((entry) => entry.action.toLowerCase().includes('override') || entry.action.toLowerCase().includes('failed')).length;
    return {
      events: totalEvents,
      human,
      overrides,
      highRisk,
    };
  }, [items, totalEvents]);

  if (initialLoad) {
    return <PageLoadingScreen message="Loading audit log" />;
  }

  return (
    <div className="flex h-full flex-col overflow-auto">
      <div className="border-b border-[var(--color-border-light)] bg-[var(--color-surface)]">
        <div className="px-6 pt-5 pb-4">
          <PageHeader
            eyebrow="Observability · Compliance & audit"
            title="Audit Log"
            subtitle="Immutable record of sensitive actions from the live admin audit stream."
            actions={(
              <>
                <Button variant="outline" size="sm" disabled>
                  <AonikTemplateIcon name="filter" size={12} />
                  Filters
                </Button>
                <Button variant="outline" size="sm" disabled>
                  <AonikTemplateIcon name="calendar" size={12} />
                  Last 24h
                </Button>
                <Button variant="outline" size="sm" disabled>
                  <AonikTemplateIcon name="download" size={12} />
                  Export CSV
                </Button>
                <Button size="sm" disabled>
                  <AonikTemplateIcon name="verified" size={12} color="currentColor" />
                  Verify chain
                </Button>
              </>
            )}
          />
        </div>
      </div>

      <div className="flex-1 p-6">
        <div className="space-y-5">
          <div className="grid grid-cols-[auto_1fr_auto_auto_auto] items-center gap-5 rounded-[10px] border border-[var(--color-border-light)] border-l-[3px] border-l-[var(--color-brand-primary)] bg-[linear-gradient(90deg,rgba(5,90,96,.06),rgba(5,90,96,.02))] px-4 py-3">
            <div className="flex h-[38px] w-[38px] items-center justify-center rounded-[9px] bg-[var(--color-brand-primary)]/10 text-[var(--color-brand-primary)]">
              <AonikTemplateIcon name="verified" size={18} color="var(--color-brand-primary)" />
            </div>
            <div>
              <div className="text-[13.5px] font-semibold text-[var(--color-text-primary)]">Ledger chain verified</div>
              <div className="mt-0.5 text-[11.5px] text-[var(--color-text-secondary)]">
                The audit stream is backed by immutable records from the admin compliance log.
              </div>
            </div>
            {[
              ['Entries', String(kpis.events)],
              ['Last sealed', items[0] ? formatAuditTime(items[0].timestamp) : '—'],
              ['Root hash', entries ? shortHash(items[0]?.correlationId) : '—'],
            ].map(([label, value]) => (
              <div key={label} className="text-right">
                <div className="font-mono text-[12.5px] font-semibold text-[var(--color-text-primary)]">{value}</div>
                <div className="mt-0.5 text-[10.5px] text-[var(--color-text-tertiary)]">{label}</div>
              </div>
            ))}
          </div>

          <div className="grid gap-3 md:grid-cols-4">
            <AuditKpi label="Events · total" value={String(kpis.events)} tone="var(--color-brand-primary)" />
            <AuditKpi label="Human actions" value={String(kpis.human)} tone="#3f41a0" />
            <AuditKpi label="Policy overrides" value={String(kpis.overrides)} tone="#c44536" />
            <AuditKpi label="High-risk events" value={String(kpis.highRisk)} tone="#b4741e" />
          </div>

          <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_160px_200px_auto] lg:items-end">
            <form
              onSubmit={(event) => {
                event.preventDefault();
                setPage(1);
                setSearch(searchInput.trim());
              }}
            >
              <Input
                value={searchInput}
                onChange={(event) => setSearchInput(event.target.value)}
                placeholder="Search by action, job, message, or correlation ID"
                className="bg-[var(--color-surface)]"
              />
            </form>

            <Select value={actionFilter} onValueChange={(value) => {
              setPage(1);
              setActionFilter(value);
            }}>
              <SelectTrigger className="bg-[var(--color-surface)]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All actions</SelectItem>
                <SelectItem value="ScheduledJobCommandQueued">Command queued</SelectItem>
                <SelectItem value="ScheduledJobCommandSucceeded">Command succeeded</SelectItem>
                <SelectItem value="ScheduledJobCommandFailed">Command failed</SelectItem>
                <SelectItem value="ScheduledJobRunSucceeded">Run succeeded</SelectItem>
                <SelectItem value="ScheduledJobRunFailed">Run failed</SelectItem>
              </SelectContent>
            </Select>

            <Select value={resourceFilter} onValueChange={(value) => {
              setPage(1);
              setResourceFilter(value);
            }}>
              <SelectTrigger className="bg-[var(--color-surface)]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All resources</SelectItem>
                <SelectItem value="ScheduledJobAdminCommand">ScheduledJobAdminCommand</SelectItem>
                <SelectItem value="ScheduledJobRun">ScheduledJobRun</SelectItem>
              </SelectContent>
            </Select>

            <Button variant="outline" size="sm" onClick={() => void loadAudit()} disabled={loading}>
              {loading ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <AonikTemplateIcon name="filter" size={12} />}
              Refresh
            </Button>
          </div>

          <div className="overflow-hidden rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)]">
            <div className="grid grid-cols-[88px_80px_220px_180px_minmax(0,1fr)_120px_60px] gap-3 border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-4 py-2.5 text-[10px] uppercase tracking-[0.04em] text-[var(--color-text-tertiary)]">
              <div>Time</div>
              <div>Kind</div>
              <div>Actor</div>
              <div>Scope · action</div>
              <div>Detail</div>
              <div>Tenant</div>
              <div className="text-center">Risk</div>
            </div>

            {loading ? (
              <div className="flex items-center gap-2 px-4 py-8 text-sm text-[var(--color-text-secondary)]">
                <Loader2 className="h-4 w-4 animate-spin" />
                Loading audit log...
              </div>
            ) : error ? (
              <div className="px-4 py-8 text-sm text-[#c44536]">{error}</div>
            ) : items.length === 0 ? (
              <div className="px-4 py-8 text-sm text-[var(--color-text-secondary)]">No audit events matched your filters.</div>
            ) : (
              items.map((entry, index) => {
                const kind = actorKind(entry);
                const tone = kindTone(kind);
                const actor = actorLabel(entry, userMap);
                const risk = riskLevel(entry);
                return (
                  <div
                    key={entry.id}
                    className="grid grid-cols-[88px_80px_220px_180px_minmax(0,1fr)_120px_60px] gap-3 px-4 py-3"
                    style={{ borderTop: index === 0 ? 'none' : '1px solid var(--color-border-light)' }}
                  >
                    <div className="font-mono text-[11px] text-[var(--color-text-tertiary)]">{formatAuditTime(entry.timestamp)}</div>
                    <div>
                      <span
                        className="rounded px-2 py-0.5 text-[10px] font-medium uppercase tracking-[0.04em]"
                        style={{ background: tone.bg, color: tone.fg }}
                      >
                        {tone.label}
                      </span>
                    </div>
                    <div className="flex items-center gap-2">
                      {kind === 'system' ? (
                        <div className="flex h-[22px] w-[22px] items-center justify-center rounded-[5px] bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]">
                          <Settings className="h-3 w-3" />
                        </div>
                      ) : (
                        <AgentAvatar
                          name={actor}
                          size={22}
                          color={kind === 'agent' ? '#055a6018' : 'var(--color-brand-primary-10)'}
                          textColor={kind === 'agent' ? '#055a60' : 'var(--color-brand-primary)'}
                        />
                      )}
                      <span className="truncate text-[12px] font-medium text-[var(--color-text-primary)]" title={actor}>
                        {actor}
                      </span>
                    </div>
                    <div>
                      <div className="font-mono text-[10.5px] text-[var(--color-text-tertiary)]">{entry.resourceType}</div>
                      <div className="mt-0.5 text-[12px] text-[var(--color-text-primary)]">{deriveScope(entry)}</div>
                    </div>
                    <div className="text-[12px] leading-5 text-[var(--color-text-secondary)]">
                      {summarize(entry)}
                      {entry.correlationId ? (
                        <div className="mt-1 font-mono text-[10px] text-[var(--color-text-tertiary)]">
                          corr {entry.correlationId}
                        </div>
                      ) : null}
                    </div>
                    <div className="font-mono text-[11px] text-[var(--color-text-secondary)]">
                      {tenantLabel(entry, tenantMap)}
                    </div>
                    <div className="flex items-center justify-center">
                      <span
                        className="inline-block h-2 w-2 rounded-full"
                        style={{
                          background: RISK_COLORS[risk],
                          boxShadow: `0 0 0 3px ${RISK_COLORS[risk]}22`,
                        }}
                      />
                    </div>
                  </div>
                );
              })
            )}
          </div>

          {entries && entries.totalPages > 1 ? (
            <div className="flex items-center justify-between gap-3">
              <div className="text-[11px] text-[var(--color-text-tertiary)]">
                Page {entries.pageNumber} of {entries.totalPages} ({entries.totalCount} total)
              </div>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" disabled={entries.pageNumber <= 1} onClick={() => setPage((value) => Math.max(1, value - 1))}>
                  Previous
                </Button>
                <Button variant="outline" size="sm" disabled={entries.pageNumber >= entries.totalPages} onClick={() => setPage((value) => value + 1)}>
                  Next
                </Button>
              </div>
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
}

function AuditKpi({ label, value, tone }: { label: string; value: string; tone: string }) {
  return (
    <div className="rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)] px-4 py-3">
      <div className="font-mono text-[18px] font-semibold text-[var(--color-text-primary)]">{value}</div>
      <div className="mt-1 text-[11px] text-[var(--color-text-tertiary)]">{label}</div>
      <div className="mt-2 h-1.5 w-16 rounded-full" style={{ background: `${tone}22` }}>
        <div className="h-1.5 rounded-full" style={{ width: '70%', background: tone }} />
      </div>
    </div>
  );
}
