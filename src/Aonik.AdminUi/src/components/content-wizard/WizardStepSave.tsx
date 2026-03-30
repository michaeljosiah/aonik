import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Loader2, CheckCircle, AlertCircle, ArrowLeft, ExternalLink, RotateCcw, ImageIcon } from 'lucide-react';
import { createContentBlock, generateContentImage } from '@/services/contentBlockService';
import type { ContentSuggestion, WizardConfig } from '@/types/contentWizard';

interface SaveResult {
  suggestion: ContentSuggestion;
  success: boolean;
  error?: string;
  createdId?: string;
  imageStatus?: 'pending' | 'generating' | 'done' | 'failed';
  imageError?: string;
}

interface WizardStepSaveProps {
  suggestions: ContentSuggestion[];
  config: WizardConfig;
  onSavedCountChange: (count: number) => void;
  onBack: () => void;
  onReset: () => void;
}

export function WizardStepSave({
  suggestions,
  config,
  onSavedCountChange,
  onBack,
  onReset,
}: WizardStepSaveProps) {
  const navigate = useNavigate();
  const [saving, setSaving] = useState(false);
  const [results, setResults] = useState<SaveResult[]>([]);
  const [done, setDone] = useState(false);

  // Generate a stable UUID for AI traceability on this wizard session
  const aiRunId = useMemo(() => crypto.randomUUID(), []);

  useEffect(() => {
    if (suggestions.length === 0 || done || saving) return;
    saveAll();
  }, [suggestions]);

  async function saveAll() {
    setSaving(true);
    const saveResults: SaveResult[] = [];
    let successCount = 0;

    for (const suggestion of suggestions) {
      const needsImage = config.includeImages && !!suggestion.imagePrompt;

      try {
        const created = await createContentBlock({
          contentKey: suggestion.contentKey,
          title: suggestion.title,
          slug: suggestion.slug,
          area: suggestion.area,
          format: suggestion.format,
          body: suggestion.body,
          locale: suggestion.locale || config.locale,
          isEnabled: true,
          priority: suggestion.priority || 100,
          aiRunId,
        });
        saveResults.push({
          suggestion,
          success: true,
          createdId: created.id,
          imageStatus: needsImage ? 'pending' : undefined,
        });
        successCount++;
      } catch (err: unknown) {
        const axiosErr = err as { userMessage?: string; message?: string };
        const message = axiosErr.userMessage || axiosErr.message || 'Unknown error';
        saveResults.push({ suggestion, success: false, error: message });
      }
      setResults([...saveResults]);
      onSavedCountChange(successCount);
    }

    // Generate images for successful saves that have imagePrompts
    for (let i = 0; i < saveResults.length; i++) {
      const result = saveResults[i];
      if (!result.success || !result.createdId || result.imageStatus !== 'pending') continue;

      saveResults[i] = { ...result, imageStatus: 'generating' };
      setResults([...saveResults]);

      try {
        await generateContentImage(result.createdId, {
          prompt: result.suggestion.imagePrompt!,
          alt: result.suggestion.title,
          width: config.imageDimensions.width,
          height: config.imageDimensions.height,
        });
        saveResults[i] = { ...saveResults[i], imageStatus: 'done' };
      } catch (err: unknown) {
        const axiosErr = err as { userMessage?: string; message?: string };
        saveResults[i] = {
          ...saveResults[i],
          imageStatus: 'failed',
          imageError: axiosErr.userMessage || axiosErr.message || 'Image generation failed',
        };
      }
      setResults([...saveResults]);
    }

    setSaving(false);
    setDone(true);
  }

  const successResults = results.filter((r) => r.success);
  const failedResults = results.filter((r) => !r.success);

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            {saving ? (
              <>
                <Loader2 className="w-5 h-5 animate-spin text-[var(--color-brand-primary)]" />
                Saving Content Blocks...
              </>
            ) : done ? (
              <>
                <CheckCircle className="w-5 h-5 text-[var(--color-success)]" />
                Save Complete
              </>
            ) : (
              'Preparing to Save...'
            )}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {/* Progress */}
          {(saving || done) && (
            <div className="text-sm text-[var(--color-text-secondary)]">
              {saving
                ? `Saving ${results.length} of ${suggestions.length}...`
                : `${successResults.length} of ${suggestions.length} saved successfully.`}
              {failedResults.length > 0 && (
                <span className="text-[var(--color-danger)] ml-2">
                  {failedResults.length} failed.
                </span>
              )}
            </div>
          )}

          {/* Results list */}
          <div className="space-y-2">
            {results.map((result) => (
              <div
                key={result.suggestion.id}
                className={`py-2 px-3 rounded border ${
                  result.success
                    ? 'border-[var(--color-success)]/30 bg-[var(--color-success)]/5'
                    : 'border-[var(--color-danger)]/30 bg-[var(--color-danger)]/5'
                }`}
              >
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2 min-w-0">
                    {result.success ? (
                      <CheckCircle className="w-4 h-4 text-[var(--color-success)] shrink-0" />
                    ) : (
                      <AlertCircle className="w-4 h-4 text-[var(--color-danger)] shrink-0" />
                    )}
                    <span className="text-sm truncate">{result.suggestion.title}</span>
                  </div>
                  {result.success && result.createdId && (
                    <Button
                      variant="ghost"
                      size="sm"
                      className="gap-1 text-xs shrink-0"
                      onClick={() => navigate(`/cms/content-blocks/${result.createdId}`)}
                    >
                      <ExternalLink className="w-3 h-3" />
                      Edit
                    </Button>
                  )}
                  {!result.success && (
                    <span className="text-xs text-[var(--color-danger)] shrink-0">
                      {result.error}
                    </span>
                  )}
                </div>
                {/* Image generation status */}
                {result.imageStatus && (
                  <div className="flex items-center gap-1.5 mt-1 ml-6 text-xs">
                    {result.imageStatus === 'generating' && (
                      <>
                        <Loader2 className="w-3 h-3 animate-spin text-[var(--color-brand-primary)]" />
                        <span className="text-[var(--color-text-secondary)]">Generating image...</span>
                      </>
                    )}
                    {result.imageStatus === 'done' && (
                      <>
                        <ImageIcon className="w-3 h-3 text-[var(--color-success)]" />
                        <span className="text-[var(--color-success)]">Image generated</span>
                      </>
                    )}
                    {result.imageStatus === 'failed' && (
                      <>
                        <AlertCircle className="w-3 h-3 text-[var(--color-warning)]" />
                        <span className="text-[var(--color-warning)]">
                          Image failed: {result.imageError}
                        </span>
                      </>
                    )}
                    {result.imageStatus === 'pending' && (
                      <>
                        <ImageIcon className="w-3 h-3 text-[var(--color-text-tertiary)]" />
                        <span className="text-[var(--color-text-tertiary)]">Image queued</span>
                      </>
                    )}
                  </div>
                )}
              </div>
            ))}

            {/* Placeholders for items not yet saved */}
            {saving &&
              suggestions.slice(results.length).map((s) => (
                <div
                  key={s.id}
                  className="flex items-center gap-2 py-2 px-3 rounded border border-[var(--color-border)] opacity-50"
                >
                  <Loader2 className="w-4 h-4 animate-spin text-[var(--color-text-tertiary)] shrink-0" />
                  <span className="text-sm truncate">{s.title}</span>
                </div>
              ))}
          </div>
        </CardContent>
      </Card>

      {/* Actions */}
      {done && (
        <div className="flex justify-between pt-2">
          <Button variant="outline" onClick={() => navigate('/cms/content-blocks')} className="gap-1.5">
            <ExternalLink className="w-4 h-4" />
            View All Content Blocks
          </Button>
          <Button onClick={onReset} className="gap-1.5">
            <RotateCcw className="w-4 h-4" />
            Generate More
          </Button>
        </div>
      )}

      {!done && !saving && (
        <div className="flex justify-start pt-2">
          <Button variant="outline" onClick={onBack} className="gap-1.5">
            <ArrowLeft className="w-4 h-4" />
            Back to Review
          </Button>
        </div>
      )}
    </div>
  );
}
