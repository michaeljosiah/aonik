import { useEffect, useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
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
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null);

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
            <h1 className="text-2xl font-bold text-foreground mb-2">FX Rate Management</h1>
            <p className="text-muted-foreground">
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
                <ArrowRightLeft className="w-5 h-5 text-primary" />
                FX Quotes
              </CardTitle>
              <CardDescription>Current and historical exchange rate quotes</CardDescription>
            </div>
            <div className="flex items-center gap-2">
              <label className="flex items-center gap-2 text-sm text-muted-foreground cursor-pointer">
                <Checkbox
                  checked={includeExpired}
                  onCheckedChange={(checked) => setIncludeExpired(checked === true)}
                />
                Show expired
              </label>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {loading && (
            <div className="text-center py-8 text-muted-foreground">Loading quotes...</div>
          )}

          {error && (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertTitle>Error loading quotes</AlertTitle>
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          {!loading && !error && quotes.length === 0 && (
            <div className="text-center py-12">
              <ArrowRightLeft className="w-12 h-12 mx-auto text-muted-foreground mb-4" />
              <h3 className="text-lg font-semibold text-foreground mb-2">No FX quotes found</h3>
              <p className="text-muted-foreground mb-4">
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
                      ? 'border-border bg-muted/50 opacity-60'
                      : 'border-border hover:border-primary transition-colors'
                  }`}
                >
                  <div className="flex-1 grid grid-cols-12 gap-4 items-center">
                    <div className="col-span-3">
                      <div className="flex items-center gap-2">
                        <div className="font-semibold text-foreground">
                          {quote.baseCurrency} → {quote.targetCurrency}
                        </div>
                        {isExpired(quote.expiresAt) && (
                          <Badge variant="secondary" className="text-xs">
                            Expired
                          </Badge>
                        )}
                      </div>
                      {quote.provider && (
                        <div className="text-xs text-muted-foreground mt-1">{quote.provider}</div>
                      )}
                    </div>

                    <div className="col-span-2">
                      <div className="text-sm text-muted-foreground">Rate</div>
                      <div className="font-mono tabular-nums font-semibold text-foreground">
                        {quote.rate.toFixed(6)}
                      </div>
                    </div>

                    <div className="col-span-2">
                      <div className="text-sm text-muted-foreground">Expires</div>
                      <div className="text-sm text-foreground">{formatDateTime(quote.expiresAt)}</div>
                    </div>

                    <div className="col-span-2">
                      <div className="text-sm text-muted-foreground">Time left</div>
                      <div
                        className={`text-sm font-semibold ${
                          isExpired(quote.expiresAt)
                            ? 'text-destructive'
                            : getTimeUntilExpiry(quote.expiresAt).endsWith('m')
                              ? 'text-warning'
                              : 'text-success'
                        }`}
                      >
                        {getTimeUntilExpiry(quote.expiresAt)}
                      </div>
                    </div>

                    <div className="col-span-2">
                      <div className="text-sm text-muted-foreground">Created</div>
                      <div className="text-sm text-foreground">{formatDateTime(quote.createdAt)}</div>
                    </div>

                    <div className="col-span-1 flex justify-end gap-2">
                      <Button variant="ghost" size="sm" onClick={() => handleEdit(quote.id)}>
                        Edit
                      </Button>
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <Button
                            variant="ghost"
                            size="icon-sm"
                            onClick={() => setPendingDeleteId(quote.id)}
                            className="text-destructive hover:text-destructive"
                            aria-label="Delete FX quote"
                          >
                            <Trash2 className="w-4 h-4" />
                          </Button>
                        </TooltipTrigger>
                        <TooltipContent>Delete FX quote</TooltipContent>
                      </Tooltip>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <div className="mt-6 grid gap-3 md:grid-cols-3">
        <Card className="hover:border-primary transition-colors cursor-pointer">
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              <Settings className="w-4 h-4 text-primary" />
              Rate Sources
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              Configure external FX rate providers and fallback sources
            </p>
          </CardContent>
        </Card>

        <Card className="hover:border-primary transition-colors cursor-pointer">
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              <TrendingUp className="w-4 h-4 text-primary" />
              Spread Policies
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              Manage markup policies by currency corridor and customer tier
            </p>
          </CardContent>
        </Card>

        <Card className="hover:border-primary transition-colors cursor-pointer">
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              <Clock className="w-4 h-4 text-primary" />
              Refresh Schedules
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              Set up automated rate refresh intervals and schedules
            </p>
          </CardContent>
        </Card>
      </div>

      <FxQuoteDialog open={dialogOpen} onOpenChange={setDialogOpen} quote={selectedQuote} onSuccess={handleDialogSuccess} />

      <AlertDialog
        open={pendingDeleteId !== null}
        onOpenChange={(open) => {
          if (!open) setPendingDeleteId(null);
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete FX quote?</AlertDialogTitle>
            <AlertDialogDescription>Are you sure you want to delete this FX quote?</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              onClick={() => {
                const id = pendingDeleteId;
                setPendingDeleteId(null);
                if (id) void handleDelete(id);
              }}
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
