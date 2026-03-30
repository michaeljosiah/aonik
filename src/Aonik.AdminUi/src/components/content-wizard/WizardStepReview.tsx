import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Check, X, ArrowRight, ArrowLeft } from 'lucide-react';
import { ContentSuggestionCard } from './ContentSuggestionCard';
import type { ContentSuggestion, SuggestionStatus } from '@/types/contentWizard';

interface WizardStepReviewProps {
  suggestions: ContentSuggestion[];
  onStatusChange: (id: string, status: SuggestionStatus) => void;
  onUpdate: (id: string, updates: Partial<ContentSuggestion>) => void;
  onBack: () => void;
  onProceedToSave: () => void;
}

export function WizardStepReview({
  suggestions,
  onStatusChange,
  onUpdate,
  onBack,
  onProceedToSave,
}: WizardStepReviewProps) {
  const approved = suggestions.filter((s) => s.status === 'approved');
  const pending = suggestions.filter((s) => s.status === 'pending');
  const rejected = suggestions.filter((s) => s.status === 'rejected');

  function approveAll() {
    suggestions.forEach((s) => {
      if (s.status === 'pending') onStatusChange(s.id, 'approved');
    });
  }

  function rejectAll() {
    suggestions.forEach((s) => {
      if (s.status === 'pending') onStatusChange(s.id, 'rejected');
    });
  }

  return (
    <div className="space-y-4">
      {/* Summary bar */}
      <Card>
        <CardContent className="py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-4 text-sm">
              <span className="text-[var(--color-text-secondary)]">
                {suggestions.length} total
              </span>
              <span className="text-[var(--color-success)] font-medium">
                {approved.length} approved
              </span>
              <span className="text-[var(--color-text-tertiary)]">
                {pending.length} pending
              </span>
              <span className="text-[var(--color-danger)]">
                {rejected.length} rejected
              </span>
            </div>
            <div className="flex items-center gap-2">
              {pending.length > 0 && (
                <>
                  <Button variant="outline" size="sm" onClick={approveAll} className="gap-1.5 text-[var(--color-success)]">
                    <Check className="w-3.5 h-3.5" />
                    Approve All
                  </Button>
                  <Button variant="outline" size="sm" onClick={rejectAll} className="gap-1.5 text-[var(--color-danger)]">
                    <X className="w-3.5 h-3.5" />
                    Reject All
                  </Button>
                </>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Suggestion cards */}
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
      </div>

      {/* Navigation */}
      <div className="flex justify-between pt-2">
        <Button variant="outline" onClick={onBack} className="gap-1.5">
          <ArrowLeft className="w-4 h-4" />
          Back to Generate
        </Button>
        <Button
          onClick={onProceedToSave}
          disabled={approved.length === 0}
          className="gap-1.5"
        >
          Save {approved.length} Approved
          <ArrowRight className="w-4 h-4" />
        </Button>
      </div>
    </div>
  );
}
