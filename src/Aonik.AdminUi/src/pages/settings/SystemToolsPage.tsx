import { useMemo, useState, useEffect } from 'react';
import { ShieldCheck, Wrench, RefreshCw, AlertCircle, ServerCog, Database, Sprout, CheckCircle2 } from 'lucide-react';
import { toast } from 'sonner';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { ImportDemoDataDialog } from '@/components/dialogs/ImportDemoDataDialog';
import { demoSeedService } from '@/services/demoSeedService';
import { permissionSeedService } from '@/services/permissionSeedService';
import { cacheManagementService } from '@/services/cacheManagementService';
import { dataSeedService } from '@/services/dataSeedService';
import { getSelectedTenant } from '@/lib/tenantContext';
import type { CacheOverviewResponse, DataSeedInfo, DataSeedResponse, DemoSeedResponse, DemoSeedType, PermissionSeedResponse } from '@/types';

type ToolStatus = 'idle' | 'running' | 'success' | 'error';

export function SystemToolsPage() {
  const selectedTenant = useMemo(() => getSelectedTenant(), []);
  const [demoSeedDialogOpen, setDemoSeedDialogOpen] = useState(false);
  const [demoSeedResult, setDemoSeedResult] = useState<DemoSeedResponse | null>(null);
  const [demoSeedStatus, setDemoSeedStatus] = useState<ToolStatus>('idle');
  const [demoSeedError, setDemoSeedError] = useState<string | null>(null);
  const [permissionSeedResult, setPermissionSeedResult] = useState<PermissionSeedResponse | null>(null);
  const [permissionSeedStatus, setPermissionSeedStatus] = useState<ToolStatus>('idle');
  const [permissionSeedError, setPermissionSeedError] = useState<string | null>(null);
  const [cacheOverview, setCacheOverview] = useState<CacheOverviewResponse | null>(null);
  const [cacheStatus, setCacheStatus] = useState<ToolStatus>('idle');
  const [cacheError, setCacheError] = useState<string | null>(null);
  const [invalidatingCacheSet, setInvalidatingCacheSet] = useState<string | null>(null);
  const [availableSeeds, setAvailableSeeds] = useState<DataSeedInfo[]>([]);
  const [selectedSeedKeys, setSelectedSeedKeys] = useState<Set<string>>(new Set());
  const [dataSeedResult, setDataSeedResult] = useState<DataSeedResponse | null>(null);
  const [dataSeedStatus, setDataSeedStatus] = useState<ToolStatus>('idle');
  const [dataSeedError, setDataSeedError] = useState<string | null>(null);

  const tenantLabel = selectedTenant?.name
    ? `${selectedTenant.name} (${selectedTenant.tenantId})`
    : selectedTenant?.tenantId;

  const handlePermissionSeed = async () => {
    if (!selectedTenant?.tenantId) {
      setPermissionSeedStatus('error');
      setPermissionSeedError('Select a tenant before running a system tool.');
      return;
    }

    setPermissionSeedStatus('running');
    setPermissionSeedError(null);

    try {
      const result = await permissionSeedService.seed(selectedTenant.tenantId);
      setPermissionSeedResult(result);
      setPermissionSeedStatus('success');
      toast.success('Permission sync completed.');
    } catch (err: unknown) {
      console.error('Permission seed failed:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setPermissionSeedError(message || 'Permission sync failed. Please try again.');
      setPermissionSeedStatus('error');
    }
  };

  const handleImportDemoData = async (seedType: DemoSeedType) => {
    if (!selectedTenant?.tenantId) {
      setDemoSeedStatus('error');
      setDemoSeedError('Select a tenant before running a system tool.');
      return;
    }

    setDemoSeedStatus('running');
    setDemoSeedError(null);

    try {
      const result = await demoSeedService.seed(selectedTenant.tenantId, seedType);
      setDemoSeedResult(result);
      setDemoSeedStatus('success');
      setDemoSeedDialogOpen(false);
      toast.success(`${result.seedType === 'CrossBorderPayments' ? 'Cross-border payments' : 'Bill collection'} demo data imported.`);
    } catch (err: unknown) {
      console.error('Demo seed failed:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setDemoSeedError(message || 'Demo data import failed. Please try again.');
      setDemoSeedStatus('error');
    }
  };


  const loadCacheOverview = async () => {
    setCacheStatus('running');
    setCacheError(null);

    try {
      const result = await cacheManagementService.getOverview();
      setCacheOverview(result);
      setCacheStatus('success');
    } catch (err: unknown) {
      console.error('Cache overview failed:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setCacheError(message || 'Cache overview could not be loaded.');
      setCacheStatus('error');
    }
  };

  const handleInvalidateCacheSet = async (cacheSet: string) => {
    setInvalidatingCacheSet(cacheSet);
    setCacheError(null);

    try {
      await cacheManagementService.invalidateCacheSet(cacheSet);
      toast.success(`Cache set "${cacheSet}" invalidated.`);
      await loadCacheOverview();
    } catch (err: unknown) {
      console.error('Cache invalidation failed:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setCacheError(message || 'Cache invalidation failed. Please try again.');
      setCacheStatus('error');
    } finally {
      setInvalidatingCacheSet(null);
    }
  };

  const loadAvailableSeeds = async () => {
    try {
      const result = await dataSeedService.list();
      setAvailableSeeds(result.seeds);
      setSelectedSeedKeys(new Set(result.seeds.map((s) => s.key)));
    } catch (err) {
      console.error('Failed to load available seeds:', err);
    }
  };

  const toggleSeedKey = (key: string) => {
    setSelectedSeedKeys((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  };

  const toggleAllSeeds = () => {
    if (selectedSeedKeys.size === availableSeeds.length) {
      setSelectedSeedKeys(new Set());
    } else {
      setSelectedSeedKeys(new Set(availableSeeds.map((s) => s.key)));
    }
  };

  const handleRunDataSeed = async () => {
    if (selectedSeedKeys.size === 0) return;

    setDataSeedStatus('running');
    setDataSeedError(null);

    try {
      const keys = selectedSeedKeys.size === availableSeeds.length
        ? undefined
        : Array.from(selectedSeedKeys);
      const result = await dataSeedService.run(keys);
      setDataSeedResult(result);
      setDataSeedStatus('success');
      toast.success(`Data seed completed — ${result.results.length} seed(s) ran.`);
    } catch (err: unknown) {
      console.error('Data seed failed:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setDataSeedError(message || 'Data seed failed. Please try again.');
      setDataSeedStatus('error');
    }
  };

  useEffect(() => {
    void loadCacheOverview();
    void loadAvailableSeeds();
  }, []);

  const breadcrumbItems = [
    { label: 'Settings', href: '/settings', icon: <ServerCog className="w-3.5 h-3.5" /> },
    { label: 'System Tools', icon: <Wrench className="w-3.5 h-3.5" /> },
  ];

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

      <div className="flex items-start justify-between gap-4 mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">System Tools</h1>
          <p className="text-[var(--color-text-secondary)]">
            Run platform maintenance utilities for the currently selected tenant.
          </p>
        </div>
        {tenantLabel ? (
          <Badge className="bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]">
            Tenant: {tenantLabel}
          </Badge>
        ) : (
          <Badge className="bg-[var(--color-warning-light)] text-[var(--color-warning)]">
            No tenant selected
          </Badge>
        )}
      </div>

      {!selectedTenant?.tenantId && (
        <Card className="mb-6 border-[var(--color-warning)] bg-[var(--color-warning-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-warning)]">
            <AlertCircle className="w-5 h-5" />
            <span>Select a tenant to enable system tools.</span>
          </CardContent>
        </Card>
      )}

      <div className="grid gap-6">

        <Card>
          <CardHeader className="flex flex-row items-start justify-between gap-4">
            <div className="space-y-1">
              <CardTitle className="flex items-center gap-2">
                <Sprout className="w-5 h-5 text-[var(--color-brand-primary)]" />
                Data Seeds
              </CardTitle>
              <CardDescription>
                Run global data seed routines on demand. These are the same seeds that run at startup — idempotent and safe to re-run.
              </CardDescription>
            </div>
            <Button
              onClick={() => void handleRunDataSeed()}
              disabled={dataSeedStatus === 'running' || selectedSeedKeys.size === 0}
              className="rounded-sm"
            >
              <RefreshCw className={`w-4 h-4 mr-2 ${dataSeedStatus === 'running' ? 'animate-spin' : ''}`} />
              {dataSeedStatus === 'running' ? 'Running...' : 'Run Selected'}
            </Button>
          </CardHeader>
          <CardContent className="space-y-3">
            {dataSeedError && (
              <div className="rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-4 py-3 text-sm text-[var(--color-error)]">
                {dataSeedError}
              </div>
            )}

            {availableSeeds.length > 0 ? (
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <button
                    type="button"
                    onClick={toggleAllSeeds}
                    className="text-xs text-[var(--color-brand-primary)] hover:underline"
                  >
                    {selectedSeedKeys.size === availableSeeds.length ? 'Deselect all' : 'Select all'}
                  </button>
                  <span className="text-xs text-[var(--color-text-tertiary)]">
                    {selectedSeedKeys.size} of {availableSeeds.length} selected
                  </span>
                </div>
                <div className="rounded-md border border-[var(--color-border-light)] divide-y divide-[var(--color-border-light)]">
                  {availableSeeds.map((seed) => (
                    <label
                      key={seed.key}
                      className="flex items-center gap-3 px-4 py-3 cursor-pointer hover:bg-[var(--color-surface-inset)]/30"
                    >
                      <input
                        type="checkbox"
                        checked={selectedSeedKeys.has(seed.key)}
                        onChange={() => toggleSeedKey(seed.key)}
                        className="h-4 w-4 rounded border-[var(--color-border-light)] text-[var(--color-brand-primary)] focus:ring-[var(--color-brand-primary)]"
                      />
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium text-[var(--color-text-primary)]">{seed.displayName}</p>
                        <p className="text-xs text-[var(--color-text-tertiary)]">{seed.description}</p>
                      </div>
                    </label>
                  ))}
                </div>
              </div>
            ) : (
              <p className="text-sm text-[var(--color-text-tertiary)]">
                Loading available seeds...
              </p>
            )}

            {dataSeedResult && (
              <div className="space-y-2 pt-2">
                <div className="flex items-center justify-between text-sm text-[var(--color-text-secondary)]">
                  <span>Last run</span>
                  <span>{new Date(dataSeedResult.seededAt).toLocaleString()}</span>
                </div>
                <div className="rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface-inset)]/30 p-3">
                  <p className="text-xs font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)] mb-2">
                    Results
                  </p>
                  <div className="space-y-2">
                    {dataSeedResult.results.map((item) => (
                      <div key={item.key}>
                        <p className="text-sm font-medium text-[var(--color-text-primary)] flex items-center gap-1.5">
                          <CheckCircle2 className="w-3.5 h-3.5 text-[var(--color-success)]" />
                          {item.displayName}
                        </p>
                        <ul className="ml-5 space-y-0.5">
                          {item.operations.map((op) => (
                            <li key={op} className="text-xs text-[var(--color-text-tertiary)]">{op}</li>
                          ))}
                        </ul>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-start justify-between gap-4">
            <div className="space-y-1">
              <CardTitle className="flex items-center gap-2">
                <Database className="w-5 h-5 text-[var(--color-brand-primary)]" />
                Cache Management
              </CardTitle>
              <CardDescription>
                View live cache sets and invalidate stale entries when troubleshooting or applying configuration changes.
              </CardDescription>
            </div>
            <Button
              onClick={() => void loadCacheOverview()}
              disabled={cacheStatus === 'running'}
              variant="secondary"
              className="rounded-sm"
            >
              <RefreshCw className={`w-4 h-4 mr-2 ${cacheStatus === 'running' ? 'animate-spin' : ''}`} />
              Refresh
            </Button>
          </CardHeader>
          <CardContent className="space-y-3">
            {cacheError && (
              <div className="rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-4 py-3 text-sm text-[var(--color-error)]">
                {cacheError}
              </div>
            )}

            {cacheOverview ? (
              <div className="space-y-3">
                <div className="flex flex-wrap items-center gap-2 text-xs text-[var(--color-text-tertiary)]">
                  <Badge className="bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]">
                    Cache sets: {cacheOverview.totalCacheSets}
                  </Badge>
                  <Badge className="bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]">
                    Total entries: {cacheOverview.totalEntries}
                  </Badge>
                </div>

                {cacheOverview.cacheSets.length > 0 ? (
                  <div className="rounded-md border border-[var(--color-border-light)] divide-y divide-[var(--color-border-light)]">
                    {cacheOverview.cacheSets.map((cacheSet) => (
                      <div key={cacheSet.name} className="flex items-center justify-between px-4 py-3 gap-3">
                        <div>
                          <p className="text-sm font-medium text-[var(--color-text-primary)]">{cacheSet.name}</p>
                          <p className="text-xs text-[var(--color-text-tertiary)]">{cacheSet.entryCount} cached entr{cacheSet.entryCount === 1 ? 'y' : 'ies'}</p>
                        </div>
                        <Button
                          size="sm"
                          variant="secondary"
                          disabled={invalidatingCacheSet === cacheSet.name}
                          onClick={() => void handleInvalidateCacheSet(cacheSet.name)}
                        >
                          {invalidatingCacheSet === cacheSet.name ? 'Invalidating...' : 'Invalidate'}
                        </Button>
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-sm text-[var(--color-text-tertiary)]">
                    No cache entries have been registered yet.
                  </p>
                )}
              </div>
            ) : (
              <p className="text-sm text-[var(--color-text-tertiary)]">
                Refresh to load the current cache sets.
              </p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-start justify-between gap-4">
            <div className="space-y-1">
              <CardTitle className="flex items-center gap-2">
                <Database className="w-5 h-5 text-[var(--color-brand-primary)]" />
                Demo Data Import
              </CardTitle>
              <CardDescription>
                Import curated sandbox datasets for bill collection or cross-border payment demos.
              </CardDescription>
            </div>
            <Button
              onClick={() => {
                setDemoSeedError(null);
                setDemoSeedDialogOpen(true);
              }}
              disabled={demoSeedStatus === 'running' || !selectedTenant?.tenantId}
              className="rounded-sm"
            >
              {demoSeedStatus === 'running' ? 'Importing...' : 'Import Data'}
            </Button>
          </CardHeader>
          <CardContent className="space-y-3">
            {demoSeedError && (
              <div className="rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-4 py-3 text-sm text-[var(--color-error)]">
                {demoSeedError}
              </div>
            )}

            {demoSeedResult ? (
              <div className="space-y-2">
                <div className="flex items-center justify-between text-sm text-[var(--color-text-secondary)]">
                  <span>Last import</span>
                  <span>{new Date(demoSeedResult.seededAt).toLocaleString()}</span>
                </div>
                <div className="flex items-center justify-between text-sm text-[var(--color-text-secondary)]">
                  <span>Dataset</span>
                  <span>{demoSeedResult.seedType === 'CrossBorderPayments' ? 'Cross-border Payments' : 'Bill Collection'}</span>
                </div>
                <div className="rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface-inset)]/30 p-3">
                  <p className="text-xs font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)] mb-2">
                    Operations
                  </p>
                  <ul className="space-y-1 text-sm text-[var(--color-text-secondary)]">
                    {demoSeedResult.operations.map((operation) => (
                      <li key={operation} className="flex items-center gap-2">
                        <span className="h-1.5 w-1.5 rounded-full bg-[var(--color-brand-primary)]" />
                        {operation}
                      </li>
                    ))}
                  </ul>
                </div>
              </div>
            ) : (
              <p className="text-sm text-[var(--color-text-tertiary)]">
                Import demo data to quickly showcase bill pay and cross-border capabilities in the admin workspace.
              </p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-start justify-between gap-4">
            <div className="space-y-1">
              <CardTitle className="flex items-center gap-2">
                <ShieldCheck className="w-5 h-5 text-[var(--color-brand-primary)]" />
                Permission Sync
              </CardTitle>
              <CardDescription>
                Ensures system permissions and role mappings are up-to-date for this tenant.
              </CardDescription>
            </div>
            <Button
              onClick={handlePermissionSeed}
              disabled={permissionSeedStatus === 'running' || !selectedTenant?.tenantId}
              className="rounded-sm"
            >
              <RefreshCw
                className={`w-4 h-4 mr-2 ${permissionSeedStatus === 'running' ? 'animate-spin' : ''}`}
              />
              {permissionSeedStatus === 'running' ? 'Running...' : 'Run Sync'}
            </Button>
          </CardHeader>
          <CardContent className="space-y-3">
            {permissionSeedError && (
              <div className="rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-4 py-3 text-sm text-[var(--color-error)]">
                {permissionSeedError}
              </div>
            )}

            {permissionSeedResult ? (
              <div className="space-y-2">
                <div className="flex items-center justify-between text-sm text-[var(--color-text-secondary)]">
                  <span>Last run</span>
                  <span>{new Date(permissionSeedResult.seededAt).toLocaleString()}</span>
                </div>
                <div className="rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface-inset)]/30 p-3">
                  <p className="text-xs font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)] mb-2">
                    Operations
                  </p>
                  <ul className="space-y-1 text-sm text-[var(--color-text-secondary)]">
                    {permissionSeedResult.operations.map((operation) => (
                      <li key={operation} className="flex items-center gap-2">
                        <span className="h-1.5 w-1.5 rounded-full bg-[var(--color-brand-primary)]" />
                        {operation}
                      </li>
                    ))}
                  </ul>
                </div>
              </div>
            ) : (
              <p className="text-sm text-[var(--color-text-tertiary)]">
                Run the sync to capture any missing permissions or role assignments.
              </p>
            )}
          </CardContent>
        </Card>
      </div>

      <ImportDemoDataDialog
        open={demoSeedDialogOpen}
        onOpenChange={setDemoSeedDialogOpen}
        onImport={handleImportDemoData}
        saving={demoSeedStatus === 'running'}
        error={demoSeedError}
      />
    </div>
  );
}
