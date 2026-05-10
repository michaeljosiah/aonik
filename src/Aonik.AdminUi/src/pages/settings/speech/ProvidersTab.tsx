import { useCallback, useEffect, useMemo, useState } from 'react';
import { Loader2, Plus } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Switch } from '@/components/ui/switch';
import { speechProviderLibraryService, speechVendorsCatalogService } from '@/services/speechProviderLibraryService';
import type {
  SpeechProvider,
  SpeechProviderType,
  SpeechVendorDescriptor,
} from '@/types/speechLibrary';

import { ProviderCard } from './ProviderCard';
import { ProviderEditPanel } from './ProviderEditPanel';

type Filter = 'All' | SpeechProviderType;
const FILTERS: Filter[] = ['All', 'Stt', 'Tts', 'Composite'];
const FILTER_LABEL: Record<Filter, string> = {
  All: 'All',
  Stt: 'Speech-to-text',
  Tts: 'Text-to-speech',
  Composite: 'Composite',
};

/**
 * Providers tab — list of every speech provider in the library plus an inline
 * edit panel for create / update / clone. Built-in archetypes get a "Clone"
 * action; tenant-owned rows get "Edit" + "Disable".
 */
export function ProvidersTab() {
  const [providers, setProviders] = useState<SpeechProvider[]>([]);
  const [vendors, setVendors] = useState<SpeechVendorDescriptor[]>([]);
  const [filter, setFilter] = useState<Filter>('All');
  const [includeDisabled, setIncludeDisabled] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Edit panel state. Either `editing` is set (existing provider — edit or clone) or
  // `creating` is true (fresh row — defaultType seeds the initial form).
  const [editing, setEditing] = useState<SpeechProvider | null>(null);
  const [creating, setCreating] = useState<{ defaultType: SpeechProviderType } | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [list, catalog] = await Promise.all([
        speechProviderLibraryService.list({ includeDisabled }),
        speechVendorsCatalogService.get(),
      ]);
      setProviders(list);
      setVendors(catalog.vendors);
    } catch (err) {
      // eslint-disable-next-line no-console
      console.error(err);
      setError('Failed to load speech library.');
    } finally {
      setLoading(false);
    }
  }, [includeDisabled]);

  useEffect(() => {
    void load();
  }, [load]);

  const visible = useMemo(() => {
    if (filter === 'All') return providers;
    return providers.filter((p) => p.type === filter);
  }, [filter, providers]);

  const grouped = useMemo(() => {
    // Built-ins first within each type, alphabetical otherwise. Backend already sorts but
    // we re-group here so the type headings render cleanly.
    const out: Record<SpeechProviderType, SpeechProvider[]> = { Stt: [], Tts: [], Composite: [] };
    for (const p of visible) out[p.type].push(p);
    return out;
  }, [visible]);

  const handleDisable = async (provider: SpeechProvider) => {
    try {
      await speechProviderLibraryService.setStatus(provider.id, { status: 'Disabled' });
      toast.success(`${provider.displayName} disabled.`);
      void load();
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        (err as { message?: string })?.message ??
        'Failed to disable provider.';
      toast.error(message);
    }
  };

  const handleSaved = (saved: SpeechProvider) => {
    setEditing(null);
    setCreating(null);
    void load();
    void saved; // we just refresh from the server
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Loading speech library…
      </div>
    );
  }
  if (error) {
    return (
      <Card>
        <CardContent className="p-6 text-destructive">{error}</CardContent>
      </Card>
    );
  }

  // When the edit panel is open, render it instead of the list (so the page reads top-down).
  if (editing || creating) {
    return (
      <ProviderEditPanel
        initial={editing}
        defaultType={editing?.type ?? creating?.defaultType ?? 'Tts'}
        vendors={vendors}
        onSaved={handleSaved}
        onCancel={() => {
          setEditing(null);
          setCreating(null);
        }}
      />
    );
  }

  return (
    <div className="space-y-6">
      {/* Toolbar */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2">
          {FILTERS.map((f) => (
            <Button
              key={f}
              variant={filter === f ? 'default' : 'outline'}
              size="sm"
              onClick={() => setFilter(f)}
            >
              {FILTER_LABEL[f]}
              {f !== 'All' && (
                <Badge variant="outline" className="ml-1">
                  {providers.filter((p) => p.type === f).length}
                </Badge>
              )}
            </Button>
          ))}
        </div>

        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2">
            <Switch
              id="include-disabled"
              checked={includeDisabled}
              onCheckedChange={setIncludeDisabled}
            />
            <label htmlFor="include-disabled" className="text-xs text-muted-foreground">
              Show disabled
            </label>
          </div>
          <Button
            onClick={() => setCreating({ defaultType: filter !== 'All' ? filter : 'Tts' })}
            size="sm"
          >
            <Plus className="h-4 w-4" />
            Add provider
          </Button>
        </div>
      </div>

      {/* Provider list — grouped by type when filter = All; flat list when filtered. */}
      {filter === 'All' ? (
        <div className="space-y-6">
          {(['Stt', 'Tts', 'Composite'] as SpeechProviderType[]).map((t) => (
            <TypeSection
              key={t}
              type={t}
              providers={grouped[t]}
              onAdd={() => setCreating({ defaultType: t })}
              onEdit={(p) => setEditing(p)}
              onDisable={(p) => void handleDisable(p)}
            />
          ))}
        </div>
      ) : (
        <div className="space-y-2">
          {visible.length === 0 ? (
            <EmptyState onAdd={() => setCreating({ defaultType: filter as SpeechProviderType })} />
          ) : (
            visible.map((p) => (
              <ProviderCard
                key={p.id}
                provider={p}
                onEdit={() => setEditing(p)}
                onTest={() => setEditing(p)}
                onDisable={() => void handleDisable(p)}
              />
            ))
          )}
        </div>
      )}
    </div>
  );
}

function TypeSection({
  type,
  providers,
  onAdd,
  onEdit,
  onDisable,
}: {
  type: SpeechProviderType;
  providers: SpeechProvider[];
  onAdd: () => void;
  onEdit: (p: SpeechProvider) => void;
  onDisable: (p: SpeechProvider) => void;
}) {
  if (providers.length === 0) return null;

  const heading: Record<SpeechProviderType, string> = {
    Stt: 'Speech-to-text',
    Tts: 'Text-to-speech',
    Composite: 'Composite (single-vendor realtime)',
  };

  return (
    <section className="space-y-2">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold tracking-tight">{heading[type]}</h2>
        <Button variant="ghost" size="sm" onClick={onAdd}>
          <Plus className="h-3 w-3" />
          New {type === 'Stt' ? 'STT' : type === 'Tts' ? 'TTS' : 'composite'}
        </Button>
      </div>
      <div className="space-y-2">
        {providers.map((p) => (
          <ProviderCard
            key={p.id}
            provider={p}
            onEdit={() => onEdit(p)}
            onTest={() => onEdit(p)}
            onDisable={() => onDisable(p)}
          />
        ))}
      </div>
    </section>
  );
}

function EmptyState({ onAdd }: { onAdd: () => void }) {
  return (
    <Card>
      <CardContent className="flex flex-col items-center gap-3 p-12 text-center">
        <p className="text-sm text-muted-foreground">No providers in this category yet.</p>
        <Button onClick={onAdd}>
          <Plus className="h-4 w-4" />
          Add provider
        </Button>
      </CardContent>
    </Card>
  );
}
