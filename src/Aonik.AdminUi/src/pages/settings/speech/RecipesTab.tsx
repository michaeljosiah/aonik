import { useCallback, useEffect, useMemo, useState } from "react";
import { Layers, Loader2, Plug, Plus, Radio } from "lucide-react";
import { Link as RouterLink } from "react-router-dom";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Sheet, SheetContent } from "@/components/ui/sheet";
import { Switch } from "@/components/ui/switch";
import { cn } from "@/lib/utils";
import { voiceModeSettingsService } from "@/services/speechActiveSettingsService";
import { speechProviderLibraryService } from "@/services/speechProviderLibraryService";
import { voiceRecipeLibraryService } from "@/services/voiceRecipeLibraryService";
import type { SpeechProvider } from "@/types/speechLibrary";
import type { VoiceRecipe, VoiceRecipeKind } from "@/types/voiceRecipes";

import { PageHeader } from "./_primitives";
import { RecipeCard } from "./RecipeCard";
import { RecipeStackEditor } from "./RecipeStackEditor";
import { RecipeTestPanel } from "./RecipeTestPanel";

type Filter = "All" | VoiceRecipeKind;
const FILTERS: { id: Filter; label: string }[] = [
  { id: "All", label: "All recipes" },
  { id: "Chained", label: "Chained" },
  { id: "Composite", label: "Realtime" },
];

interface RecipesTabProps {
  /** Bumped by the parent shell when settings change elsewhere — triggers a reload. */
  settingsTick?: number;
  /** Notify the parent shell that the persisted settings changed. */
  onSettingsChanged?: () => void;
}

/**
 * Recipes tab. Banner explains the create-your-own model, filter pills slice by kind, then a
 * vertical list of full-width recipe cards rendering the visual flow. The active-in-voice-mode
 * highlight + the per-card Activate button now read/write the real <c>VoiceModeSettings</c>
 * row (spec 024 Phase C.1).
 */
