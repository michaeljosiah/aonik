import { useEffect, useMemo, useState } from 'react';
import {
  X, Check, ChevronRight, ChevronDown, ArrowRight, Download,
  RefreshCw, AlertCircle, RotateCw,
} from 'lucide-react';
import { Pill } from '@/components/layout/aonik/Pill';
import { Button } from '@/components/ui/button';
import { billerImportService } from '@/services/billerImportService';
import type {
  BillerImportSourceItem,
  BillerImportPreviewEntry,
  BillerImportSummaryResponse,
} from '@/types';
import { connectorColor } from './billerVisuals';

interface BillerImportWizardProps {
  onClose: () => void;
  onImported: (summary: BillerImportSummaryResponse, connectorType: string) => void;
}

const DASH = '—';

function StepDots({ step }: { step: number }) {
  const labels = ['Source', 'Preview', 'Confirm'];
  return (
    <div className="flex items-center gap-2">
      {labels.map((label, i) => {
        const n = i + 1;
        const active = n === step;
        const done = n < step;
        return (
          <div key={label} className="flex items-center gap-2">
            <div className="flex items-center gap-1.5">
              <span
                className="w-5 h-5 rounded-full grid place-items-center text-[10.5px] font-bold"
                style={{
                  background: active
                    ? 'var(--color-brand-primary)'
                    : done
                      ? 'var(--color-brand-primary-10)'
                      : 'var(--color-surface-inset)',
                  color: active ? '#fff' : done ? 'var(--color-brand-primary)' : 'var(--color-text-tertiary)',
                  border: active ? 'none' : '1px solid var(--color-border-light)',
                }}
              >
                {done ? <Check className="w-2.5 h-2.5" /> : n}
              </span>
              <span
                className="text-xs"
                style={{
                  fontWeight: active ? 600 : 500,
                  color: active ? 'var(--color-text-primary)' : 'var(--color-text-tertiary)',
                }}
              >
                {label}
              </span>
            </div>
            {n < 3 && <div className="w-5 h-px bg-[var(--color-border-light)]" />}
          </div>
        );
      })}
    </div>
  );
}

const STATUS_STYLES: Record<string, { fg: string; bg: string }> = {
  New: { fg: 'var(--color-brand-primary)', bg: 'var(--color-brand-primary-10)' },
  Changed: { fg: '#b4741e', bg: '#b4741e18' },
  Mapped: { fg: 'var(--color-text-tertiary)', bg: 'var(--color-surface-inset)' },
};

function StatusChip({ status }: { status: string }) {
  const s = STATUS_STYLES[status] ?? STATUS_STYLES.Mapped;
  return (
    <span
      className="text-[9.5px] font-bold uppercase tracking-wide px-1.5 py-0.5 rounded font-mono"
      style={{ color: s.fg, background: s.bg }}
    >
      {status}
    </span>
  );
}

function connectorInitials(type: string): string {
  return (type || '?').replace(/[^a-zA-Z0-9]/g, '').slice(0, 2).toUpperCase() || '?';
}

