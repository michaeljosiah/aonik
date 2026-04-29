// Searchable party dropdown — visual port of `PartyPicker` from
// templates/aonik-admin-starterkit/screens/orders.jsx.
//
// Wires to /admin/customers (customerService.list) for search and
// /parties (partyService.createParty) for inline "Add new party". Replaces
// the raw UUID input + modal flow of the old form.

import { useEffect, useMemo, useRef, useState } from 'react';
import { Check, ChevronDown, ChevronUp, Plus, Search, Users } from 'lucide-react';
import { customerService } from '@/services/customerService';
import { partyService } from '@/services/partyService';
import { PartyAvatar } from './PartyAvatar';
import { cn } from '@/lib/utils';
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
          ? 'border-transparent bg-[var(--color-brand-primary-10)] text-[var(--color-brand-primary)]'
          : 'border-[var(--color-border)] bg-[var(--color-surface)] text-[var(--color-text-primary)]',
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

  const containerRef = useRef<HTMLDivElement | null>(null);

  const selected = useMemo(() => {
    if (!value) return null;
    if (preloaded && preloaded.partyId === value) return preloaded;
    return results.find((r) => r.partyId === value) ?? null;
  }, [value, preloaded, results]);

  // Close on outside click.
  useEffect(() => {
    if (!open) return;
    const handler = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
        setShowCreate(false);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

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
      <div className="text-[11px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
        {label}
      </div>
      <div ref={containerRef} className="relative">
        <button
          type="button"
          onClick={() => setOpen((o) => !o)}
          className={cn(
            'flex w-full items-center gap-2.5 rounded-[10px] px-3 py-2 transition-colors',
            'bg-[var(--color-surface-inset)]',
            open
              ? 'border-[1.5px] border-[var(--color-brand-primary)] shadow-[0_0_0_3px_var(--color-brand-primary-10)]'
              : 'border-[1.5px] border-[var(--color-border-light)] hover:border-[var(--color-border)]',
          )}
        >
          {selected ? (
            <>
              <PartyAvatar name={selected.displayName} size={32} />
              <div className="min-w-0 flex-1 text-left">
                <div className="truncate text-[13px] font-semibold text-[var(--color-text-primary)]">
                  {selected.displayName}
                </div>
                <div className="truncate text-[11px] text-[var(--color-text-secondary)]">
                  {selected.partyType}
                  {selected.tier ? ` · ${selected.tier}` : ''}
                </div>
              </div>
            </>
          ) : (
            <>
              <span className="grid h-8 w-8 flex-none place-items-center rounded-md border border-dashed border-[var(--color-border)] bg-[var(--color-surface)]">
                <Users className="h-3.5 w-3.5 text-[var(--color-text-tertiary)]" />
              </span>
              <span className="flex-1 text-left text-[13px] text-[var(--color-text-tertiary)]">
                {placeholder}
              </span>
            </>
          )}
          {open ? (
            <ChevronUp className="h-3.5 w-3.5 text-[var(--color-text-secondary)]" />
          ) : (
            <ChevronDown className="h-3.5 w-3.5 text-[var(--color-text-secondary)]" />
          )}
        </button>

        {open && (
          <div className="absolute left-0 right-0 z-50 mt-1.5 overflow-hidden rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] shadow-[0_12px_32px_-8px_rgb(0_0_0_/_0.18)]">
            {!showCreate ? (
              <>
                <div className="border-b border-[var(--color-border-light)] px-3 py-2.5">
                  <div className="relative">
                    <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3 w-3 -translate-y-1/2 text-[var(--color-text-tertiary)]" />
                    <input
                      autoFocus
                      type="text"
                      value={query}
                      onChange={(e) => setQuery(e.target.value)}
                      placeholder="Search parties…"
                      className="aonik-input h-[34px] pl-8 text-[12.5px]"
                    />
                  </div>
                </div>
                <div className="max-h-[280px] overflow-auto">
                  {loading && (
                    <div className="px-3.5 py-5 text-center text-[12.5px] text-[var(--color-text-tertiary)]">
                      Searching…
                    </div>
                  )}
                  {!loading && results.length === 0 && (
                    <div className="px-3.5 py-5 text-center text-[12.5px] text-[var(--color-text-tertiary)]">
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
                          setOpen(false);
                          setQuery('');
                        }}
                        className={cn(
                          'flex w-full items-center gap-3 border-b border-[var(--color-border-light)] px-3.5 py-2.5 text-left transition-colors last:border-b-0',
                          party.partyId === value
                            ? 'bg-[var(--color-brand-primary-10)]'
                            : 'hover:bg-[var(--color-surface-inset)]',
                        )}
                      >
                        <PartyAvatar name={party.displayName} size={36} />
                        <div className="min-w-0 flex-1">
                          <div className="flex items-center gap-1.5">
                            <span className="truncate text-[13px] font-semibold text-[var(--color-text-primary)]">
                              {party.displayName}
                            </span>
                            <PartyTagPill tier={party.tier ?? null} fallback={party.partyType} />
                          </div>
                          <div className="mt-0.5 truncate text-[11px] text-[var(--color-text-secondary)]">
                            {[party.primaryEmail, party.primaryPhone].filter(Boolean).join(' · ')}
                          </div>
                        </div>
                        {party.partyId === value && (
                          <Check className="h-3.5 w-3.5 text-[var(--color-brand-primary)]" />
                        )}
                      </button>
                    ))}
                </div>
                <div className="border-t border-[var(--color-border-light)] px-3 py-2">
                  <button
                    type="button"
                    onClick={() => {
                      setShowCreate(true);
                      setDraftName(query);
                      setCreateError(null);
                    }}
                    className="flex h-[30px] w-full items-center justify-center gap-1.5 rounded-md text-[12px] font-medium text-[var(--color-text-secondary)] transition-colors hover:bg-[var(--color-surface-inset)] hover:text-[var(--color-text-primary)]"
                  >
                    <Plus className="h-3 w-3" />
                    Add new party
                  </button>
                </div>
              </>
            ) : (
              <div className="space-y-3 px-3.5 py-3">
                <div className="text-[11px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                  New party
                </div>
                {createError && (
                  <div className="rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-2.5 py-1.5 text-[11px] text-[var(--color-error)]">
                    {createError}
                  </div>
                )}
                <label className="block text-[11px] text-[var(--color-text-secondary)]">
                  Display name
                  <input
                    type="text"
                    value={draftName}
                    onChange={(e) => setDraftName(e.target.value)}
                    className="aonik-input mt-1 h-[34px] px-2.5 text-[12.5px]"
                  />
                </label>
                <label className="block text-[11px] text-[var(--color-text-secondary)]">
                  Type
                  <select
                    value={draftType}
                    onChange={(e) => setDraftType(e.target.value as 'Person' | 'Business')}
                    className="aonik-select mt-1 h-[34px] px-2.5 text-[12.5px]"
                  >
                    <option value="Person">Person</option>
                    <option value="Business">Business</option>
                  </select>
                </label>
                <div className="flex gap-2">
                  <button
                    type="button"
                    onClick={() => setShowCreate(false)}
                    className="h-[30px] flex-1 rounded-md text-[12px] text-[var(--color-text-secondary)] transition-colors hover:bg-[var(--color-surface-inset)] hover:text-[var(--color-text-primary)]"
                  >
                    Cancel
                  </button>
                  <button
                    type="button"
                    onClick={handleCreate}
                    disabled={creating}
                    className="h-[30px] flex-1 rounded-md bg-[var(--color-brand-primary)] text-[12px] font-medium text-white transition-colors hover:bg-[var(--color-brand-primary-dark)] disabled:opacity-60"
                  >
                    {creating ? 'Creating…' : 'Create'}
                  </button>
                </div>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
