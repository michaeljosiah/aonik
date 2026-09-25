// Searchable party dropdown — visual port of `PartyPicker` from
// templates/aonik-admin-starterkit/screens/orders.jsx.
//
// Wires to /admin/customers (customerService.list) for search and
// /parties (partyService.createParty) for inline "Add new party". Replaces
// the raw UUID input + modal flow of the old form.

import { useEffect, useMemo, useState } from 'react';
import { Check, ChevronDown, ChevronUp, Plus, Search, Users } from 'lucide-react';
import { customerService } from '@/services/customerService';
import { partyService } from '@/services/partyService';
import { PartyAvatar } from './PartyAvatar';
import { cn } from '@/lib/utils';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { NativeSelect } from '@/components/ui/native-select';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import type { CustomerListItem, PartyResponse } from '@/types';

export interface PartyPickerOption {
  partyId: string;
  displayName: string;
  partyType: string;
  primaryEmail?: string | null;
  primaryPhone?: string | null;
  /** Optional tier label (only known if the party was loaded from the customer list). */
  tier?: string | null;
}

export interface PartyPickerProps {
  label: string;
  value: string;
  onChange: (partyId: string, party: PartyPickerOption) => void;
  /** Party ids to hide from the dropdown (e.g. the payer when picking a beneficiary). */
  excludeIds?: string[];
  placeholder?: string;
  /** Pre-loaded option for the current value, so the trigger renders a name on first paint. */
  preloaded?: PartyPickerOption | null;
}

function toOption(item: CustomerListItem): PartyPickerOption {
  return {
    partyId: item.partyId,
    displayName: item.displayName,
    partyType: item.partyType,
    primaryEmail: item.primaryEmail,
    primaryPhone: item.primaryPhone,
  };
}

function fromPartyResponse(party: PartyResponse): PartyPickerOption {
  return {
    partyId: party.partyId,
    displayName: party.displayName,
    partyType: party.partyType,
  };
}

// Inline pill matching the template's `.pill` (white bg + light border +
// primary text), with a teal-tint variant for Gold tier. Inlined here
// instead of using the workspace `Pill` primitive because that primitive's
// `default` tone is intentionally muted (gray bg + secondary text) for
// status cells — the template's PartyPicker calls for the more emphatic
// neutral pill style.
function PartyTagPill({ tier, fallback }: { tier: string | null; fallback: string }) {
  const isGold = tier === 'Gold';
  const label = tier ?? fallback;
  return (
    <span
      className={cn(
        'inline-flex flex-none items-center rounded-full border px-1.5 py-0.5 text-[10px] font-medium leading-none',
        isGold
          ? 'border-transparent bg-primary/10 text-primary'
          : 'border-border bg-card text-foreground',
      )}
    >
      {label}
    </span>
  );
}