export function RecipesTab({
  settingsTick,
  onSettingsChanged,
}: RecipesTabProps = {}) {
  const [recipes, setRecipes] = useState<VoiceRecipe[]>([]);
  const [providers, setProviders] = useState<SpeechProvider[]>([]);
  const [activeRecipeId, setActiveRecipeId] = useState<string | null>(null);
  const [filter, setFilter] = useState<Filter>("All");
  const [includeDisabled, setIncludeDisabled] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [editing, setEditing] = useState<VoiceRecipe | null>(null);
  const [creating, setCreating] = useState<{
    defaultKind: VoiceRecipeKind;
  } | null>(null);
  // Phase E: per-recipe Test sheet. Mutually exclusive with edit / create — rendering both
  // simultaneously would stack two sheets which the Sheet primitive doesn't support.
  const [testing, setTesting] = useState<VoiceRecipe | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [recipeList, providerList, voiceMode] = await Promise.all([
        voiceRecipeLibraryService.list({ includeDisabled }),
        speechProviderLibraryService.list({ includeDisabled: true }),
        voiceModeSettingsService.get(),
      ]);
      setRecipes(recipeList);
      setProviders(providerList);
      setActiveRecipeId(voiceMode.activeRecipeId);
    } catch (err) {
      // eslint-disable-next-line no-console
      console.error(err);
      setError("Failed to load recipe library.");
    } finally {
      setLoading(false);
    }
  }, [includeDisabled]);

  useEffect(() => {
    void load();
  }, [load, settingsTick]);

  const providerLookup = useMemo(() => {
    const m = new Map<string, SpeechProvider>();
    for (const p of providers) m.set(p.id, p);
    return m;
  }, [providers]);

  const visible = useMemo(() => {
    if (filter === "All") return recipes;
    return recipes.filter((r) => r.kind === filter);
  }, [filter, recipes]);

  const handleActivate = async (recipe: VoiceRecipe) => {
    try {
      const saved = await voiceModeSettingsService.update({
        activeRecipeId: recipe.id,
        // Activating from the recipe list implies "I want this on" — flip enabled true.
        enabled: true,
      });
      setActiveRecipeId(saved.activeRecipeId);
      onSettingsChanged?.();
      toast.success(`${recipe.displayName} is now active in Voice Mode.`);
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data
          ?.error ??
        (err as { message?: string })?.message ??
        "Failed to activate recipe.";
      toast.error(message);
    }
  };

  const handleDisable = async (recipe: VoiceRecipe) => {
    try {
      await voiceRecipeLibraryService.setStatus(recipe.id, "Disabled");
      toast.success(`${recipe.displayName} disabled.`);
      void load();
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data
          ?.error ??
        (err as { message?: string })?.message ??
        "Failed to disable recipe.";
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
        <CardContent className="p-6 text-[var(--color-error)]">
          {error}
        </CardContent>
      </Card>
    );
  }

  // Editor renders inside a slide-out Sheet so the recipe list stays visible behind.
  const sheetOpen = editing !== null || creating !== null;
  const closeSheet = () => {
    setEditing(null);
    setCreating(null);
  };
  // Test sheet uses its own state slot so a Test sheet doesn't collide with an editor sheet.
  const testSheetOpen = testing !== null;
  const closeTestSheet = () => setTesting(null);

  const tenantHasNoProviders = providers.length === 0;
  const tenantHasNoRecipes = recipes.length === 0;

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Speech & Voice"
        title="Recipes"
        subtitle="Reusable voice configurations. Voice Mode runs whichever recipe is currently active."
        actions={
          <Button
            size="sm"
            disabled={tenantHasNoProviders}
            onClick={() =>
              setCreating({
                defaultKind:
                  filter !== "All" ? (filter as VoiceRecipeKind) : "Chained",
              })
            }
          >
            <Plus className="h-3.5 w-3.5" /> New recipe
          </Button>
        }
      />

      {tenantHasNoRecipes ? (
        <FirstRecipeHero
          tenantHasNoProviders={tenantHasNoProviders}
          onCreate={(k) => setCreating({ defaultKind: k })}
        />
      ) : (
        <>
          {/* Filter pills + show-disabled toggle */}
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="flex flex-wrap gap-1.5">
              {FILTERS.map((f) => {
                const count =
                  f.id === "All"
                    ? recipes.length
                    : recipes.filter((r) => r.kind === f.id).length;
                const active = filter === f.id;
                return (
                  <button
                    key={f.id}
                    type="button"
                    onClick={() => setFilter(f.id)}
                    className={cn(
                      "inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs transition-colors",
                      active
                        ? "border-[var(--color-brand-primary)] bg-[var(--color-brand-primary)] text-white"
                        : "border-[var(--color-border-light)] bg-[var(--color-surface)] text-[var(--color-text-primary)] hover:border-[var(--color-brand-primary)]",
                    )}
                  >
                    {f.label}
                    <span
                      className={cn(
                        "font-mono text-[11px]",
                        active
                          ? "text-white/85"
                          : "text-[var(--color-text-tertiary)]",
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
            <FilterEmptyState
              onAdd={() =>
                setCreating({
                  defaultKind:
                    filter !== "All" ? (filter as VoiceRecipeKind) : "Chained",
                })
              }
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
                  onTest={() => setTesting(r)}
                  onActivate={() => void handleActivate(r)}
                  onDisable={() => void handleDisable(r)}
                />
              ))}
            </div>
          )}
        </>
      )}

      {/* Slide-out recipe stack editor */}
      <Sheet open={sheetOpen} onOpenChange={(open) => !open && closeSheet()}>
        <SheetContent size="lg" className="sm:max-w-none">
          {sheetOpen && (
            <RecipeStackEditor
              initial={editing}
              defaultKind={editing?.kind ?? creating?.defaultKind ?? "Chained"}
              providers={providers}
              onSaved={handleSaved}
              onCancel={closeSheet}
            />
          )}
        </SheetContent>
      </Sheet>

      {/* Per-recipe Test sheet (Phase E). Renders TTS + STT cards pre-bound to the recipe's
          provider + voice + model picks so admins can sanity-check credentials without
          repeating the inline picker affordances. */}
      <Sheet
        open={testSheetOpen}
        onOpenChange={(open) => !open && closeTestSheet()}
      >
        <SheetContent size="md" className="sm:max-w-none">
          {testing && (
            <RecipeTestPanel
              recipe={testing}
              providers={providers}
              onClose={closeTestSheet}
            />
          )}
        </SheetContent>
      </Sheet>
    </div>
  );
}

