import { useEffect, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ArrowRightLeft, Plus, Settings, TrendingUp, Clock, AlertCircle, Trash2 } from 'lucide-react';
import { FxQuoteDialog } from '@/components/FxQuoteDialog';
import { fxRateService } from '@/services/fxRateService';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import type { FxQuoteListResponse, FxQuoteDetailResponse } from '@/types';

export function FxRatesPage() {
  const [quotes, setQuotes] = useState<FxQuoteListResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [includeExpired, setIncludeExpired] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [selectedQuote, setSelectedQuote] = useState<FxQuoteDetailResponse | undefined>(undefined);

  useEffect(() => {
    loadQuotes();
  }, [includeExpired]);

  const loadQuotes = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await fxRateService.getAll({ includeExpired });
      setQuotes(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load FX quotes');
    } finally {
      setLoading(false);
      setInitialLoad(false);
    }
  };

  const formatDateTime = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleString('en-US', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const isExpired = (expiresAt: string) => {
    return new Date(expiresAt) < new Date();
  };

  const getTimeUntilExpiry = (expiresAt: string) => {
    const now = new Date();
    const expiry = new Date(expiresAt);
    const diff = expiry.getTime() - now.getTime();

    if (diff < 0) return 'Expired';

    const minutes = Math.floor(diff / 60000);
    const hours = Math.floor(minutes / 60);
    const days = Math.floor(hours / 24);

    if (days > 0) return `${days}d`;
    if (hours > 0) return `${hours}h`;
    return `${minutes}m`;
  };

  const handleCreate = () => {
    setSelectedQuote(undefined);
    setDialogOpen(true);
  };

  const handleEdit = async (id: string) => {
    try {
      const quote = await fxRateService.getById(id);
      setSelectedQuote(quote);
      setDialogOpen(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load quote');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this FX quote?')) return;

    try {
      await fxRateService.delete(id);
      await loadQuotes();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete quote');
    }
  };

  const handleDialogSuccess = () => {
    loadQuotes();
  };

  if (initialLoad) {
    return <PageLoadingScreen message="Loading exchange rates" />;
  }

  return (
    <div className="h-full overflow-auto p-6">

      <div className="flex flex-col gap-4 mb-6">
        <div className="flex flex-col md:flex-row md:items-start md:justify-between gap-4">
          <div className="flex-1 max-w-[48rem]">
            <h1 className="text-2xl font-bold text-[var(--color-text-primary)] mb-2">FX Rate Management</h1>
            <p className="text-[var(--color-text-secondary)]">
              Manage foreign exchange quotes, rate sources, spread policies, and refresh schedules.
            </p>
          </div>
          <div className="flex flex-wrap gap-2 md:flex-shrink-0">
          <Button variant="outline" className="gap-2">
            <Settings className="w-4 h-4" />
            Rate Sources
          </Button>
          <Button variant="outline" className="gap-2">
            <TrendingUp className="w-4 h-4" />
            Spread Policies
          </Button>
          <Button variant="outline" className="gap-2">
            <Clock className="w-4 h-4" />
            Refresh Schedules
          </Button>
          <Button className="gap-2" onClick={handleCreate}>
            <Plus className="w-4 h-4" />
            New Quote
          </Button>
          </div>
        </div>
      </div>

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle className="flex items-center gap-2">
                <ArrowRightLeft className="w-5 h-5 text-[var(--color-brand-primary)]" />
                FX Quotes
              </CardTitle>
              <CardDescription>Current and historical exchange rate quotes</CardDescription>
            </div>
            <div className="flex items-center gap-2">
              <label className="flex items-center gap-2 text-sm text-[var(--color-text-secondary)] cursor-pointer">
                <input
                  type="checkbox"
                  checked={includeExpired}
                  onChange={(e) => setIncludeExpired(e.target.checked)}
                  className="rounded border-[var(--color-border-light)]"
                />
                Show expired
              </label>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {loading && (
            <div className="text-center py-8 text-[var(--color-text-secondary)]">Loading quotes...</div>
          )}

          {error && (
            <div className="flex items-center gap-2 p-4 rounded-lg bg-[color-mix(in_srgb,var(--color-danger)_8%,transparent)] border border-[color-mix(in_srgb,var(--color-danger)_25%,transparent)] text-[var(--color-danger)]">
              <AlertCircle className="w-5 h-5" />
              <div>
                <div className="font-semibold">Error loading quotes</div>
                <div className="text-sm">{error}</div>
              </div>
            </div>
          )}

          {!loading && !error && quotes.length === 0 && (
            <div className="text-center py-12">
              <ArrowRightLeft className="w-12 h-12 mx-auto text-[var(--color-text-tertiary)] mb-4" />
              <h3 className="text-lg font-semibold text-[var(--color-text-primary)] mb-2">No FX quotes found</h3>
              <p className="text-[var(--color-text-secondary)] mb-4">
                {includeExpired
                  ? 'No quotes available.'
                  : 'No active quotes found. Create your first quote or check expired quotes.'}
              </p>
              <Button className="gap-2" onClick={handleCreate}>
                <Plus className="w-4 h-4" />
                Create First Quote
              </Button>
            </div>
          )}

          {!loading && !error && quotes.length > 0 && (
            <div className="space-y-3">
              {quotes.map((quote) => (
                <div
                  key={quote.id}
                  className={`flex items-center justify-between p-4 rounded-lg border ${
                    isExpired(quote.expiresAt)
                      ? 'border-[var(--color-border-light)] bg-gray-50/50 opacity-60'
                      : 'border-[var(--color-border-light)] hover:border-[var(--color-brand-primary)] transition-colors'
                  }`}
                >
                  <div className="flex-1 grid grid-cols-12 gap-4 items-center">
                    <div className="col-span-3">
                      <div className="flex items-center gap-2">
                        <div className="font-semibold text-[var(--color-text-primary)]">
                          {quote.baseCurrency} → {quote.targetCurrency}
                        </div>
                        {isExpired(quote.expiresAt) && (
                          <Badge variant="secondary" className="text-xs">
                            Expired
                          </Badge>
                        )}
                      </div>
                      {quote.provider && (
                        <div className="text-xs text-[var(--color-text-tertiary)] mt-1">{quote.provider}</div>
                      )}
                    </div>

                    <div className="col-span-2">
                      <div className="text-sm text-[var(--color-text-secondary)]">Rate</div>
                      <div className="font-mono font-semibold text-[var(--color-text-primary)]">
                        {quote.rate.toFixed(6)}
                      </div>
                    </div>

                    <div className="col-span-2">
                      <div className="text-sm text-[var(--color-text-secondary)]">Expires</div>
                      <div className="text-sm text-[var(--color-text-primary)]">{formatDateTime(quote.expiresAt)}</div>
                    </div>

                    <div className="col-span-2">
                      <div className="text-sm text-[var(--color-text-secondary)]">Time left</div>
                      <div
                        className={`text-sm font-semibold ${
                          isExpired(quote.expiresAt)
                            ? 'text-[var(--color-danger)]'
                            : getTimeUntilExpiry(quote.expiresAt).endsWith('m')
                              ? 'text-[var(--color-warning)]'
                              : 'text-[var(--color-success)]'
                        }`}
                      >
                        {getTimeUntilExpiry(quote.expiresAt)}
                      </div>
                    </div>

                    <div className="col-span-2">
                      <div className="text-sm text-[var(--color-text-secondary)]">Created</div>
                      <div className="text-sm text-[var(--color-text-primary)]">{formatDateTime(quote.createdAt)}</div>
                    </div>

                    <div className="col-span-1 flex justify-end gap-2">
                      <Button variant="ghost" size="sm" onClick={() => handleEdit(quote.id)}>
                        Edit
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => handleDelete(quote.id)}
                        className="text-[var(--color-danger)] hover:text-[var(--color-danger)]"
                      >
                        <Trash2 className="w-4 h-4" />
                      </Button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <div className="mt-6 grid gap-3 md:grid-cols-3">
        <Card className="hover:border-[var(--color-brand-primary)] transition-colors cursor-pointer">
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              <Settings className="w-4 h-4 text-[var(--color-brand-primary)]" />
              Rate Sources
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-[var(--color-text-secondary)]">
              Configure external FX rate providers and fallback sources
            </p>
          </CardContent>
        </Card>

        <Card className="hover:border-[var(--color-brand-primary)] transition-colors cursor-pointer">
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              <TrendingUp className="w-4 h-4 text-[var(--color-brand-primary)]" />
              Spread Policies
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-[var(--color-text-secondary)]">
              Manage markup policies by currency corridor and customer tier
            </p>
          </CardContent>
        </Card>

        <Card className="hover:border-[var(--color-brand-primary)] transition-colors cursor-pointer">
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              <Clock className="w-4 h-4 text-[var(--color-brand-primary)]" />
              Refresh Schedules
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-[var(--color-text-secondary)]">
              Set up automated rate refresh intervals and schedules
            </p>
          </CardContent>
        </Card>
      </div>

      <FxQuoteDialog open={dialogOpen} onOpenChange={setDialogOpen} quote={selectedQuote} onSuccess={handleDialogSuccess} />
    </div>
  );
}