export function PartyPicker({
  label,
  value,
  onChange,
  excludeIds = [],
  placeholder = 'Select party',
  preloaded,
}: PartyPickerProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<PartyPickerOption[]>([]);
  const [loading, setLoading] = useState(false);
  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [draftName, setDraftName] = useState('');
  const [draftType, setDraftType] = useState<'Person' | 'Business'>('Person');

  const selected = useMemo(() => {
    if (!value) return null;
    if (preloaded && preloaded.partyId === value) return preloaded;
    return results.find((r) => r.partyId === value) ?? null;
  }, [value, preloaded, results]);

  // Closing (outside click, Escape, trigger) also leaves the create form.
  const handleOpenChange = (next: boolean) => {
    setOpen(next);
    if (!next) setShowCreate(false);
  };

  // Search customers when the dropdown is open and the query changes.
  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setLoading(true);
    const handle = window.setTimeout(async () => {
      try {
        const result = await customerService.list({
          search: query || undefined,
          pageSize: 25,
        });
        if (cancelled) return;
        setResults(result.items.map(toOption).filter((o) => !excludeIds.includes(o.partyId)));
      } catch {
        if (cancelled) return;
        setResults([]);
      } finally {
        if (!cancelled) setLoading(false);
      }
    }, 200);
    return () => {
      cancelled = true;
      window.clearTimeout(handle);
    };
  }, [open, query, excludeIds]);

  const handleCreate = async () => {
    const trimmed = draftName.trim();
    if (!trimmed) {
      setCreateError('Display name is required.');
      return;
    }
    setCreating(true);
    setCreateError(null);
    try {
      const party = await partyService.createParty({
        displayName: trimmed,
        partyType: draftType,
      });
      const option = fromPartyResponse(party);
      onChange(option.partyId, option);
      setResults((prev) => [option, ...prev.filter((p) => p.partyId !== option.partyId)]);
      setOpen(false);
      setShowCreate(false);
      setDraftName('');
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setCreateError(message || 'Unable to create party.');
    } finally {
      setCreating(false);
    }
  };

  return (
    <div className="flex flex-col gap-1">
      <div className="text-xs font-medium text-muted-foreground">
        {label}
      </div>
      <Popover open={open} onOpenChange={handleOpenChange}>
        <PopoverTrigger asChild>
        <button
          type="button"
          aria-label={label}
          className={cn(
            'flex w-full items-center gap-2.5 rounded-lg px-3 py-2 transition-colors',
            'bg-muted',
            open
              ? 'border-[1.5px] border-primary ring-[3px] ring-primary/10'
              : 'border-[1.5px] border-border hover:border-border',
          )}
        >
          {selected ? (
            <>
              <PartyAvatar name={selected.displayName} size={32} />
              <div className="min-w-0 flex-1 text-left">
                <div className="truncate text-[13px] font-semibold text-foreground">
                  {selected.displayName}
                </div>
                <div className="truncate text-[11px] text-muted-foreground">
                  {selected.partyType}
                  {selected.tier ? ` · ${selected.tier}` : ''}
                </div>
              </div>
            </>
          ) : (
            <>
              <span className="grid h-8 w-8 flex-none place-items-center rounded-md border border-dashed border-border bg-card">
                <Users className="h-3.5 w-3.5 text-muted-foreground" />
              </span>
              <span className="flex-1 text-left text-[13px] text-muted-foreground">
                {placeholder}
              </span>
            </>
          )}
          {open ? (
            <ChevronUp className="h-3.5 w-3.5 text-muted-foreground" />
          ) : (
            <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" />
          )}
        </button>
        </PopoverTrigger>

          <PopoverContent
            align="start"
            sideOffset={6}
            className="w-(--radix-popover-trigger-width) overflow-hidden rounded-xl bg-card p-0 shadow-lg"
          >
            {!showCreate ? (
              <>
                <div className="border-b border-border px-3 py-2.5">
                  <div className="relative">
                    <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3 w-3 -translate-y-1/2 text-muted-foreground" />
                    <Input
                      autoFocus
                      type="text"
                      value={query}
                      onChange={(e) => setQuery(e.target.value)}
                      placeholder="Search parties…"
                      aria-label="Search parties"
                      className="h-[34px] pl-8 text-[12.5px]"
                    />
                  </div>
                </div>
                <div className="max-h-[280px] overflow-auto">
                  {loading && (
                    <div className="px-3.5 py-5 text-center text-[12.5px] text-muted-foreground">
                      Searching…
                    </div>
                  )}
                  {!loading && results.length === 0 && (
                    <div className="px-3.5 py-5 text-center text-[12.5px] text-muted-foreground">
                      No parties found
                    </div>
                  )}
                  {!loading &&
                    results.map((party) => (
                      <button
                        key={party.partyId}
                        type="button"
                        onClick={() => {
                          onChange(party.partyId, party);
                          handleOpenChange(false);
                          setQuery('');
                        }}
                        className={cn(
                          'flex w-full items-center gap-3 border-b border-border px-3.5 py-2.5 text-left transition-colors last:border-b-0',
                          party.partyId === value
                            ? 'bg-primary/10'
                            : 'hover:bg-muted',
                        )}
                      >
                        <PartyAvatar name={party.displayName} size={36} />
                        <div className="min-w-0 flex-1">
                          <div className="flex items-center gap-1.5">
                            <span className="truncate text-[13px] font-semibold text-foreground">
                              {party.displayName}
                            </span>
                            <PartyTagPill tier={party.tier ?? null} fallback={party.partyType} />
                          </div>
                          <div className="mt-0.5 truncate text-[11px] text-muted-foreground">
                            {[party.primaryEmail, party.primaryPhone].filter(Boolean).join(' · ')}
                          </div>
                        </div>
                        {party.partyId === value && (
                          <Check className="h-3.5 w-3.5 text-primary" />
                        )}
                      </button>
                    ))}
                </div>
                <div className="border-t border-border px-3 py-2">
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    onClick={() => {
                      setShowCreate(true);
                      setDraftName(query);
                      setCreateError(null);
                    }}
                    className="h-[30px] w-full text-[12px] text-muted-foreground"
                  >
                    <Plus className="h-3 w-3" />
                    Add new party
                  </Button>
                </div>
              </>
            ) : (
              <div className="space-y-3 px-3.5 py-3">
                <div className="text-xs font-medium text-muted-foreground">
                  New party
                </div>
                {createError && (
                  <Alert variant="destructive" className="px-2.5 py-1.5">
                    <AlertDescription className="text-[11px]">{createError}</AlertDescription>
                  </Alert>
                )}
                <label className="block text-[11px] text-muted-foreground">
                  Display name
                  <Input
                    type="text"
                    value={draftName}
                    onChange={(e) => setDraftName(e.target.value)}
                    className="mt-1 h-[34px] px-2.5 text-[12.5px]"
                  />
                </label>
                <label className="block text-[11px] text-muted-foreground">
                  Type
                  <div className="mt-1">
                    <NativeSelect
                      value={draftType}
                      onChange={(e) => setDraftType(e.target.value as 'Person' | 'Business')}
                      className="h-[34px] pl-2.5 text-[12.5px]"
                    >
                      <option value="Person">Person</option>
                      <option value="Business">Business</option>
                    </NativeSelect>
                  </div>
                </label>
                <div className="flex gap-2">
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    onClick={() => setShowCreate(false)}
                    className="h-[30px] flex-1 text-[12px] text-muted-foreground"
                  >
                    Cancel
                  </Button>
                  <Button
                    type="button"
                    size="sm"
                    onClick={handleCreate}
                    disabled={creating}
                    className="h-[30px] flex-1 text-[12px]"
                  >
                    {creating ? 'Creating…' : 'Create'}
                  </Button>
                </div>
              </div>
            )}
          </PopoverContent>
      </Popover>
    </div>
  );
}
