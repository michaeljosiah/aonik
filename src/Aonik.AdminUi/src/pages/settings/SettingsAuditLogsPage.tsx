import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Cog, RefreshCw, ScrollText } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { auditLogService, type AuditLogListItem } from '@/services/auditLogService';
import type { PagedResult } from '@/types';

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
    return parsed && typeof parsed === 'object' ? parsed as Record<string, unknown> : null;
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
    .filter((value): value is string => Boolean(value))
    .join(' · ');
}

export function SettingsAuditLogsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [entries, setEntries] = useState<PagedResult<AuditLogListItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [searchInput, setSearchInput] = useState(searchParams.get('search') ?? '');

  const pageNumber = Number(searchParams.get('pageNumber') ?? '1');
  const action = searchParams.get('action') ?? 'all';
  const resourceType = searchParams.get('resourceType') ?? 'all';
  const correlationId = searchParams.get('correlationId') ?? '';

  const loadEntries = useCallback(async () => {
    setLoading(true);

    try {
      const result = await auditLogService.list({
        pageNumber,
        pageSize: 20,
        search: searchParams.get('search') ?? undefined,
        action: action !== 'all' ? action : undefined,
        resourceType: resourceType !== 'all' ? resourceType : undefined,
        correlationId: correlationId || undefined,
      });
      setEntries(result);
    } catch (error) {
      console.error('Failed to load audit logs:', error);
      setEntries(null);
    } finally {
      setLoading(false);
    }
  }, [action, correlationId, pageNumber, resourceType, searchParams]);

  useEffect(() => {
    void loadEntries();
  }, [loadEntries]);

  const updateFilters = (updates: Record<string, string | null>) => {
    const next = new URLSearchParams(searchParams);

    Object.entries(updates).forEach(([key, value]) => {
      if (!value || value === 'all') {
        next.delete(key);
      } else {
        next.set(key, value);
      }
    });

    if (!('pageNumber' in updates)) {
      next.set('pageNumber', '1');
    }

    setSearchParams(next);
  };

  const handleRefresh = async () => {
    setRefreshing(true);
    await loadEntries();
    setRefreshing(false);
  };

  const title = useMemo(() => {
    if (action !== 'all' || resourceType !== 'all' || correlationId) {
      return 'Filtered Audit Logs';
    }

    return 'Audit Logs';
  }, [action, correlationId, resourceType]);

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb
        items={[
          { label: 'Settings', href: '/settings', icon: <Cog className="h-3.5 w-3.5" /> },
          { label: 'Audit Logs', icon: <ScrollText className="h-3.5 w-3.5" /> },
        ]}
        className="mb-4"
      />

      <div className="mb-6 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">{title}</h1>
          <p className="text-[var(--color-text-secondary)]">
            Review real control-plane audit records, including scheduled job queueing, execution, and failures.
          </p>
        </div>
        <Button variant="outline" onClick={() => void handleRefresh()} disabled={refreshing || loading}>
          <RefreshCw className={`mr-2 h-4 w-4 ${refreshing ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
      </div>

      <div className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle>Filters</CardTitle>
            <CardDescription>Narrow audit events by job outcome, resource type, correlation ID, or free text.</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-4">
            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="audit-search">Search</Label>
              <form
                onSubmit={(event) => {
                  event.preventDefault();
                  updateFilters({ search: searchInput || null });
                }}
              >
                <Input
                  id="audit-search"
                  placeholder="Search by job, message, correlation ID..."
                  value={searchInput}
                  onChange={(event) => setSearchInput(event.target.value)}
                />
              </form>
            </div>

            <div className="space-y-2">
              <Label>Action</Label>
              <Select value={action} onValueChange={(value) => updateFilters({ action: value })}>
                <SelectTrigger>
                  <SelectValue placeholder="All actions" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All actions</SelectItem>
                  {scheduledJobActions.map((item) => (
                    <SelectItem key={item} value={item}>{item}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Resource</Label>
              <Select value={resourceType} onValueChange={(value) => updateFilters({ resourceType: value })}>
                <SelectTrigger>
                  <SelectValue placeholder="All resources" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All resources</SelectItem>
                  <SelectItem value="ScheduledJobAdminCommand">ScheduledJobAdminCommand</SelectItem>
                  <SelectItem value="ScheduledJobRun">ScheduledJobRun</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="audit-correlation">Correlation ID</Label>
              <Input
                id="audit-correlation"
                placeholder="Filter to one command or run correlation"
                value={correlationId}
                onChange={(event) => updateFilters({ correlationId: event.target.value || null })}
              />
            </div>

            <div className="md:col-span-2 flex items-end gap-2">
              <Button
                variant="outline"
                onClick={() => {
                  setSearchInput('');
                  setSearchParams(new URLSearchParams());
                }}
              >
                Clear Filters
              </Button>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Recent Events</CardTitle>
            <CardDescription>{entries?.totalCount ?? 0} event(s) matched your filters.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {loading ? (
              <p className="py-8 text-center text-sm text-[var(--color-text-tertiary)]">Loading audit events...</p>
            ) : !entries || entries.items.length === 0 ? (
              <p className="py-8 text-center text-sm text-[var(--color-text-tertiary)]">No audit events matched your filters.</p>
            ) : (
              entries.items.map((entry) => {
                const details = parseDetails(entry.detailsJson);
                const errorMessage = getDetailString(details, 'errorMessage');
                const resultSummary = getDetailString(details, 'resultSummary');

                return (
                  <div
                    key={entry.id}
                    className="rounded-md border border-[var(--color-border-light)] px-4 py-3"
                  >
                    <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                      <div className="space-y-2 min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                          <p className="text-sm font-medium text-[var(--color-text-primary)]">{summarize(entry)}</p>
                          <Badge variant={resultVariant(entry.action)}>{entry.action}</Badge>
                        </div>

                        <p className="text-xs text-[var(--color-text-tertiary)] break-all">
                          {metadataLine(entry)}
                        </p>

                        {entry.correlationId && (
                          <p className="font-mono text-xs text-[var(--color-text-tertiary)] break-all">
                            Correlation: {entry.correlationId}
                          </p>
                        )}

                        {(errorMessage || resultSummary) && (
                          <div className="rounded-sm bg-[var(--color-surface-inset)] p-3 text-xs text-[var(--color-text-secondary)] whitespace-pre-wrap break-words">
                            {errorMessage ?? resultSummary}
                          </div>
                        )}

                        {entry.detailsJson.trim() && (
                          <details className="text-xs text-[var(--color-text-tertiary)]">
                            <summary className="cursor-pointer select-none">Raw details</summary>
                            <pre className="mt-2 overflow-auto rounded-sm bg-[var(--color-surface-inset)] p-3 whitespace-pre-wrap break-words">
                              {entry.detailsJson}
                            </pre>
                          </details>
                        )}
                      </div>

                      <div className="text-xs text-[var(--color-text-tertiary)] whitespace-nowrap">
                        {formatDateTime(entry.timestamp)}
                      </div>
                    </div>
                  </div>
                );
              })
            )}

            {entries && entries.totalPages > 1 && (
              <div className="flex items-center justify-between pt-2">
                <span className="text-xs text-[var(--color-text-tertiary)]">
                  Page {entries.pageNumber} of {entries.totalPages} ({entries.totalCount} total)
                </span>
                <div className="flex gap-2">
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={entries.pageNumber <= 1}
                    onClick={() => updateFilters({ pageNumber: String(entries.pageNumber - 1) })}
                  >
                    Previous
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={entries.pageNumber >= entries.totalPages}
                    onClick={() => updateFilters({ pageNumber: String(entries.pageNumber + 1) })}
                  >
                    Next
                  </Button>
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
