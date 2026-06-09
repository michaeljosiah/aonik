import { useEffect, useState } from 'react';
import { X, Download, Pencil, RefreshCw, AlertCircle } from 'lucide-react';
import { Pill } from '@/components/layout/aonik/Pill';
import { Button } from '@/components/ui/button';
import { catalogService } from '@/services/catalogService';
import type { CatalogBillerSummaryItem, CatalogBillerServiceItem } from '@/types';
import { billerColor, billerInitials, connectorColor, formatSyncTime } from './billerVisuals';

interface BillerDetailDrawerProps {
  biller: CatalogBillerSummaryItem;
  categoryName?: string;
  countryName?: string;
  onClose: () => void;
  onEdit: (biller: CatalogBillerSummaryItem) => void;
  onViewDetails: (biller: CatalogBillerSummaryItem) => void;
}

const DASH = '—';

export function BillerDetailDrawer({
  biller,
  categoryName,
  countryName,
  onClose,
  onEdit,
  onViewDetails,
}: BillerDetailDrawerProps) {
  const [services, setServices] = useState<CatalogBillerServiceItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const imported = (biller.sourceConnectors?.length ?? 0) > 0;
  const sourceLabel = biller.sourceConnectors?.join(', ') ?? '';
  const tile = billerColor(biller.name);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    catalogService
      .getTenantBillerServices(biller.billerId)
      .then((res) => {
        if (!cancelled) setServices(res.services);
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        const message =
          err && typeof err === 'object' && 'userMessage' in err
            ? String((err as { userMessage?: string }).userMessage ?? '')
            : '';
        setError(message || 'Failed to load services.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [biller.billerId]);

  return (
    <div className="fixed inset-0 z-50">
      <div className="absolute inset-0 bg-black/30" onClick={onClose} />
      <div className="absolute top-0 right-0 bottom-0 w-[520px] max-w-full bg-[var(--color-surface)] border-l border-[var(--color-border-light)] shadow-2xl flex flex-col">
        {/* Header */}
        <div className="px-6 py-4 border-b border-[var(--color-border-light)] flex items-start gap-3">
          <div
            className="w-11 h-11 rounded-lg flex items-center justify-center text-white font-bold text-[15px] flex-none"
            style={{ background: tile, filter: biller.isActive ? 'none' : 'grayscale(1)' }}
          >
            {billerInitials(biller.name)}
          </div>
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2">
              <span className="text-base font-bold text-[var(--color-text-primary)] truncate">{biller.name}</span>
              <Pill tone={biller.isActive ? 'success' : 'muted'} dot>
                {biller.isActive ? 'Active' : 'Inactive'}
              </Pill>
            </div>
            <div className="text-xs text-[var(--color-text-secondary)] mt-1">
              {categoryName ?? 'Uncategorized'} · {countryName ?? biller.countryCode}
            </div>
          </div>
          <button
            onClick={onClose}
            aria-label="Close"
            className="w-7 h-7 rounded-md border border-[var(--color-border-light)] grid place-items-center text-[var(--color-text-secondary)] hover:bg-[var(--color-surface-inset)]"
          >
            <X className="w-3.5 h-3.5" />
          </button>
        </div>

        <div className="flex-1 overflow-auto p-6 flex flex-col gap-5">
          {/* Provenance / mapping */}
          <div className="rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-4">
            <div className="flex items-center gap-2 mb-2">
              {imported ? (
                <Download className="w-3.5 h-3.5" style={{ color: connectorColor(sourceLabel) }} />
              ) : (
                <Pencil className="w-3.5 h-3.5 text-[var(--color-text-tertiary)]" />
              )}
              <span className="text-[12.5px] font-semibold text-[var(--color-text-primary)]">
                {imported ? `Imported from ${sourceLabel}` : 'Manually authored'}
              </span>
            </div>
            {imported && (
              <div className="flex flex-wrap gap-x-6 gap-y-1 text-[11.5px] text-[var(--color-text-secondary)]">
                <span>
                  provider biller code{' '}
                  <b className="font-mono text-[var(--color-text-primary)]">{biller.providerBillerCode ?? DASH}</b>
                </span>
                <span>
                  last sync{' '}
                  <b className="text-[var(--color-text-primary)]">{formatSyncTime(biller.lastSyncedAt) ?? DASH}</b>
                </span>
              </div>
            )}
            {!biller.isActive && (
              <div className="text-[11.5px] text-[var(--color-warning)] mt-2">
                This biller was no longer offered by the partner on the last import, so it was soft-deactivated.
                Its history and any orders are preserved.
              </div>
            )}
          </div>

          {/* KPIs — operational metrics are not modelled yet (Spec 040 O7), shown as "—". */}
          <div className="grid grid-cols-4 gap-2">
            {[
              ['Tx / mo', DASH],
              ['Success', DASH],
              ['p50 ETA', DASH],
              ['Fee', DASH],
            ].map(([label, value]) => (
              <div key={label} className="bg-[var(--color-surface-inset)] rounded-lg px-3 py-2">
                <div className="text-[9.5px] font-semibold text-[var(--color-text-tertiary)] uppercase tracking-wide">
                  {label}
                </div>
                <div className="font-mono text-[13.5px] font-semibold text-[var(--color-text-primary)] mt-1">
                  {value}
                </div>
              </div>
            ))}
          </div>

          {/* Services */}
          <div>
            <div className="flex items-center gap-2 mb-2.5">
              <span className="text-[13px] font-semibold text-[var(--color-text-primary)]">Services</span>
              <span className="font-mono text-[11px] font-semibold text-[var(--color-text-tertiary)] px-2 py-0.5 rounded-full bg-[var(--color-surface-inset)]">
                {services.length}
              </span>
              <div className="flex-1" />
              <span className="text-[11px] text-[var(--color-text-tertiary)]">packages this biller offers</span>
            </div>

            {error ? (
              <div className="rounded-lg border border-[var(--color-error)] bg-[var(--color-error-light)] p-3 flex items-center gap-2 text-[var(--color-error)] text-sm">
                <AlertCircle className="w-4 h-4" />
                <span>{error}</span>
              </div>
            ) : loading ? (
              <div className="rounded-lg border border-[var(--color-border-light)] p-8 text-center">
                <RefreshCw className="w-5 h-5 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                <p className="text-sm text-[var(--color-text-secondary)]">Loading services…</p>
              </div>
            ) : services.length === 0 ? (
              <div className="rounded-lg border border-[var(--color-border-light)] p-8 text-center text-sm text-[var(--color-text-secondary)]">
                No services on this biller yet.
              </div>
            ) : (
              <div className="rounded-lg border border-[var(--color-border-light)] overflow-hidden">
                <div className="grid grid-cols-[1fr_80px_84px_30px] gap-2.5 px-3 py-2 bg-[var(--color-surface-inset)] border-b border-[var(--color-border-light)] text-[9.5px] font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)]">
                  <div>Service · field</div>
                  <div>Type</div>
                  <div className="text-right">Amount</div>
                  <div />
                </div>
                {services.map((s, i) => {
                  const isFixed = (s.amountType ?? '').toLowerCase() === 'fixed';
                  const amount = s.fixedAmount != null ? `${s.currency} ${s.fixedAmount.toLocaleString('en-GB')}` : DASH;
                  return (
                    <div
                      key={s.serviceId}
                      className="grid grid-cols-[1fr_80px_84px_30px] gap-2.5 px-3 py-2.5 items-center"
                      style={{
                        borderTop: i ? '1px solid var(--color-border-light)' : 'none',
                        opacity: s.isActive ? 1 : 0.5,
                      }}
                    >
                      <div className="min-w-0">
                        <div className="text-[12.5px] font-medium text-[var(--color-text-primary)] truncate">{s.name}</div>
                        <div className="text-[10.5px] text-[var(--color-text-tertiary)] mt-0.5 truncate">
                          {s.customerFieldLabel ?? s.type}
                          {s.providerItemCode && (
                            <>
                              {' · '}
                              <span className="font-mono">{s.providerItemCode}</span>
                            </>
                          )}
                        </div>
                      </div>
                      <span
                        className="justify-self-start text-[9.5px] font-bold uppercase tracking-wide px-1.5 py-0.5 rounded font-mono"
                        style={{
                          color: isFixed ? '#0e7490' : '#b4741e',
                          background: (isFixed ? '#0e7490' : '#b4741e') + '18',
                        }}
                      >
                        {s.amountType ?? (isFixed ? 'Fixed' : 'Variable')}
                      </span>
                      <span
                        className="text-right font-mono text-[12px] font-semibold"
                        style={{ color: amount === DASH ? 'var(--color-text-tertiary)' : 'var(--color-text-primary)' }}
                      >
                        {amount}
                      </span>
                      <span
                        className="justify-self-center rounded-full"
                        title={s.isActive ? 'Active' : 'Inactive'}
                        style={{
                          width: 7,
                          height: 7,
                          background: s.isActive ? 'var(--color-success)' : 'var(--color-text-tertiary)',
                        }}
                      />
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        </div>

        {/* Footer */}
        <div className="flex-none px-6 py-3.5 border-t border-[var(--color-border-light)] bg-[var(--color-surface-inset)] flex justify-end gap-2">
          <Button variant="outline" size="sm" onClick={() => onViewDetails(biller)}>
            View details
          </Button>
          <Button size="sm" onClick={() => onEdit(biller)}>
            <Pencil className="w-3.5 h-3.5 mr-1.5" />
            Edit biller
          </Button>
        </div>
      </div>
    </div>
  );
}