/**
 * Hero shown when the workspace has no recipes yet. If providers also haven't been added,
 * the CTA points back to the Providers tab — recipes can't reference anything until at
 * least one provider exists.
 */
function FirstRecipeHero({
  tenantHasNoProviders,
  onCreate,
}: {
  tenantHasNoProviders: boolean;
  onCreate: (kind: VoiceRecipeKind) => void;
}) {
  if (tenantHasNoProviders) {
    return (
      <div className="rounded-2xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-12 text-center">
        <div className="mx-auto mb-4 grid h-14 w-14 place-items-center rounded-2xl bg-[var(--color-brand-primary-10)]">
          <Plug className="h-6 w-6 text-[var(--color-brand-primary)]" />
        </div>
        <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">
          Add a provider before composing recipes
        </h2>
        <p className="mx-auto mt-1 max-w-[28rem] text-sm text-[var(--color-text-secondary)]">
          Recipes are pipelines that wire providers together. Add at least one
          Speech-to-Text and one Text-to-Speech provider, then come back to
          compose them.
        </p>
        <Button asChild className="mt-6" size="sm">
          <RouterLink to="/settings/speech?tab=providers">
            Open Providers
          </RouterLink>
        </Button>
      </div>
    );
  }

  return (
    <div className="rounded-2xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-12 text-center">
      <div className="mx-auto mb-4 grid h-14 w-14 place-items-center rounded-2xl bg-[var(--color-brand-primary-10)]">
        <Layers className="h-6 w-6 text-[var(--color-brand-primary)]" />
      </div>
      <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">
        Compose your first voice recipe
      </h2>
      <p className="mx-auto mt-1 max-w-[28rem] text-sm text-[var(--color-text-secondary)]">
        A recipe wires your providers into a pipeline that powers Voice Mode.
        Pick a chained STT → Agent → TTS flow, or a single-vendor realtime
        composite.
      </p>

      <div className="mx-auto mt-6 grid max-w-[36rem] grid-cols-1 gap-3 sm:grid-cols-2">
        <RecipeKindChoice
          icon={<Layers className="h-5 w-5" />}
          title="Chained recipe"
          description="STT → Agent → TTS. Mix and match vendors."
          onClick={() => onCreate("Chained")}
        />
        <RecipeKindChoice
          icon={<Radio className="h-5 w-5" />}
          title="Realtime composite"
          description="Single-vendor end-to-end (Voice Live, Realtime API)."
          onClick={() => onCreate("Composite")}
        />
      </div>
    </div>
  );
}

function RecipeKindChoice({
  icon,
  title,
  description,
  onClick,
}: {
  icon: React.ReactNode;
  title: string;
  description: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="group flex flex-col items-center gap-2 rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-5 text-center transition-colors hover:border-[var(--color-brand-primary)] hover:bg-[var(--color-brand-primary-10)]"
    >
      <span className="grid h-10 w-10 place-items-center rounded-lg bg-[var(--color-surface)] text-[var(--color-brand-primary)] transition-colors group-hover:bg-[var(--color-brand-primary)] group-hover:text-white">
        {icon}
      </span>
      <span className="text-sm font-semibold text-[var(--color-text-primary)]">
        {title}
      </span>
      <span className="text-[11.5px] text-[var(--color-text-tertiary)]">
        {description}
      </span>
    </button>
  );
}

/** Shown when filtering produces zero rows but other recipes exist. */
function FilterEmptyState({ onAdd }: { onAdd: () => void }) {
  return (
    <Card>
      <CardContent className="flex flex-col items-center gap-3 p-12 text-center">
        <p className="text-sm text-[var(--color-text-secondary)]">
          No recipes in this category yet.
        </p>
        <Button onClick={onAdd}>
          <Plus className="h-4 w-4" />
          New recipe
        </Button>
      </CardContent>
    </Card>
  );
}
