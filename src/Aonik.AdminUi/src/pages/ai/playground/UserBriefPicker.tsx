import { useState, useEffect, useCallback } from 'react';
import { sampleBriefs } from '@/data/sampleBriefs';
import { playgroundService } from '@/services/aiService';
import { customerService } from '@/services/customerService';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { Check, AlertCircle, Search, Loader2, User } from 'lucide-react';
import type { CustomerListItem } from '@/types';

interface UserBriefPickerProps {
  value: string | null;
  onChange: (json: string | null) => void;
}

export function UserBriefPicker({ value, onChange }: UserBriefPickerProps) {
  const [search, setSearch] = useState('');
  const [customers, setCustomers] = useState<CustomerListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [searching, setSearching] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selectedPartyId, setSelectedPartyId] = useState<string | null>(null);
  // Track which source the current selection came from
  const [selectionSource, setSelectionSource] = useState<'samples' | 'real' | 'manual' | null>(null);

  const pageSize = 10;

  const loadCustomers = useCallback(async (searchTerm: string, pageNumber: number) => {
    setSearching(true);
    setError(null);
    try {
      const result = await customerService.list({
        search: searchTerm || undefined,
        pageNumber: pageNumber,
        pageSize,
        status: 'Active',
      });
      setCustomers(result.items);
      setTotalCount(result.totalCount);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setSearching(false);
    }
  }, []);

  // Load initial customer list when the Real User tab is shown
  useEffect(() => {
    loadCustomers('', 1);
  }, [loadCustomers]);

  // Debounced search
  useEffect(() => {
    const timer = setTimeout(() => {
      setPage(1);
      loadCustomers(search, 1);
    }, 300);
    return () => clearTimeout(timer);
  }, [search, loadCustomers]);

  const handlePageChange = (newPage: number) => {
    setPage(newPage);
    loadCustomers(search, newPage);
  };

  const handleSelectCustomer = async (customer: CustomerListItem) => {
    setSelectedPartyId(customer.partyId);
    setSelectionSource('real');
    setLoading(true);
    setError(null);
    // Immediately clear the previous brief so stale context (e.g. from a
    // sample user) is never used while the real user's context loads.
    onChange(null);
    try {
      const brief = await playgroundService.projectUserBrief({ partyId: customer.partyId });
      onChange(JSON.stringify(brief, null, 2));
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  // When the active tab changes, clear cross-tab selection state so the
  // new tab starts with a clean slate.
  const handleTabChange = (tab: string) => {
    if (tab === 'samples' && selectionSource !== 'samples') {
      setSelectedPartyId(null);
    } else if (tab === 'real' && selectionSource !== 'real') {
      // Don't clear the brief here — let the user pick a customer first
    } else if (tab === 'manual' && selectionSource !== 'manual') {
      setSelectedPartyId(null);
    }
  };

  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div className="space-y-1.5">
      <Label className="text-xs">User Brief</Label>

      <Tabs defaultValue="samples" onValueChange={handleTabChange}>
        <TabsList className="w-full">
          <TabsTrigger value="samples" className="flex-1 text-xs">Samples</TabsTrigger>
          <TabsTrigger value="real" className="flex-1 text-xs">Real User</TabsTrigger>
          <TabsTrigger value="manual" className="flex-1 text-xs">Manual</TabsTrigger>
        </TabsList>

        {/* Sample briefs */}
        <TabsContent value="samples">
          <div className="space-y-1">
            <button
              onClick={() => { onChange(null); setSelectionSource(null); setSelectedPartyId(null); }}
              className={`w-full rounded-[2px] border px-3 py-2 text-left text-xs transition-colors ${
                value === null
                  ? 'border-[var(--color-brand-primary)] bg-[var(--color-brand-primary-light)]'
                  : 'border-[var(--color-border-light)] hover:border-[var(--color-border)]'
              }`}
            >
              <span className="font-medium text-[var(--color-text-primary)]">None</span>
            </button>
            {sampleBriefs.map((brief) => (
              <button
                key={brief.id}
                onClick={() => { onChange(brief.json); setSelectionSource('samples'); setSelectedPartyId(null); }}
                className={`w-full rounded-[2px] border px-3 py-2 text-left text-xs transition-colors ${
                  value === brief.json
                    ? 'border-[var(--color-brand-primary)] bg-[var(--color-brand-primary-light)]'
                    : 'border-[var(--color-border-light)] hover:border-[var(--color-border)]'
                }`}
              >
                <div className="flex items-center gap-1.5">
                  <span className="font-medium text-[var(--color-text-primary)]">{brief.name}</span>
                  {value === brief.json && (
                    <Check className="h-3 w-3 text-[var(--color-brand-primary)]" />
                  )}
                </div>
                <span className="text-[var(--color-text-tertiary)]">{brief.description}</span>
              </button>
            ))}
          </div>
        </TabsContent>

        {/* Real user lookup — searchable customer list */}
        <TabsContent value="real">
          <div className="space-y-2">
            <div className="relative">
              <Search className="absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-[var(--color-text-tertiary)]" />
              <Input
                placeholder="Search customers by name, email, or phone..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="h-8 pl-8 text-xs"
              />
            </div>

            <div className="max-h-52 overflow-y-auto space-y-0.5">
              {searching && customers.length === 0 ? (
                <div className="flex items-center justify-center py-6 text-xs text-[var(--color-text-tertiary)]">
                  <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
                  Searching...
                </div>
              ) : customers.length === 0 ? (
                <div className="py-6 text-center text-xs text-[var(--color-text-tertiary)]">
                  No customers found
                </div>
              ) : (
                customers.map((customer) => {
                  const isSelected = selectedPartyId === customer.partyId;
                  const isLoading = isSelected && loading;
                  return (
                    <button
                      key={customer.partyId}
                      onClick={() => handleSelectCustomer(customer)}
                      disabled={loading}
                      className={`flex w-full items-center gap-2.5 rounded-[2px] border px-3 py-2 text-left text-xs transition-colors disabled:opacity-50 ${
                        isSelected
                          ? 'border-[var(--color-brand-primary)] bg-[var(--color-brand-primary-light)]'
                          : 'border-[var(--color-border-light)] hover:border-[var(--color-border)]'
                      }`}
                    >
                      {customer.photoUrlTiny ? (
                        <img
                          src={customer.photoUrlTiny}
                          alt=""
                          className="h-7 w-7 rounded-full object-cover"
                        />
                      ) : (
                        <div className="flex h-7 w-7 items-center justify-center rounded-full bg-[var(--color-bg-tertiary)]">
                          <User className="h-3.5 w-3.5 text-[var(--color-text-tertiary)]" />
                        </div>
                      )}
                      <div className="min-w-0 flex-1">
                        <div className="flex items-center gap-1.5">
                          <span className="truncate font-medium text-[var(--color-text-primary)]">
                            {customer.displayName}
                          </span>
                          {isLoading && (
                            <Loader2 className="h-3 w-3 flex-shrink-0 animate-spin text-[var(--color-brand-primary)]" />
                          )}
                          {isSelected && !isLoading && (
                            <Check className="h-3 w-3 flex-shrink-0 text-[var(--color-brand-primary)]" />
                          )}
                        </div>
                        <span className="truncate text-[var(--color-text-tertiary)]">
                          {customer.primaryEmail ?? customer.primaryPhone ?? customer.partyType}
                        </span>
                      </div>
                    </button>
                  );
                })
              )}
            </div>

            {/* Pagination */}
            {totalPages > 1 && (
              <div className="flex items-center justify-between pt-1 text-xs text-[var(--color-text-tertiary)]">
                <span>{totalCount} customer{totalCount !== 1 ? 's' : ''}</span>
                <div className="flex items-center gap-1">
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-6 px-2 text-xs"
                    disabled={page <= 1 || searching}
                    onClick={() => handlePageChange(page - 1)}
                  >
                    Prev
                  </Button>
                  <span>{page} / {totalPages}</span>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-6 px-2 text-xs"
                    disabled={page >= totalPages || searching}
                    onClick={() => handlePageChange(page + 1)}
                  >
                    Next
                  </Button>
                </div>
              </div>
            )}

            {error && (
              <div className="flex items-center gap-2 text-xs text-[var(--color-error)]">
                <AlertCircle className="h-3 w-3" />
                {error}
              </div>
            )}
          </div>
        </TabsContent>

        {/* Manual JSON editor */}
        <TabsContent value="manual">
          <Textarea
            value={value ?? ''}
            onChange={(e) => { onChange(e.target.value || null); setSelectionSource('manual'); setSelectedPartyId(null); }}
            placeholder="Paste User Brief JSON..."
            rows={6}
            className="font-mono text-xs"
          />
        </TabsContent>
      </Tabs>

      {value && (
        <p className="text-xs text-[var(--color-text-tertiary)]">
          Brief loaded (~{Math.round(value.length / 4)} tokens)
        </p>
      )}
    </div>
  );
}