export function BillerImportWizard({ onClose, onImported }: BillerImportWizardProps) {
  const [step, setStep] = useState(1);

  const [sources, setSources] = useState<BillerImportSourceItem[]>([]);
  const [sourcesLoading, setSourcesLoading] = useState(true);
  const [sourcesError, setSourcesError] = useState<string | null>(null);
  const [connectorId, setConnectorId] = useState<string>('');

  const [entries, setEntries] = useState<BillerImportPreviewEntry[]>([]);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [previewError, setPreviewError] = useState<string | null>(null);

  const [selection, setSelection] = useState<Set<string>>(new Set());
  const [catFilter, setCatFilter] = useState('all');
  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});

  const [importing, setImporting] = useState(false);
  const [importError, setImportError] = useState<string | null>(null);
  const [done, setDone] = useState(false);
  const [summary, setSummary] = useState<BillerImportSummaryResponse | null>(null);

  const selectedConnector = sources.find((s) => s.connectorId === connectorId) ?? null;

  useEffect(() => {
    let cancelled = false;
    billerImportService
      .getSources()
      .then((res) => {
        if (cancelled) return;
        setSources(res.sources);
        const preferred = res.sources.find((s) => !s.isSandbox) ?? res.sources[0];
        if (preferred) setConnectorId(preferred.connectorId);
      })
      .catch((err: unknown) => {
        if (!cancelled) setSourcesError(resolveError(err, 'Failed to load connectors.'));
      })
      .finally(() => {
        if (!cancelled) setSourcesLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const groups = useMemo(() => {
    const map = new Map<string, BillerImportPreviewEntry[]>();
    for (const entry of entries) {
      const key = entry.categoryName || 'Other';
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(entry);
    }
    return Array.from(map.entries()).map(([category, items]) => ({ category, items }));
  }, [entries]);

  const categories = useMemo(() => groups.map((g) => g.category), [groups]);

  const selectedItems = entries.filter((e) => selection.has(e.billerCode));
  const selNew = selectedItems.filter((e) => e.importStatus === 'New');
  const selChanged = selectedItems.filter((e) => e.importStatus === 'Changed');
  const selMapped = selectedItems.filter((e) => e.importStatus === 'Mapped');
  const sumSvc = (list: BillerImportPreviewEntry[]) => list.reduce((acc, e) => acc + (e.serviceCount || 0), 0);
  const projected = {
    created: selNew.length,
    updated: selChanged.length + selMapped.length,
    servicesCreated: sumSvc(selNew),
    servicesUpdated: sumSvc([...selChanged, ...selMapped]),
  };

  const runPreview = async () => {
    if (!connectorId) return;
    setPreviewLoading(true);
    setPreviewError(null);
    try {
      const res = await billerImportService.preview({ connectorId });
      setEntries(res.entries);
      // Default selection = everything not already Mapped (new + changed).
      setSelection(new Set(res.entries.filter((e) => e.importStatus !== 'Mapped').map((e) => e.billerCode)));
      setStep(2);
    } catch (err: unknown) {
      setPreviewError(resolveError(err, 'Failed to read the partner catalogue.'));
    } finally {
      setPreviewLoading(false);
    }
  };

  const toggle = (code: string) =>
    setSelection((prev) => {
      const next = new Set(prev);
      if (next.has(code)) next.delete(code);
      else next.add(code);
      return next;
    });

  const selectAllNew = () =>
    setSelection((prev) => {
      const next = new Set(prev);
      entries.filter((e) => e.importStatus === 'New').forEach((e) => next.add(e.billerCode));
      return next;
    });

  const runImport = async () => {
    if (!connectorId) return;
    setImporting(true);
    setImportError(null);
    try {
      const res = await billerImportService.import({
        connectorId,
        entries: selectedItems.map((e) => ({ billerCode: e.billerCode })),
      });
      setSummary(res);
      setDone(true);
    } catch (err: unknown) {
      setImportError(resolveError(err, 'Import failed.'));
    } finally {
      setImporting(false);
    }
  };

  const visibleGroups = catFilter === 'all' ? groups : groups.filter((g) => g.category === catFilter);
  const hasLiveConnector = sources.some((s) => !s.isSandbox);

  return (
    <div
      className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-7"
      onClick={onClose}
    >
      <div
        className="w-[min(880px,94%)] max-h-[90%] bg-[var(--color-surface)] rounded-2xl shadow-2xl flex flex-col overflow-hidden"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="px-6 pt-4 pb-3.5 border-b border-[var(--color-border-light)] flex items-center gap-4">
          <div className="flex-1 min-w-0">
            <div className="text-base font-bold text-[var(--color-text-primary)]">Import billers from a partner</div>
            <div className="text-xs text-[var(--color-text-secondary)] mt-0.5">
              Pull a connector's live catalogue · idempotent upsert · no money moves
            </div>
          </div>
          {!done && <StepDots step={step} />}
          <button
            onClick={onClose}
            aria-label="Close"
            className="w-7 h-7 rounded-md border border-[var(--color-border-light)] grid place-items-center text-[var(--color-text-secondary)] hover:bg-[var(--color-surface-inset)] flex-none"
          >
            <X className="w-3.5 h-3.5" />
          </button>
        </div>

        {/* Preview toolbar */}
        {step === 2 && !done && (
          <div className="px-6 py-2.5 border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)] flex items-center gap-2.5 flex-wrap">
            <span className="inline-flex items-center gap-1.5 text-[11.5px] text-[var(--color-text-secondary)]">
              <span className="w-[7px] h-[7px] rounded-full bg-[var(--color-success)]" /> Live ·{' '}
              {selectedConnector?.connectorType}
            </span>
            <div className="flex gap-1 flex-wrap">
              {['all', ...categories].map((c) => {
                const on = catFilter === c;
                return (
                  <button
                    key={c}
                    onClick={() => setCatFilter(c)}
                    className="text-[11px] px-2.5 py-1 rounded-full border"
                    style={{
                      borderColor: on ? 'var(--color-brand-primary)' : 'var(--color-border-light)',
                      background: on ? 'var(--color-brand-primary-10)' : 'var(--color-surface)',
                      color: on ? 'var(--color-brand-primary)' : 'var(--color-text-secondary)',
                      fontWeight: on ? 600 : 500,
                    }}
                  >
                    {c === 'all' ? 'All' : c}
                  </button>
                );
              })}
            </div>
            <div className="flex-1" />
            <Button variant="ghost" size="sm" onClick={selectAllNew} className="text-[11.5px]">
              <Check className="w-3 h-3 mr-1" /> Select all new
            </Button>
          </div>
        )}

        {/* Body */}
        <div className="flex-1 overflow-auto p-6">
          {done && summary ? (
            <ResultScreen summary={summary} connectorType={selectedConnector?.connectorType ?? 'the partner'} />
          ) : step === 1 ? (
            <div className="flex flex-col gap-2.5">
              <div className="text-[12.5px] text-[var(--color-text-secondary)] mb-0.5">
                Choose a configured partner connector to import from. Catalogues are NG-only for bill payment.
              </div>

              {sourcesLoading ? (
                <div className="p-10 text-center">
                  <RefreshCw className="w-5 h-5 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                  <p className="text-sm text-[var(--color-text-secondary)]">Loading connectors…</p>
                </div>
              ) : sourcesError ? (
                <ErrorBox message={sourcesError} />
              ) : sources.length === 0 ? (
                <div className="rounded-xl border border-[var(--color-border-light)] p-8 text-center text-sm text-[var(--color-text-secondary)]">
                  No bill-payment connectors are configured for this tenant.
                </div>
              ) : (
                <>
                  {sources.map((c) => {
                    const on = connectorId === c.connectorId;
                    return (
                      <div
                        key={c.connectorId}
                        onClick={() => setConnectorId(c.connectorId)}
                        className="flex items-center gap-3 p-3.5 rounded-xl cursor-pointer border"
                        style={{
                          borderColor: on ? 'var(--color-brand-primary)' : 'var(--color-border-light)',
                          background: on ? 'var(--color-brand-primary-10)' : 'var(--color-surface)',
                          boxShadow: on ? '0 0 0 1px var(--color-brand-primary)' : 'none',
                        }}
                      >
                        <div
                          className="w-10 h-10 rounded-lg grid place-items-center text-white font-bold text-xs flex-none"
                          style={{ background: connectorColor(c.connectorType) }}
                        >
                          {connectorInitials(c.connectorType)}
                        </div>
                        <div className="flex-1 min-w-0">
                          <div className="flex items-center gap-2">
                            <span className="text-sm font-semibold text-[var(--color-text-primary)]">
                              {c.connectorType}
                            </span>
                            <Pill tone={c.isSandbox ? 'muted' : 'success'} dot>
                              {c.isSandbox ? 'Sandbox' : 'Connected'}
                            </Pill>
                          </div>
                          <div className="text-xs text-[var(--color-text-secondary)] mt-0.5">
                            {c.isSandbox ? 'Sandbox connector · fallback' : 'NG · Bill payment'} · {c.status}
                          </div>
                        </div>
                        <span
                          className="w-[18px] h-[18px] rounded-full grid place-items-center flex-none"
                          style={{ border: `2px solid ${on ? 'var(--color-brand-primary)' : 'var(--color-border-medium)'}` }}
                        >
                          {on && <span className="w-2 h-2 rounded-full bg-[var(--color-brand-primary)]" />}
                        </span>
                      </div>
                    );
                  })}
                  {!hasLiveConnector && (
                    <div className="text-[11.5px] text-[var(--color-text-tertiary)] mt-1">
                      Only the sandbox connector is available. Configure a live partner's bills secret in{' '}
                      <b className="text-[var(--color-text-secondary)]">Settings → Payment Gateways</b> to import a real catalogue.
                    </div>
                  )}
                  {previewError && <ErrorBox message={previewError} />}
                </>
              )}
            </div>
          ) : step === 2 ? (
            <div className="flex flex-col gap-4">
              {entries.length === 0 ? (
                <div className="rounded-xl border border-[var(--color-border-light)] p-8 text-center text-sm text-[var(--color-text-secondary)]">
                  The partner returned no billers for this catalogue.
                </div>
              ) : (
                visibleGroups.map((g) => {
                  const open = collapsed[g.category] !== true;
                  const groupNew = g.items.filter((i) => i.importStatus === 'New').length;
                  return (
                    <div key={g.category}>
                      <div
                        onClick={() => setCollapsed((c) => ({ ...c, [g.category]: open }))}
                        className="flex items-center gap-2 py-1 px-0.5 cursor-pointer mb-1.5"
                      >
                        {open ? (
                          <ChevronDown className="w-3.5 h-3.5 text-[var(--color-text-tertiary)]" />
                        ) : (
                          <ChevronRight className="w-3.5 h-3.5 text-[var(--color-text-tertiary)]" />
                        )}
                        <span className="text-[12.5px] font-bold text-[var(--color-text-primary)]">{g.category}</span>
                        <span className="font-mono text-[11px] text-[var(--color-text-tertiary)]">{g.items.length}</span>
                        {groupNew > 0 && (
                          <span className="text-[10px] text-[var(--color-brand-primary)] font-semibold">· {groupNew} new</span>
                        )}
                      </div>
                      {open && (
                        <div className="rounded-lg border border-[var(--color-border-light)] overflow-hidden">
                          {g.items.map((it, i) => {
                            const checked = selection.has(it.billerCode);
                            return (
                              <div
                                key={it.billerCode}
                                onClick={() => toggle(it.billerCode)}
                                className="grid grid-cols-[22px_1fr_auto] gap-3 items-center px-3.5 py-2.5 cursor-pointer"
                                style={{
                                  borderTop: i ? '1px solid var(--color-border-light)' : 'none',
                                  background: checked ? 'var(--color-brand-primary-10)' : 'transparent',
                                }}
                              >
                                <span
                                  className="w-[17px] h-[17px] rounded grid place-items-center flex-none"
                                  style={{
                                    border: `1.5px solid ${checked ? 'var(--color-brand-primary)' : 'var(--color-border-medium)'}`,
                                    background: checked ? 'var(--color-brand-primary)' : 'var(--color-surface)',
                                  }}
                                >
                                  {checked && <Check className="w-2.5 h-2.5 text-white" />}
                                </span>
                                <div className="min-w-0">
                                  <div className="text-[13px] font-medium text-[var(--color-text-primary)] truncate">
                                    {it.billerName}
                                  </div>
                                  <div className="text-[10.5px] text-[var(--color-text-tertiary)] truncate">
                                    <span className="font-mono">{it.billerCode}</span> · {it.serviceCount} service
                                    {it.serviceCount === 1 ? '' : 's'}
                                    {it.changeNote && <span style={{ color: '#b4741e' }}> · {it.changeNote}</span>}
                                  </div>
                                </div>
                                <StatusChip status={it.importStatus} />
                              </div>
                            );
                          })}
                        </div>
                      )}
                    </div>
                  );
                })
              )}
            </div>
          ) : (
            /* step 3 — confirm */
            <div className="flex flex-col gap-4">
              <div className="text-[13.5px] font-semibold text-[var(--color-text-primary)]">Review import</div>
              <div className="flex items-center gap-3 p-4 rounded-xl bg-[var(--color-surface-inset)] border border-[var(--color-border-light)]">
                <div
                  className="w-10 h-10 rounded-lg grid place-items-center text-white font-bold text-xs flex-none"
                  style={{ background: connectorColor(selectedConnector?.connectorType ?? '') }}
                >
                  {connectorInitials(selectedConnector?.connectorType ?? '')}
                </div>
                <ArrowRight className="w-4 h-4 text-[var(--color-text-tertiary)]" />
                <span className="text-[13px] font-semibold text-[var(--color-text-primary)]">Aonik catalog</span>
                <div className="flex-1" />
                <span className="text-xs text-[var(--color-text-secondary)]">
                  <b className="font-mono text-[var(--color-text-primary)]">{selectedItems.length}</b> selected
                </span>
              </div>

              <div className="grid grid-cols-3 gap-2.5">
                {[
                  ['Billers created', String(projected.created), 'var(--color-brand-primary)'],
                  ['Billers updated', String(projected.updated), 'var(--color-text-primary)'],
                  ['Deactivated', DASH, 'var(--color-warning)'],
                  ['Services created', String(projected.servicesCreated), 'var(--color-brand-primary)'],
                  ['Services updated', String(projected.servicesUpdated), 'var(--color-text-primary)'],
                  ['Duplicates', '0', 'var(--color-text-tertiary)'],
                ].map(([label, value, color]) => (
                  <div key={label} className="bg-[var(--color-surface)] border border-[var(--color-border-light)] rounded-lg px-3.5 py-3">
                    <div className="text-[10.5px] text-[var(--color-text-tertiary)] uppercase tracking-wide font-semibold">
                      {label}
                    </div>
                    <div className="font-mono text-[22px] font-bold mt-1" style={{ color }}>
                      {value}
                    </div>
                  </div>
                ))}
              </div>

              <div className="flex gap-2.5 px-3.5 py-3 rounded-r-lg bg-[var(--color-brand-primary-10)] border-l-[3px] border-[var(--color-brand-primary)]">
                <RotateCw className="w-3.5 h-3.5 text-[var(--color-brand-primary)] mt-0.5 flex-none" />
                <div className="text-[11.5px] text-[var(--color-text-secondary)] leading-relaxed">
                  Identity is the provider mapping, so this is{' '}
                  <b className="text-[var(--color-text-primary)]">idempotent</b> — running it again reports{' '}
                  <span className="font-mono">0 created</span>. Any deactivation is a biller the partner no longer
                  offers; it is kept (soft-deactivated), never deleted. The actual counts are confirmed after import.
                </div>
              </div>

              {importError && <ErrorBox message={importError} />}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="flex-none px-6 py-3.5 border-t border-[var(--color-border-light)] bg-[var(--color-surface-inset)] flex items-center justify-between gap-3">
          <div className="text-xs text-[var(--color-text-secondary)]">
            {done ? (
              <span>Catalogue refreshed.</span>
            ) : step === 1 ? (
              <span>{selectedConnector ? `${selectedConnector.connectorType} · ${selectedConnector.status}` : 'Select a connector'}</span>
            ) : step === 2 ? (
              <span>
                <b className="font-mono text-[var(--color-text-primary)]">{selectedItems.length}</b> selected · {selNew.length} new ·{' '}
                {selChanged.length} changed
              </span>
            ) : (
              <span>Catalog.Write · medium-risk reference-data write</span>
            )}
          </div>
          <div className="flex gap-2">
            {done ? (
              <Button
                size="sm"
                onClick={() => summary && onImported(summary, selectedConnector?.connectorType ?? '')}
              >
                Done
              </Button>
            ) : (
              <>
                {step > 1 && (
                  <Button variant="outline" size="sm" onClick={() => setStep(step - 1)} disabled={importing}>
                    Back
                  </Button>
                )}
                <Button variant="ghost" size="sm" onClick={onClose} disabled={importing}>
                  Cancel
                </Button>
                {step === 1 && (
                  <Button size="sm" onClick={runPreview} disabled={!connectorId || previewLoading}>
                    {previewLoading ? 'Loading…' : 'Preview catalogue'}
                    {!previewLoading && <ArrowRight className="w-3 h-3 ml-1.5" />}
                  </Button>
                )}
                {step === 2 && (
                  <Button size="sm" onClick={() => setStep(3)} disabled={selectedItems.length === 0}>
                    Review {selectedItems.length} <ArrowRight className="w-3 h-3 ml-1.5" />
                  </Button>
                )}
                {step === 3 && (
                  <Button size="sm" onClick={runImport} disabled={importing || selectedItems.length === 0}>
                    {importing ? (
                      'Importing…'
                    ) : (
                      <>
                        <Download className="w-3.5 h-3.5 mr-1.5" /> Import {selectedItems.length} billers
                      </>
                    )}
                  </Button>
                )}
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

function ResultScreen({
  summary,
  connectorType,
}: {
  summary: BillerImportSummaryResponse;
  connectorType: string;
}) {
  const cells: Array<[string, number, string]> = [
    ['created', summary.billersCreated, 'var(--color-success)'],
    ['updated', summary.billersUpdated, 'var(--color-text-primary)'],
    ['duplicates', 0, 'var(--color-text-tertiary)'],
    ['deactivated', summary.deactivated, 'var(--color-warning)'],
  ];
  return (
    <div className="flex flex-col items-center text-center pt-4 pb-2 gap-3.5">
      <span className="w-13 h-13 rounded-full bg-[var(--color-success)] text-white grid place-items-center" style={{ width: 52, height: 52 }}>
        <Check className="w-6 h-6" />
      </span>
      <div>
        <div className="text-lg font-bold text-[var(--color-text-primary)]">Import complete</div>
        <div className="text-[13px] text-[var(--color-text-secondary)] mt-1">
          Billers from {connectorType} are now in your catalogue, each routed through its connector mapping.
        </div>
      </div>
      <div className="flex gap-6 px-6 py-3.5 rounded-xl bg-[var(--color-surface-inset)] border border-[var(--color-border-light)] font-mono">
        {cells.map(([label, value, color]) => (
          <div key={label} className="text-center">
            <div className="text-2xl font-bold" style={{ color }}>
              {value}
            </div>
            <div className="text-[10.5px] text-[var(--color-text-tertiary)] uppercase tracking-wide mt-0.5 font-sans">
              {label}
            </div>
          </div>
        ))}
      </div>
      <div className="text-[11.5px] text-[var(--color-text-tertiary)] max-w-[440px] leading-relaxed">
        Re-running this import is safe — it would refresh changed rows and create nothing new.
      </div>
    </div>
  );
}

function ErrorBox({ message }: { message: string }) {
  return (
    <div className="rounded-lg border border-[var(--color-error)] bg-[var(--color-error-light)] p-3 flex items-start gap-2 text-[var(--color-error)] text-sm">
      <AlertCircle className="w-4 h-4 mt-0.5 flex-none" />
      <span>{message}</span>
    </div>
  );
}

function resolveError(err: unknown, fallback: string): string {
  if (err && typeof err === 'object' && 'userMessage' in err) {
    const msg = String((err as { userMessage?: string }).userMessage ?? '');
    if (msg) return msg;
  }
  return fallback;
}
