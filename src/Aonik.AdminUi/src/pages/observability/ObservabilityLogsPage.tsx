import { useCallback, useEffect, useMemo, useState } from 'react';
import { Loader2 } from 'lucide-react';

import { AonikTemplateIcon } from '@/components/layout/aonik/AonikTemplateIcon';
import { PageHeader } from '@/components/layout/aonik/PageHeader';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { cn } from '@/lib/utils';
import {
  observabilityService,
  type StructuredLogEntry,
  type StructuredLogsResponse,
} from '@/services/observabilityService';

const TIME_RANGE_OPTIONS = [
  { value: '1h', label: 'Last hour' },
  { value: '24h', label: 'Last 24 hours' },
  { value: '7d', label: 'Last 7 days' },
  { value: '30d', label: 'Last 30 days' },
];

const SEVERITY_OPTIONS = [
  { value: 'all', label: 'All' },
  { value: 'debug', label: 'Debug' },
  { value: 'info', label: 'Info' },
  { value: 'warn', label: 'Warn' },
  { value: 'error', label: 'Error' },
];

function getErrorMessage(error: unknown, fallback = 'Unknown error'): string {
  if (error && typeof error === 'object' && 'userMessage' in error) {
    const message = String((error as { userMessage?: string }).userMessage ?? '').trim();
    if (message) return message;
  }

  if (error instanceof Error) {
    const message = error.message.trim();
    if (message) return message;
  }

  return fallback;
}

function severityTone(severity: string) {
  switch (severity.toLowerCase()) {
    case 'debug':
      return { bg: 'var(--color-surface-inset)', fg: 'var(--color-text-tertiary)' };
    case 'warn':
      return { bg: '#b4741e22', fg: '#b4741e' };
    case 'error':
      return { bg: '#c4453622', fg: '#c44536' };
    default:
      return { bg: '#055a6020', fg: '#055a60' };
  }
}

function formatTimestamp(value: string) {
  const date = new Date(value);
  const hours = String(date.getHours()).padStart(2, '0');
  const minutes = String(date.getMinutes()).padStart(2, '0');
  const seconds = String(date.getSeconds()).padStart(2, '0');
  const millis = String(date.getMilliseconds()).padStart(3, '0');
  return `${hours}:${minutes}:${seconds}.${millis}`;
}

function formatNumber(value: number) {
  return value.toLocaleString();
}

function compactFieldKey(value: string) {
  return value
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/[._-]+/g, ' ')
    .trim()
    .split(/\s+/)
    .map((part, index) => (index === 0 ? part.toLowerCase() : part))
    .join('');
}

function buildFieldSummary(entry: StructuredLogEntry) {
  const priorityKeys = [
    'operation',
    'source',
    'observationType',
    'model',
    'actualModel',
    'latencyMs',
    'durationMs',
    'inputTokens',
    'outputTokens',
    'totalTokens',
    'costUsd',
    'jobName',
    'jobOutcome',
    'target',
  ];

  const items: Array<[string, string]> = [];
  for (const key of priorityKeys) {
    const value = entry.fields[key];
    if (value && value.trim()) {
      items.push([key, value.trim()]);
    }
  }

  if (items.length > 0) return items.slice(0, 4);

  return Object.entries(entry.fields)
    .filter(([, value]) => Boolean(value?.trim()))
    .slice(0, 4);
}

