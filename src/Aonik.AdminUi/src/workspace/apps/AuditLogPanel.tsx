import { useCallback, useEffect, useState } from 'react';
import { RefreshCw, ScrollText, LinkIcon } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { auditLogService, type AuditLogListItem } from '@/services/auditLogService';
import type { PagedResult } from '@/types';
import type { WorkspacePanelRenderProps, WorkspaceEvent } from '../types';
import { useWorkspaceEvents } from '../useWorkspace';

// ---------------------------------------------------------------------------
// Constants & helpers
// ---------------------------------------------------------------------------

const scheduledJobActions = [
  'ScheduledJobCommandQueued',
  'ScheduledJobCommandSucceeded',
  'ScheduledJobCommandFailed',
  'ScheduledJobRunSucceeded',
  'ScheduledJobRunFailed',
] as const;

function formatDateTime(isoDate: string) {
  return new Date(isoDate).toLocaleString();
}

function resultVariant(action: string): 'success' | 'warning' | 'error' | 'outline' {
  if (action.endsWith('Failed')) return 'error';
  if (action.endsWith('Queued')) return 'warning';
  if (action.endsWith('Succeeded')) return 'success';
  return 'outline';
}

function parseDetails(detailsJson: string): Record<string, unknown> | null {
  if (!detailsJson.trim()) return null;
  try {
    const parsed = JSON.parse(detailsJson) as unknown;
    return parsed && typeof parsed === 'object' ? (parsed as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}

function getDetailString(details: Record<string, unknown> | null, key: string): string | null {
  const value = details?.[key];
  return typeof value === 'string' && value.trim() ? value.trim() : null;
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
    return `${commandType ?? 'Command'} queued for ${displayName ?? jobName ?? 'scheduled job'}.`;
  }
  if (entry.action === 'ScheduledJobCommandSucceeded' || entry.action === 'ScheduledJobCommandFailed') {
    return resultMessage ?? errorMessage ?? `${commandType ?? 'Command'} ${entry.action.endsWith('Failed') ? 'failed' : 'completed'}.`;
  }
  return resultSummary ?? errorMessage ?? `${displayName ?? jobName ?? 'Scheduled job'} ${entry.action.endsWith('Failed') ? 'failed' : 'completed'}.`;
}

function metadataLine(entry: AuditLogListItem): string {
  const details = parseDetails(entry.detailsJson);
  const jobName = getDetailString(details, 'jobName');
  const commandType = getDetailString(details, 'commandType');
  const fireInstanceId = getDetailString(details, 'fireInstanceId');
  return [entry.resourceType, jobName, commandType, fireInstanceId]
    .filter((v): v is string => Boolean(v))
    .join(' \u00B7 ');
}

// ---------------------------------------------------------------------------
// Panel
// ---------------------------------------------------------------------------

export function AuditLogPanel({ panelId, title }: WorkspacePanelRenderProps) {
  const { onEvent } = useWorkspaceEvents(panelId);

  // Linked context from jobs panel
  const [linkedJob, setLinkedJob] = useState<string | null>(null);
  const [linkedJobDisplay, setLinkedJobDisplay] = useState<string | null>(null);

  // Filters
  const [search, setSearch] = useState('');
  const [actionFilter, setActionFilter] = useState('all');
  const [resourceTypeFilter, setResourceTypeFilter] = useState('all');
  const [pageNumber, setPageNumber] = useState(1);

  // Data
  const [entries, setEntries] = useState<PagedResult<AuditLogListItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

  // ── Context subscription ───────────────────────────────────────────

  useEffect(() => {
    const unsub1 = onEvent('job:selected', (event: WorkspaceEvent) => {
      const jobName = event.payload?.jobName as string | undefined;
      const displayName = event.payload?.displayName as string | undefined;
      if (jobName) {
        setLinkedJob(jobName);
        setLinkedJobDisplay(displayName ?? jobName);
        setSearch(jobName);
        setActionFilter('all');
        setResourceTypeFilter('all');
        setPageNumber(1);
      }
    });

    const unsub2 = onEvent('job:audit-requested', (event: WorkspaceEvent) => {
      const jobName = event.payload?.jobName as string | undefined;
      const displayName = event.payload?.displayName as string | undefined;
      const action = event.payload?.action as string | undefined;
      const resourceType = event.payload?.resourceType as string | undefined;
      if (jobName) {
        setLinkedJob(jobName);
        setLinkedJobDisplay(displayName ?? jobName);
        setSearch(jobName);
        setActionFilter(action ?? 'all');
        setResourceTypeFilter(resourceType ?? 'all');
        setPageNumber(1);
      }
    });

    return () => {
      unsub1();
      unsub2();
    };
  }, [onEvent]);

  // ── Data loading ───────────────────────────────────────────────────

  const loadEntries = useCallback(async () => {
    setLoading(true);
    try {
      const result = await auditLogService.list({
        pageNumber,
        pageSize: 15,
        search: search || undefined,
        action: actionFilter !== 'all' ? actionFilter : undefined,
        resourceType: resourceTypeFilter !== 'all' ? resourceTypeFilter : undefined,
      });
      setEntries(result);
    } catch {
      setEntries(null);
    } finally {
      setLoading(false);
    }
  }, [pageNumber, search, actionFilter, resourceTypeFilter]);

  useEffect(() => {
    void loadEntries();
  }, [loadEntries]);

  const handleRefresh = async () => {
    setRefreshing(true);
    await loadEntries();
    setRefreshing(false);
  };

  const clearLink = () => {
    setLinkedJob(null);
    setLinkedJobDisplay(null);
    setSearch('');
    setActionFilter('all');
    setResourceTypeFilter('all');
    setPageNumber(1);
  };

  // ── Render ─────────────────────────────────────────────────────────

  return (
    <div className="h-full overflow-auto p-4 space-y-3">
      {/* Header */}
      <div className="flex items-start justify-between gap-2">
        <div>
          <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">{title}</h2>
          <p className="text-xs text-[var(--color-text-secondary)]">
            Audit trail for scheduled job runs and admin commands.
          </p>
        </div>
        <Button
          variant="secondary"
          size="sm"
          onClick={() => void handleRefresh()}
          disabled={refreshing || loading}
        >
          <RefreshCw className={`w-3.5 h-3.5 ${refreshing ? 'animate-spin' : ''}`} />
        </Button>
      </div>

      {/* Linked context indicator */}
      {linkedJob && (
        <Card className="p-3 flex items-center justify-between gap-2">
          <div className="flex items-center gap-2 min-w-0">
            <LinkIcon className="w-3.5 h-3.5 text-[var(--color-brand-primary)] shrink-0" />
            <div className="min-w-0">
              <p className="text-xs text-[var(--color-text-tertiary)]">Linked from workspace</p>
              <p className="text-sm font-semibold text-[var(--color-text-primary)] truncate">
                {linkedJobDisplay}
              </p>
            </div>
          </div>
          <div className="flex items-center gap-1.5 shrink-0">
            <Badge variant="success" className="text-[10px] px-1.5 py-0">In focus</Badge>
            <Button size="sm" variant="ghost" className="h-5 px-1 text-[10px]" onClick={clearLink}>
              Clear
            </Button>
          </div>
        </Card>
      )}

      {/* Compact filters */}
      <div className="flex flex-wrap gap-2">
        <form
          className="flex-1 min-w-[120px]"
          onSubmit={(e) => {
            e.preventDefault();
            setPageNumber(1);
            void loadEntries();
          }}
        >
          <Input
            placeholder="Search..."
            className="h-7 text-xs"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </form>
        <Select value={actionFilter} onValueChange={(v) => { setActionFilter(v); setPageNumber(1); }}>
          <SelectTrigger className="h-7 w-[140px] text-xs">
            <SelectValue placeholder="Action" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All actions</SelectItem>
            {scheduledJobActions.map((a) => (
              <SelectItem key={a} value={a}>{a.replace('ScheduledJob', '').replace(/([A-Z])/g, ' $1').trim()}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={resourceTypeFilter} onValueChange={(v) => { setResourceTypeFilter(v); setPageNumber(1); }}>
          <SelectTrigger className="h-7 w-[120px] text-xs">
            <SelectValue placeholder="Resource" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All</SelectItem>
            <SelectItem value="ScheduledJobAdminCommand">Command</SelectItem>
            <SelectItem value="ScheduledJobRun">Run</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* Results */}
      {loading ? (
        <p className="text-xs text-[var(--color-text-tertiary)] py-6 text-center">Loading audit events...</p>
      ) : !entries || entries.items.length === 0 ? (
        <div className="py-6 text-center">
          <ScrollText className="mx-auto h-8 w-8 text-[var(--color-text-tertiary)] mb-2" />
          <p className="text-xs text-[var(--color-text-tertiary)]">
            {linkedJob ? `No audit events found for ${linkedJobDisplay}.` : 'No audit events matched your filters.'}
          </p>
        </div>
      ) : (
        <div className="space-y-2">
          {entries.items.map((entry) => {
            const details = parseDetails(entry.detailsJson);
            const errorMessage = getDetailString(details, 'errorMessage');
            const resultSummary = getDetailString(details, 'resultSummary');

            return (
              <div
                key={entry.id}
                className="rounded-md border border-[var(--color-border-light)] px-3 py-2.5"
              >
                <div className="space-y-1.5">
                  {/* Summary + badge */}
                  <div className="flex flex-wrap items-center gap-1.5">
                    <p className="text-xs font-medium text-[var(--color-text-primary)] line-clamp-1">
                      {summarize(entry)}
                    </p>
                    <Badge variant={resultVariant(entry.action)} className="text-[10px] px-1.5 py-0">
                      {entry.action.replace('ScheduledJob', '')}
                    </Badge>
                  </div>

                  {/* Metadata */}
                  <p className="text-[10px] text-[var(--color-text-tertiary)] break-all">
                    {metadataLine(entry)}
                  </p>

                  {/* Error / result */}
                  {(errorMessage || resultSummary) && (
                    <div className="rounded-sm bg-[var(--color-surface-inset)] p-2 text-[10px] text-[var(--color-text-secondary)] whitespace-pre-wrap break-words line-clamp-3">
                      {errorMessage ?? resultSummary}
                    </div>
                  )}

                  {/* Timestamp + correlation */}
                  <div className="flex items-center justify-between text-[10px] text-[var(--color-text-tertiary)]">
                    <span>{formatDateTime(entry.timestamp)}</span>
                    {entry.correlationId && (
                      <span className="font-mono truncate max-w-[120px]" title={entry.correlationId}>
                        {entry.correlationId.slice(0, 8)}...
                      </span>
                    )}
                  </div>

                  {/* Expandable raw details */}
                  {entry.detailsJson.trim() && (
                    <details className="text-[10px] text-[var(--color-text-tertiary)]">
                      <summary className="cursor-pointer select-none">Raw details</summary>
                      <pre className="mt-1 overflow-auto rounded-sm bg-[var(--color-surface-inset)] p-2 whitespace-pre-wrap break-words max-h-32">
                        {entry.detailsJson}
                      </pre>
                    </details>
                  )}
                </div>
              </div>
            );
          })}

          {/* Pagination */}
          {entries.totalPages > 1 && (
            <div className="flex items-center justify-between pt-1">
              <span className="text-[10px] text-[var(--color-text-tertiary)]">
                Page {entries.pageNumber} of {entries.totalPages}
              </span>
              <div className="flex gap-1">
                <Button
                  size="sm"
                  variant="outline"
                  className="h-5 px-2 text-[10px]"
                  disabled={entries.pageNumber <= 1}
                  onClick={() => setPageNumber(entries.pageNumber - 1)}
                >
                  Prev
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  className="h-5 px-2 text-[10px]"
                  disabled={entries.pageNumber >= entries.totalPages}
                  onClick={() => setPageNumber(entries.pageNumber + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
