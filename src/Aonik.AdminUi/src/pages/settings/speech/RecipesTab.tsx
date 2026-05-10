import { useCallback, useEffect, useMemo, useState } from 'react';
import { Bolt, Loader2, Plus } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Sheet, SheetContent } from '@/components/ui/sheet';
import { Switch } from '@/components/ui/switch';
import { cn } from '@/lib/utils';
import { speechProviderLibraryService } from '@/services/speechProviderLibraryService';
import { voiceRecipeLibraryService } from '@/services/voiceRecipeLibraryService';
import type { SpeechProvider } from '@/types/speechLibrary';
import type { VoiceRecipe, VoiceRecipeKind } from '@/types/voiceRecipes';

import { PageHeader } from './_primitives';
import { RecipeCard } from './RecipeCard';
import { RecipeStackEditor } from './RecipeStackEditor';

type Filter = 'All' | VoiceRecipeKind;
const FILTERS: { id: Filter; label: string }[] = [
  { id: 'All', label: 'All recipes' },
  { id: 'Chained', label: 'Chained' },
  { id: 'Composite', label: 'Realtime' },
];

/**
 * Recipes tab. Banner explains clone-before-edit, filter pills slice by kind, then a
 * vertical list of full-width recipe cards rendering the visual flow. Active-in-voice-
 * mode highlight is mocked from the first built-in chained recipe until Phase C wires
 * the real active-recipe setting.
 */
