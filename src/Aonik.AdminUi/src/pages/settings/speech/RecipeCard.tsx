import { Copy, Edit3, TestTube2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { SpeechProvider } from '@/types/speechLibrary';
import type { VoiceRecipe } from '@/types/voiceRecipes';

import { Pill, RecipeFlow, buildRecipeSteps } from './_primitives';

interface RecipeCardProps {
  recipe: VoiceRecipe;
  /** Provider library used to resolve referenced ids → display names for the flow boxes. */
  providerLookup: Map<string, SpeechProvider>;
  /** Whether this recipe is the currently active voice-mode recipe. */
  activeInVoiceMode?: boolean;
  onEdit: () => void;
  onActivate?: () => void;
  onDisable: () => void;
}

/**
 * Full-width recipe card. Top section is a header row (name + status pills + actions);
 * bottom section is a visual flow renderer (Listen → Transcribe → Agent → Speak boxes
 * connected by arrows, or one box for composite). Active-in-voice-mode cards get a
 * brand-color border so admins can spot the live one at a glance.
 */
export function RecipeCard({
  recipe,
  providerLookup,
  activeInVoiceMode = false,
  onEdit,
  onActivate,
  onDisable,
}: RecipeCardProps) {
  const steps = buildRecipeSteps(recipe, providerLookup);
  const flowKind: 'chained' | 'composite' = recipe.kind === 'Composite' ? 'composite' : 'chained';

  return (
    <div
      className={cn(
        'rounded-xl bg-[var(--color-surface)] p-5 transition-colors',
        activeInVoiceMode
          ? 'border-2 border-[var(--color-brand-primary)]'
          : 'border border-[var(--color-border-light)]',
      )}
    >
      {/* Header row */}
      <div className="mb-4 flex items-start justify-between gap-4">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-[15px] font-semibold text-[var(--color-text-primary)]">
              {recipe.displayName}
            </span>
            {activeInVoiceMode && (
              <Pill tone="success" dot>
                Active in Voice Mode
              </Pill>
            )}
            {recipe.isBuiltIn ? (
              <Pill tone="default">Built-in</Pill>
            ) : (
              <Pill tone="warning">Custom</Pill>
            )}
            <Pill tone={recipe.kind === 'Composite' ? 'pending' : 'tint'}>
              {recipe.kind === 'Composite' ? 'Realtime' : 'Chained'}
            </Pill>
            {recipe.status === 'Disabled' && <Pill tone="default">Disabled</Pill>}
          </div>
          {recipe.description && (
            <p className="mt-1.5 text-[12.5px] leading-relaxed text-[var(--color-text-secondary)]">
              {recipe.description}
            </p>
          )}
        </div>

        <div className="flex shrink-0 items-center gap-2">
          <Button variant="ghost" size="sm" disabled>
            <TestTube2 className="h-3.5 w-3.5" /> Test
          </Button>
          {recipe.isBuiltIn ? (
            <Button variant="ghost" size="sm" onClick={onEdit}>
              <Copy className="h-3.5 w-3.5" /> Clone
            </Button>
          ) : (
            <Button variant="ghost" size="sm" onClick={onEdit}>
              <Edit3 className="h-3.5 w-3.5" /> Edit
            </Button>
          )}
          {!activeInVoiceMode && onActivate && (
            <Button size="sm" onClick={onActivate}>
              Activate
            </Button>
          )}
          {!recipe.isBuiltIn && recipe.status === 'Active' && (
            <Button variant="ghost" size="sm" onClick={onDisable}>
              Disable
            </Button>
          )}
        </div>
      </div>

      {/* Visual flow */}
      {steps.length > 0 ? (
        <RecipeFlow steps={steps} kind={flowKind} />
      ) : (
        <div className="rounded-[10px] bg-[var(--color-surface-inset)] p-4 text-xs text-[var(--color-text-tertiary)]">
          Recipe body is empty — open the editor to configure providers.
        </div>
      )}
    </div>
  );
}
