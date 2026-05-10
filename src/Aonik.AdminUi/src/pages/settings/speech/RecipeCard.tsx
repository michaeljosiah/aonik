import { Edit3, Layers, Plug, Trash2 } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import type { SpeechProvider } from '@/types/speechLibrary';
import type { VoiceRecipe } from '@/types/voiceRecipes';

interface RecipeCardProps {
  recipe: VoiceRecipe;
  /** Provider library used to resolve referenced ids → display names for the summary line. */
  providerLookup: Map<string, SpeechProvider>;
  onEdit: () => void;
  onDisable: () => void;
}

/**
 * One row in the Recipes tab. Renders the recipe's resolved provider chain so admins can read
 * "Whisper · Alloy" instead of opaque ids. Built-in recipes get "Clone" instead of Edit.
 */
export function RecipeCard({ recipe, providerLookup, onEdit, onDisable }: RecipeCardProps) {
  return (
    <Card className="transition-colors hover:border-primary/40">
      <CardContent className="flex items-center gap-4 p-4">
        <KindIcon kind={recipe.kind} />

        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <span className="font-medium truncate">{recipe.displayName}</span>
            <StatusBadge recipe={recipe} />
          </div>
          {recipe.description && (
            <div className="mt-0.5 truncate text-xs text-muted-foreground">
              {recipe.description}
            </div>
          )}
          <div className="mt-1 truncate text-xs text-muted-foreground">
            {summarise(recipe, providerLookup)}
          </div>
        </div>

        <div className="flex shrink-0 items-center gap-2">
          <Button variant="ghost" size="sm" onClick={onEdit}>
            <Edit3 className="h-4 w-4" />
            {recipe.isBuiltIn ? 'Clone' : 'Edit'}
          </Button>
          {!recipe.isBuiltIn && recipe.status === 'Active' && (
            <Button variant="ghost" size="sm" onClick={onDisable}>
              <Trash2 className="h-4 w-4" />
              Disable
            </Button>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

function KindIcon({ kind }: { kind: VoiceRecipe['kind'] }) {
  const palette: Record<VoiceRecipe['kind'], string> = {
    Chained: 'bg-amber-100 text-amber-700 dark:bg-amber-950/30 dark:text-amber-300',
    Composite: 'bg-purple-100 text-purple-700 dark:bg-purple-950/30 dark:text-purple-300',
  };
  const Icon = kind === 'Chained' ? Layers : Plug;
  return (
    <div
      className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-md ${palette[kind]}`}
    >
      <Icon className="h-4 w-4" />
    </div>
  );
}

function StatusBadge({ recipe }: { recipe: VoiceRecipe }) {
  if (recipe.isBuiltIn) {
    return <Badge variant="outline">Built-in</Badge>;
  }
  if (recipe.status === 'Disabled') {
    return <Badge variant="secondary">Disabled</Badge>;
  }
  if (recipe.status === 'SoftDeleted') {
    return <Badge variant="error">Deleted</Badge>;
  }
  return null;
}

function summarise(recipe: VoiceRecipe, lookup: Map<string, SpeechProvider>): string {
  if (recipe.kind === 'Chained' && recipe.chained) {
    const stt = lookup.get(recipe.chained.sttProviderId)?.displayName ?? recipe.chained.sttProviderId;
    const tts = lookup.get(recipe.chained.ttsProviderId)?.displayName ?? recipe.chained.ttsProviderId;
    const vad = recipe.chained.vad;
    return `${stt} → ${tts} · VAD ${vad}${recipe.chained.vadStopMs ? ` (${recipe.chained.vadStopMs}ms)` : ''}`;
  }
  if (recipe.kind === 'Composite' && recipe.composite) {
    const comp = lookup.get(recipe.composite.compositeProviderId)?.displayName
      ?? recipe.composite.compositeProviderId;
    return `composite · ${comp}`;
  }
  return '';
}
