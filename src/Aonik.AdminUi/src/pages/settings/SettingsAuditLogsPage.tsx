import { useMemo, useState } from 'react';
import { Cog, Download, ScrollText } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';

interface AuditEntry {
  id: string;
  timestampUtc: string;
  actor: string;
  category: 'Auth' | 'Policy' | 'Settings' | 'Operations';
  action: string;
  result: 'Success' | 'Warning' | 'Failed';
  metadata: string;
}

const auditEntries: AuditEntry[] = [
  {
    id: 'AUD-23918',
    timestampUtc: '2026-03-01T08:40:00Z',
    actor: 'michael.josiah@mailinator.com',
    category: 'Settings',
    action: 'Updated webhook retry policy',
    result: 'Success',
    metadata: 'retryPolicy=5',
  },
  {
    id: 'AUD-23911',
    timestampUtc: '2026-03-01T08:12:00Z',
    actor: 'ops.bot@aonik.ai',
    category: 'Operations',
    action: 'Invalidated cache set',
    result: 'Success',
    metadata: 'cacheSet=TenantFeatures',
  },
  {
    id: 'AUD-23887',
    timestampUtc: '2026-02-28T20:05:00Z',
    actor: 'security.admin@aonik.ai',
    category: 'Policy',
    action: 'Updated approval threshold',
    result: 'Warning',
    metadata: 'threshold=10000',
  },
  {
    id: 'AUD-23855',
    timestampUtc: '2026-02-28T17:18:00Z',
    actor: 'integration-worker',
    category: 'Auth',
    action: 'API key authentication attempt',
    result: 'Failed',
    metadata: 'reason=revoked_key',
  },
  {
    id: 'AUD-23822',
    timestampUtc: '2026-02-28T12:52:00Z',
    actor: 'platform.admin@aonik.ai',
    category: 'Settings',
    action: 'Generated new API key',
    result: 'Success',
    metadata: 'keyLabel=Reporting Pipeline',
  },
];

function formatDateTime(isoDate: string) {
  return new Date(isoDate).toLocaleString();
}

function resultVariant(result: AuditEntry['result']): 'success' | 'warning' | 'error' {
  if (result === 'Success') return 'success';
  if (result === 'Warning') return 'warning';
  return 'error';
}

export function SettingsAuditLogsPage() {
  const [query, setQuery] = useState('');
  const [categoryFilter, setCategoryFilter] = useState<'all' | AuditEntry['category']>('all');
  const [resultFilter, setResultFilter] = useState<'all' | AuditEntry['result']>('all');

  const filteredEntries = useMemo(() => {
    const normalized = query.trim().toLowerCase();

    return auditEntries.filter((entry) => {
      if (categoryFilter !== 'all' && entry.category !== categoryFilter) return false;
      if (resultFilter !== 'all' && entry.result !== resultFilter) return false;

      if (!normalized) return true;

      const searchable = `${entry.id} ${entry.actor} ${entry.action} ${entry.metadata}`.toLowerCase();
      return searchable.includes(normalized);
    });
  }, [categoryFilter, query, resultFilter]);

  const handleExport = () => {
    toast.success('Export queued. Audit log file will be prepared shortly.');
  };

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
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Audit Logs</h1>
          <p className="text-[var(--color-text-secondary)]">
            Trace administrative actions and control-plane changes for governance review.
          </p>
        </div>
        <Button variant="outline" onClick={handleExport}>
          <Download className="mr-2 h-4 w-4" />
          Export CSV
        </Button>
      </div>

      <div className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle>Filters</CardTitle>
            <CardDescription>Narrow down events by category, outcome, or free text.</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-3">
            <div className="space-y-2">
              <Label htmlFor="audit-search">Search</Label>
              <Input
                id="audit-search"
                placeholder="Search by actor, action, ID..."
                value={query}
                onChange={(event) => setQuery(event.target.value)}
              />
            </div>

            <div className="space-y-2">
              <Label>Category</Label>
              <Select value={categoryFilter} onValueChange={(value) => setCategoryFilter(value as 'all' | AuditEntry['category'])}>
                <SelectTrigger>
                  <SelectValue placeholder="All categories" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All categories</SelectItem>
                  <SelectItem value="Auth">Auth</SelectItem>
                  <SelectItem value="Policy">Policy</SelectItem>
                  <SelectItem value="Settings">Settings</SelectItem>
                  <SelectItem value="Operations">Operations</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Result</Label>
              <Select value={resultFilter} onValueChange={(value) => setResultFilter(value as 'all' | AuditEntry['result'])}>
                <SelectTrigger>
                  <SelectValue placeholder="All results" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All results</SelectItem>
                  <SelectItem value="Success">Success</SelectItem>
                  <SelectItem value="Warning">Warning</SelectItem>
                  <SelectItem value="Failed">Failed</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Recent Events</CardTitle>
            <CardDescription>{filteredEntries.length} event(s) matched your filters.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {filteredEntries.length === 0 ? (
              <p className="py-8 text-center text-sm text-[var(--color-text-tertiary)]">No audit events matched your filters.</p>
            ) : (
              filteredEntries.map((entry) => (
                <div
                  key={entry.id}
                  className="flex flex-col gap-3 rounded-md border border-[var(--color-border-light)] px-4 py-3 lg:flex-row lg:items-start lg:justify-between"
                >
                  <div className="space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="text-sm font-medium text-[var(--color-text-primary)]">{entry.action}</p>
                      <Badge variant="outline">{entry.category}</Badge>
                      <Badge variant={resultVariant(entry.result)}>{entry.result}</Badge>
                    </div>
                    <p className="text-xs text-[var(--color-text-tertiary)]">
                      {entry.id} · {entry.actor}
                    </p>
                    <p className="font-mono text-xs text-[var(--color-text-tertiary)]">{entry.metadata}</p>
                  </div>
                  <p className="text-xs text-[var(--color-text-tertiary)]">{formatDateTime(entry.timestampUtc)}</p>
                </div>
              ))
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
