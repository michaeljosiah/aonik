import { useEffect, useState } from 'react';
import { X, Download, Pencil, RefreshCw, AlertCircle } from 'lucide-react';
import { Pill } from '@/components/layout/aonik/Pill';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Sheet, SheetContent, SheetTitle, SheetDescription, SheetClose } from '@/components/ui/sheet';
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
    <Sheet open onOpenChange={(open) => !open && onClose()}>
      <SheetContent size="md" className="bg-card">
        {/* Header */}
        <div className="px-6 py-4 border-b border-border flex items-start gap-3">
          <div
            className="w-11 h-11 rounded-lg flex items-center justify-center text-white font-bold text-[15px] flex-none"
            style={{ background: tile, filter: biller.isActive ? 'none' : 'grayscale(1)' }}
          >
            {billerInitials(biller.name)}
          </div>
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2">
              <SheetTitle className="text-base font-bold text-foreground truncate">{biller.name}</SheetTitle>
              <Pill tone={biller.isActive ? 'success' : 'muted'} dot>
                {biller.isActive ? 'Active' : 'Inactive'}
              </Pill>
            </div>
            <SheetDescription className="text-xs text-muted-foreground mt-1">
              {categoryName ?? 'Uncategorized'} · {countryName ?? biller.countryCode}
            </SheetDescription>
          </div>
          <SheetClose asChild>
            <Button variant="ghost" size="icon-sm" aria-label="Close">
              <X className="w-3.5 h-3.5" />
            </Button>
          </SheetClose>
        </div>

        <div className="flex-1 overflow-auto p-6 flex flex-col gap-5">
          {/* Provenance / mapping */}
          <div className="rounded-lg border border-border bg-muted p-4">
            <div className="flex items-center gap-2 mb-2">
              {imported ? (
                <Download className="w-3.5 h-3.5" style={{ color: connectorColor(sourceLabel) }} />
              ) : (
                <Pencil className="w-3.5 h-3.5 text-muted-foreground" />
              )}
              <span className="text-[12.5px] font-semibold text-foreground">
                {imported ? `Imported from ${sourceLabel}` : 'Manually authored'}
              </span>
            </div>
            {imported && (
              <div className="flex flex-wrap gap-x-6 gap-y-1 text-[11.5px] text-muted-foreground">
                <span>
                  provider biller code{' '}
                  <b className="font-mono text-foreground">{biller.providerBillerCode ?? DASH}</b>
                </span>
                <span>
                  last sync{' '}
                  <b className="text-foreground">{formatSyncTime(biller.lastSyncedAt) ?? DASH}</b>
                </span>
              </div>
            )}
            {!biller.isActive && (
              <div className="text-[11.5px] text-warning mt-2">
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
              <div key={label} className="bg-muted rounded-lg px-3 py-2">
                <div className="text-xs font-medium text-muted-foreground">
                  {label}
                </div>
                <div className="font-mono text-[13.5px] font-semibold text-foreground mt-1">
                  {value}
                </div>
              </div>
            ))}
          </div>

          {/* Services */}
          <div>
            <div className="flex items-center gap-2 mb-2.5">
              <span className="text-[13px] font-semibold text-foreground">Services</span>
              <span className="font-mono text-[11px] font-semibold text-muted-foreground px-2 py-0.5 rounded-full bg-muted">
                {services.length}
              </span>
              <div className="flex-1" />
              <span className="text-[11px] text-muted-foreground">packages this biller offers</span>
            </div>

            {error ? (
              <Alert variant="destructive">
                <AlertCircle />
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            ) : loading ? (
              <div className="rounded-lg border border-border p-8 text-center">
                <RefreshCw className="w-5 h-5 animate-spin mx-auto mb-2 text-muted-foreground" />
                <p className="text-sm text-muted-foreground">Loading services…</p>
              </div>
            ) : services.length === 0 ? (
              <div className="rounded-lg border border-border p-8 text-center text-sm text-muted-foreground">
                No services on this biller yet.
              </div>
            ) : (
              <div className="rounded-lg border border-border overflow-hidden">
                <div className="grid grid-cols-[1fr_80px_84px_30px] gap-2.5 px-3 py-2 bg-muted border-b border-border text-xs font-medium text-muted-foreground">
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
                        borderTop: i ? '1px solid var(--border)' : 'none',
                        opacity: s.isActive ? 1 : 0.5,
                      }}
                    >
                      <div className="min-w-0">
                        <div className="text-[12.5px] font-medium text-foreground truncate">{s.name}</div>
                        <div className="text-[10.5px] text-muted-foreground mt-0.5 truncate">
                          {s.customerFieldLabel ?? s.type}
                          {s.providerItemCode && (
                            <>
                              {' · '}
                              <span className="font-mono">{s.providerItemCode}</span>
                            </>
                          )}
                        </div>
                      </div>
                      <Badge variant={isFixed ? 'info' : 'warning'} className="justify-self-start">
                        {s.amountType ?? (isFixed ? 'Fixed' : 'Variable')}
                      </Badge>
                      <span
                        className="text-right font-mono text-[12px] font-semibold"
                        style={{ color: amount === DASH ? 'var(--muted-foreground)' : 'var(--foreground)' }}
                      >
                        {amount}
                      </span>
                      <span
                        className="justify-self-center rounded-full"
                        title={s.isActive ? 'Active' : 'Inactive'}
                        style={{
                          width: 7,
                          height: 7,
                          background: s.isActive ? 'var(--success)' : 'var(--muted-foreground)',
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
        <div className="flex-none px-6 py-3.5 border-t border-border bg-muted flex justify-end gap-2">
          <Button variant="outline" size="sm" onClick={() => onViewDetails(biller)}>
            View details
          </Button>
          <Button size="sm" onClick={() => onEdit(biller)}>
            <Pencil className="w-3.5 h-3.5 mr-1.5" />
            Edit biller
          </Button>
        </div>
      </SheetContent>
    </Sheet>
  );
}