export function RecipesTab() {
  const [recipes, setRecipes] = useState<VoiceRecipe[]>([]);
  const [providers, setProviders] = useState<SpeechProvider[]>([]);
  const [filter, setFilter] = useState<Filter>('All');
  const [includeDisabled, setIncludeDisabled] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [editing, setEditing] = useState<VoiceRecipe | null>(null);
  const [creating, setCreating] = useState<{ defaultKind: VoiceRecipeKind } | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [recipeList, providerList] = await Promise.all([
        voiceRecipeLibraryService.list({ includeDisabled }),
        speechProviderLibraryService.list({ includeDisabled: true }),
      ]);
      setRecipes(recipeList);
      setProviders(providerList);
    } catch (err) {
      // eslint-disable-next-line no-console
      console.error(err);
      setError('Failed to load recipe library.');
    } finally {
      setLoading(false);
    }
  }, [includeDisabled]);

  useEffect(() => {
    void load();
  }, [load]);

  const providerLookup = useMemo(() => {
    const m = new Map<string, SpeechProvider>();
    for (const p of providers) m.set(p.id, p);
    return m;
  }, [providers]);

  const visible = useMemo(() => {
    if (filter === 'All') return recipes;
    return recipes.filter((r) => r.kind === filter);
  }, [filter, recipes]);

  // Mock "active in voice mode" — Phase C will replace this with the real
  // VoiceModeSettings.recipeId. For now, pick the first built-in chained recipe so admins
  // can see the highlight design.
  const activeRecipeId = useMemo(() => {
    const builtinChained = recipes.find((r) => r.isBuiltIn && r.kind === 'Chained');
    return builtinChained?.id ?? null;
  }, [recipes]);

  const handleDisable = async (recipe: VoiceRecipe) => {
    try {
      await voiceRecipeLibraryService.setStatus(recipe.id, 'Disabled');
      toast.success(`${recipe.displayName} disabled.`);
      void load();
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        (err as { message?: string })?.message ??
        'Failed to disable recipe.';
      toast.error(message);
    }
  };

  const handleSaved = (saved: VoiceRecipe) => {
    setEditing(null);
    setCreating(null);
    void load();
    void saved;
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center p-12 text-[var(--color-text-secondary)]">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Loading recipes…
      </div>
    );
  }
  if (error) {
    return (
      <Card>
        <CardContent className="p-6 text-[var(--color-error)]">{error}</CardContent>
      </Card>
    );
  }

  // Editor renders inside a slide-out Sheet so the recipe list stays visible behind.
  const sheetOpen = editing !== null || creating !== null;
  const closeSheet = () => {
    setEditing(null);
    setCreating(null);
  };

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Speech & Voice"
        title="Recipes"
        subtitle="Reusable voice configurations. Voice Mode runs whichever recipe is currently active."
        actions={
          <Button
            size="sm"
            onClick={() => setCreating({ defaultKind: filter !== 'All' ? (filter as VoiceRecipeKind) : 'Chained' })}
          >
            <Plus className="h-3.5 w-3.5" /> New recipe
          </Button>
        }
      />

      {/* Clone-before-edit banner */}
      <div className="flex items-start gap-3 rounded-xl border border-[var(--color-brand-primary-20)] bg-[var(--color-brand-primary-10)] px-4 py-3.5">
        <span className="grid h-7 w-7 shrink-0 place-items-center rounded-md bg-[var(--color-brand-primary)]">
          <Bolt className="h-3.5 w-3.5 text-white" />
        </span>
        <div className="min-w-0 flex-1">
          <div className="text-[13px] font-semibold text-[var(--color-text-primary)]">
            Built-in recipes are read-only
          </div>
          <p className="mt-0.5 text-xs leading-relaxed text-[var(--color-text-secondary)]">
            To customise a built-in recipe, clone it first. Custom recipes are stored per workspace
            and can be edited at any time.
          </p>
        </div>
      </div>

      {/* Filter pills + show-disabled toggle */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap gap-1.5">
          {FILTERS.map((f) => {
            const count =
              f.id === 'All' ? recipes.length : recipes.filter((r) => r.kind === f.id).length;
            const active = filter === f.id;
            return (
              <button
                key={f.id}
                type="button"
                onClick={() => setFilter(f.id)}
                className={cn(
                  'inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs transition-colors',
                  active
                    ? 'border-[var(--color-brand-primary)] bg-[var(--color-brand-primary)] text-white'
                    : 'border-[var(--color-border-light)] bg-[var(--color-surface)] text-[var(--color-text-primary)] hover:border-[var(--color-brand-primary)]',
                )}
              >
                {f.label}
                <span
                  className={cn(
                    'font-mono text-[11px]',
                    active ? 'text-white/85' : 'text-[var(--color-text-tertiary)]',
                  )}
                >
                  {count}
                </span>
              </button>
            );
          })}
        </div>

        <div className="flex items-center gap-2">
          <Switch
            id="include-disabled-recipes"
            checked={includeDisabled}
            onCheckedChange={setIncludeDisabled}
          />
          <label
            htmlFor="include-disabled-recipes"
            className="text-xs text-[var(--color-text-secondary)]"
          >
            Show disabled
          </label>
        </div>
      </div>

      {/* Recipe list */}
      {visible.length === 0 ? (
        <EmptyState
          onAdd={() => setCreating({ defaultKind: filter !== 'All' ? (filter as VoiceRecipeKind) : 'Chained' })}
        />
      ) : (
        <div className="space-y-3">
          {visible.map((r) => (
            <RecipeCard
              key={r.id}
              recipe={r}
              providerLookup={providerLookup}
              activeInVoiceMode={r.id === activeRecipeId}
              onEdit={() => setEditing(r)}
              onDisable={() => void handleDisable(r)}
            />
          ))}
        </div>
      )}

      {/* Slide-out recipe stack editor */}
      <Sheet open={sheetOpen} onOpenChange={(open) => !open && closeSheet()}>
        <SheetContent size="lg" className="sm:max-w-none">
          {sheetOpen && (
            <RecipeStackEditor
              initial={editing}
              defaultKind={editing?.kind ?? creating?.defaultKind ?? 'Chained'}
              providers={providers}
              onSaved={handleSaved}
              onCancel={closeSheet}
            />
          )}
        </SheetContent>
      </Sheet>
    </div>
  );
}

function EmptyState({ onAdd }: { onAdd: () => void }) {
  return (
    <Card>
      <CardContent className="flex flex-col items-center gap-3 p-12 text-center">
        <p className="text-sm text-[var(--color-text-secondary)]">No recipes yet.</p>
        <Button onClick={onAdd}>
          <Plus className="h-4 w-4" />
          Add recipe
        </Button>
      </CardContent>
    </Card>
  );
}
