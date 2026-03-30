import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Loader2, Sparkles, ArrowRight } from 'lucide-react';
import { ContentSuggestionCard } from './ContentSuggestionCard';
import type { ContentSuggestion, SuggestionStatus } from '@/types/contentWizard';

interface WizardStepGenerateProps {
  suggestions: ContentSuggestion[];
  isGenerating: boolean;
  onStatusChange: (id: string, status: SuggestionStatus) => void;
  onUpdate: (id: string, updates: Partial<ContentSuggestion>) => void;
  onProceedToReview: () => void;
}

export function WizardStepGenerate({
  suggestions,
  isGenerating,
  onStatusChange,
  onUpdate,
  onProceedToReview,
}: WizardStepGenerateProps) {
  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-3">
          <div className="flex items-center justify-between">
            <CardTitle className="flex items-center gap-2">
              <Sparkles className="w-5 h-5 text-[var(--color-brand-primary)]" />
              AI Suggestions
              {suggestions.length > 0 && (
                <span className="text-sm font-normal text-[var(--color-text-secondary)]">
                  ({suggestions.length} generated)
                </span>
              )}
            </CardTitle>
            {!isGenerating && suggestions.length > 0 && (
              <Button size="sm" onClick={onProceedToReview} className="gap-1.5">
                Review All
                <ArrowRight className="w-4 h-4" />
              </Button>
            )}
          </div>
        </CardHeader>
        <CardContent>
          {isGenerating && suggestions.length === 0 && (
            <div className="flex flex-col items-center justify-center py-12 gap-3">
              <Loader2 className="w-8 h-8 animate-spin text-[var(--color-brand-primary)]" />
              <p className="text-sm text-[var(--color-text-secondary)]">
                AI is generating content suggestions...
              </p>
              <p className="text-xs text-[var(--color-text-tertiary)]">
                Suggestions will appear here as they are generated. You can refine them using the chat.
              </p>
            </div>
          )}

          {suggestions.length > 0 && (
            <div className="space-y-3">
              {suggestions.map((suggestion, index) => (
                <ContentSuggestionCard
                  key={suggestion.id}
                  suggestion={suggestion}
                  index={index}
                  onStatusChange={onStatusChange}
                  onUpdate={onUpdate}
                />
              ))}
              {isGenerating && (
                <div className="flex items-center gap-2 py-3 justify-center text-sm text-[var(--color-text-secondary)]">
                  <Loader2 className="w-4 h-4 animate-spin" />
                  Generating more...
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
