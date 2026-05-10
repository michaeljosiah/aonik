import { useCallback, useEffect, useMemo, useState } from 'react';
import { Loader2, Plus } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Switch } from '@/components/ui/switch';
import { speechProviderLibraryService } from '@/services/speechProviderLibraryService';
import { voiceRecipeLibraryService } from '@/services/voiceRecipeLibraryService';
import type { SpeechProvider } from '@/types/speechLibrary';
import type { VoiceRecipe, VoiceRecipeKind } from '@/types/voiceRecipes';

import { RecipeCard } from './RecipeCard';
import { RecipeStackEditor } from './RecipeStackEditor';

type Filter = 'All' | VoiceRecipeKind;
const FILTERS: Filter[] = ['All', 'Chained', 'Composite'];
const FILTER_LABEL: Record<Filter, string> = {
  All: 'All',
  Chained: 'Chained',
  Composite: 'Composite',
};

/**
 * Recipes tab. Lists every recipe in the library and opens the stack editor for create /
 * edit / clone. Uses the provider library to resolve referenced ids → display names so the
 * cards read "Whisper → Alloy" instead of opaque ids.
 */
export function RecipesTab() {
  const [recipes, setRecipes] = useState<VoiceRecipe[]>([]);
  const [providers, setProviders] = useState<SpeechProvider[]>([]);
  const [filter, setFilter] = useState<Filter>('All');
  const [includeDisabled, setIncludeDisabled] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Editor state.
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
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Loading recipes…
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

  if (editing || creating) {
    return (
      <RecipeStackEditor
        initial={editing}
        defaultKind={editing?.kind ?? creating?.defaultKind ?? 'Chained'}
        providers={providers}
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
                  {recipes.filter((r) => r.kind === f).length}
                </Badge>
              )}
            </Button>
          ))}
        </div>

        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2">
            <Switch
              id="include-disabled-recipes"
              checked={includeDisabled}
              onCheckedChange={setIncludeDisabled}
            />
            <label htmlFor="include-disabled-recipes" className="text-xs text-muted-foreground">
              Show disabled
            </label>
          </div>
          <Button
            onClick={() => setCreating({ defaultKind: filter !== 'All' ? filter : 'Chained' })}
            size="sm"
          >
            <Plus className="h-4 w-4" />
            Add recipe
          </Button>
        </div>
      </div>

      {visible.length === 0 ? (
        <EmptyState onAdd={() => setCreating({ defaultKind: filter !== 'All' ? filter : 'Chained' })} />
      ) : (
        <div className="space-y-2">
          {visible.map((r) => (
            <RecipeCard
              key={r.id}
              recipe={r}
              providerLookup={providerLookup}
              onEdit={() => setEditing(r)}
              onDisable={() => void handleDisable(r)}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function EmptyState({ onAdd }: { onAdd: () => void }) {
  return (
    <Card>
      <CardContent className="flex flex-col items-center gap-3 p-12 text-center">
        <p className="text-sm text-muted-foreground">No recipes yet.</p>
        <Button onClick={onAdd}>
          <Plus className="h-4 w-4" />
          Add recipe
        </Button>
      </CardContent>
    </Card>
  );
}