export function ObservabilityLogsPage() {
  const [timeRange, setTimeRange] = useState('24h');
  const [severityFilter, setSeverityFilter] = useState('all');
  const [live, setLive] = useState(true);
  const [data, setData] = useState<StructuredLogsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadLogs = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      // Push severity into the request so the backend's take-120 window
      // picks rows of the requested severity instead of returning an
      // info-heavy slice that we then narrow client-side to nothing.
      const result = await observabilityService.getStructuredLogs(timeRange, severityFilter);
      setData(result);
    } catch (loadError) {
      setError(getErrorMessage(loadError, 'Failed to load structured logs.'));
    } finally {
      setLoading(false);
      setInitialLoad(false);
    }
  }, [timeRange, severityFilter]);

  useEffect(() => {
    void loadLogs();
  }, [loadLogs]);

  useEffect(() => {
    if (!live) return;
    const timer = window.setInterval(() => {
      void loadLogs();
    }, 15000);
    return () => window.clearInterval(timer);
  }, [live, loadLogs]);

  const counts = data?.counts ?? { debug: 0, info: 0, warn: 0, error: 0 };

  // Backend already applies the severity filter; this is a defensive
  // pass-through for the rare case where stale data lingers between
  // filter changes (loadLogs replaces it on the next round-trip).
  const filteredEntries = data?.entries ?? [];

  const maxVolume = useMemo(() => {
    const points = data?.volume ?? [];
    return Math.max(1, ...points.map((point) => point.events));
  }, [data?.volume]);

  if (initialLoad) {
    return <PageLoadingScreen message="Loading logs" />;
  }

  return (
    <div className="flex h-full flex-col overflow-auto">
      <div className="border-b border-[var(--color-border-light)] bg-[var(--color-surface)]">
        <div className="px-6 pt-5 pb-4">
          <PageHeader
            eyebrow="Observability · Structured logs"
            title="Logs"
            subtitle="Live tail across every service using indexed Application Insights trace events."
            actions={(
              <>
                <Button
                  variant={live ? 'default' : 'outline'}
                  size="sm"
                  onClick={() => setLive((value) => !value)}
                >
                  <span
                    className={cn(
                      'inline-block h-1.5 w-1.5 rounded-full',
                      live ? 'bg-white' : 'bg-[var(--color-success)]',
                    )}
                  />
                  {live ? 'Live tail' : 'Paused'}
                </Button>
                <Button variant="outline" size="sm" disabled>
                  <AonikTemplateIcon name="download" size={12} />
                  Export
                </Button>
                <Button variant="outline" size="sm" disabled>
                  <AonikTemplateIcon name="bell" size={12} />
                  Alert on…
                </Button>
              </>
            )}
          />
        </div>
      </div>

      <div className="flex-1 p-6">
        <div className="space-y-4">
          <div className="rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)] px-3 py-2.5">
            <div className="flex flex-wrap items-center gap-3">
              <AonikTemplateIcon name="terminal" size={14} color="var(--color-text-secondary)" />
              <span className="flex-1 font-mono text-[12px] text-[var(--color-text-primary)]">
                <span className="text-[var(--color-text-tertiary)]">range:</span>{TIME_RANGE_OPTIONS.find((option) => option.value === timeRange)?.label.toLowerCase() ?? timeRange}
                <span className="mx-2 text-[var(--color-text-tertiary)]">|</span>
                <span className="text-[var(--color-text-tertiary)]">sev:</span>
                {severityFilter === 'all' ? 'any' : severityFilter}
              </span>
              <div className="flex flex-wrap gap-1">
                {SEVERITY_OPTIONS.map((option) => {
                  const active = severityFilter === option.value;
                  const count = option.value === 'all'
                    ? (data?.totalEvents ?? 0)
                    : counts[option.value as keyof typeof counts] ?? 0;
                  return (
                    <button
                      key={option.value}
                      type="button"
                      onClick={() => setSeverityFilter(option.value)}
                      className={cn(
                        'rounded-full border px-2.5 py-1 text-[11px] font-medium transition-colors',
                        active
                          ? 'border-[var(--color-brand-primary)] bg-[var(--color-brand-primary)]/10 text-[var(--color-brand-primary)]'
                          : 'border-[var(--color-border-light)] text-[var(--color-text-secondary)] hover:bg-[var(--color-surface-inset)]',
                      )}
                    >
                      {option.label}
                      <span className="ml-1 font-mono opacity-70">{count}</span>
                    </button>
                  );
                })}
              </div>
            </div>
          </div>

          <div className="grid gap-4 rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)] px-4 py-3 lg:grid-cols-[180px_minmax(0,1fr)_auto] lg:items-center">
            <div>
              <div className="text-[11px] text-[var(--color-text-tertiary)]">Volume · selected window</div>
              <div className="font-mono text-[18px] font-semibold text-[var(--color-text-primary)]">
                {formatNumber(data?.totalEvents ?? 0)}{' '}
                <span className="text-[11px] font-normal text-[var(--color-text-tertiary)]">events</span>
              </div>
            </div>

            <div className="flex h-10 items-end gap-[3px] overflow-hidden">
              {(data?.volume ?? []).map((point) => {
                const height = Math.max(8, Math.round((point.events / maxVolume) * 40));
                const errorHeight = point.events > 0 ? Math.max(0, Math.round((point.errors / maxVolume) * 40)) : 0;
                return (
                  <div key={point.timestamp} className="relative flex w-[6px] items-end justify-center">
                    <div
                      className="w-[4px] rounded-t-sm bg-[var(--color-brand-primary)]/80"
                      style={{ height }}
                      title={`${formatNumber(point.events)} events`}
                    />
                    {errorHeight > 0 ? (
                      <div
                        className="absolute bottom-[calc(100%+2px)] w-[4px] rounded-t-sm bg-[#c44536]"
                        style={{ height: Math.max(2, errorHeight / 4) }}
                        title={`${formatNumber(point.errors)} errors`}
                      />
                    ) : null}
                  </div>
                );
              })}
            </div>

            <div className="flex items-center gap-3 text-[10.5px] text-[var(--color-text-secondary)]">
              <span className="inline-flex items-center gap-1.5">
                <span className="h-2 w-2 bg-[var(--color-brand-primary)]" />
                events
              </span>
              <span className="inline-flex items-center gap-1.5">
                <span className="h-2 w-2 bg-[#c44536]" />
                errors
              </span>
            </div>
          </div>

          <div className="overflow-hidden rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)]">
            <div className="grid grid-cols-[116px_72px_140px_120px_140px_minmax(0,1fr)] gap-3 border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-4 py-2 text-[10px] uppercase tracking-[0.04em] text-[var(--color-text-tertiary)]">
              <div>Timestamp</div>
              <div>Sev</div>
              <div>Service</div>
              <div>Agent</div>
              <div>Trace</div>
              <div>Message</div>
            </div>

            <div className="max-h-[720px] overflow-y-auto font-mono text-[11.5px]">
              {loading ? (
                <div className="flex items-center gap-2 px-4 py-8 text-sm text-[var(--color-text-secondary)]">
                  <Loader2 className="h-4 w-4 animate-spin" />
                  Loading logs...
                </div>
              ) : error ? (
                <div className="px-4 py-8 text-sm text-[#c44536]">{error}</div>
              ) : !data?.configured ? (
                <div className="px-4 py-8 text-sm text-[var(--color-text-secondary)]">
                  Application Insights is not configured for structured logs.
                </div>
              ) : filteredEntries.length === 0 ? (
                <div className="px-4 py-8 text-sm text-[var(--color-text-secondary)]">
                  No log events matched the current filters.
                </div>
              ) : (
                filteredEntries.map((entry) => {
                  const tone = severityTone(entry.severity);
                  const fields = buildFieldSummary(entry);
                  return (
                    <LogRow key={`${entry.timestamp}-${entry.traceId}-${entry.message}`} entry={entry} tone={tone} fields={fields} />
                  );
                })
              )}

              {live && !loading && !error && data?.configured ? (
                <div className="flex items-center gap-2 px-4 py-3 text-[11px] text-[var(--color-text-tertiary)]">
                  <span className="h-1.5 w-1.5 rounded-full bg-[var(--color-success)]" />
                  awaiting new events…
                </div>
              ) : null}
            </div>
          </div>

          <div className="flex items-center justify-between gap-3">
            <Select value={timeRange} onValueChange={setTimeRange}>
              <SelectTrigger className="w-[180px] bg-[var(--color-surface)]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {TIME_RANGE_OPTIONS.map((option) => (
                  <SelectItem key={option.value} value={option.value}>
                    {option.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Button variant="outline" size="sm" onClick={() => void loadLogs()} disabled={loading}>
              {loading ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <AonikTemplateIcon name="filter" size={12} />}
              Refresh
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}

function LogRow({
  entry,
  tone,
  fields,
}: {
  entry: StructuredLogEntry;
  tone: { bg: string; fg: string };
  fields: Array<[string, string]>;
}) {
  return (
    <div className="grid grid-cols-[116px_72px_140px_120px_140px_minmax(0,1fr)] gap-3 border-b border-[var(--color-border-light)] px-4 py-2.5 last:border-b-0">
      <span className="text-[var(--color-text-tertiary)]">{formatTimestamp(entry.timestamp)}</span>
      <span
        className="justify-self-start rounded px-1.5 py-0.5 text-[9.5px] font-semibold uppercase tracking-[0.04em]"
        style={{ background: tone.bg, color: tone.fg }}
      >
        {entry.severity}
      </span>
      <span className="truncate text-[var(--color-text-secondary)]" title={entry.service}>{entry.service}</span>
      <span className="truncate text-[var(--color-text-secondary)]" title={entry.agent}>{entry.agent}</span>
      <span className="truncate text-[var(--color-brand-primary)]" title={entry.traceId}>{entry.traceId}</span>
      <span className="min-w-0 text-[var(--color-text-primary)]">
        {entry.message}
        {fields.length > 0 ? (
          <span className="ml-1 text-[var(--color-text-tertiary)]">
            {' '}· {fields.map(([key, value], index) => (
              <span key={key} className="mr-2 last:mr-0">
                {index > 0 ? '' : ''}
                <span>{compactFieldKey(key)}=</span>
                <span className="text-[var(--color-text-secondary)]">&quot;{value}&quot;</span>
              </span>
            ))}
          </span>
        ) : null}
      </span>
    </div>
  );
}
