import { useNavigate } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useAguiChat } from '@/hooks/useAguiChat';
import { useContentWizard } from '@/hooks/useContentWizard';
import { WizardStepConfigure } from '@/components/content-wizard/WizardStepConfigure';
import { WizardStepGenerate } from '@/components/content-wizard/WizardStepGenerate';
import { WizardStepReview } from '@/components/content-wizard/WizardStepReview';
import { WizardStepSave } from '@/components/content-wizard/WizardStepSave';
import { WizardChatPanel } from '@/components/content-wizard/WizardChatPanel';
import { WIZARD_STEPS } from '@/types/contentWizard';

export function ContentWizardPage() {
  const navigate = useNavigate();
  const chat = useAguiChat();
  const wizard = useContentWizard(chat);

  const stepIndex = WIZARD_STEPS.findIndex((s) => s.key === wizard.step);

  return (
    <div className="flex h-full overflow-hidden">
      {/* Left panel — Wizard */}
      <div className="flex-1 flex flex-col min-w-0 overflow-hidden">
        {/* Header */}
        <div className="px-6 py-4 border-b border-[var(--color-border-light)] bg-[var(--color-surface)] shrink-0">
          <div className="flex items-center justify-between mb-3">
            <div>
              <h1 className="text-xl font-bold text-[var(--color-text-primary)]">
                AI Content Wizard
              </h1>
              <p className="text-sm text-[var(--color-text-secondary)]">
                Generate content blocks with AI assistance
              </p>
            </div>
            <Button variant="outline" size="sm" onClick={() => navigate('/cms/content-blocks')}>
              <ArrowLeft className="w-4 h-4 mr-1.5" />
              Back to Content
            </Button>
          </div>

          {/* Step indicator */}
          <div className="flex items-center gap-1">
            {WIZARD_STEPS.map((s, i) => (
              <div key={s.key} className="flex items-center">
                {i > 0 && (
                  <div
                    className={`w-8 h-px mx-1 ${
                      i <= stepIndex
                        ? 'bg-[var(--color-brand-primary)]'
                        : 'bg-[var(--color-border)]'
                    }`}
                  />
                )}
                <button
                  onClick={() => {
                    // Only allow going to completed or current steps
                    if (i <= stepIndex) wizard.setStep(s.key);
                  }}
                  disabled={i > stepIndex}
                  className={`flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-medium transition-colors ${
                    i === stepIndex
                      ? 'bg-[var(--color-brand-primary)] text-white'
                      : i < stepIndex
                        ? 'bg-[var(--color-brand-primary)]/15 text-[var(--color-brand-primary)] hover:bg-[var(--color-brand-primary)]/25'
                        : 'bg-[var(--color-surface-inset)] text-[var(--color-text-tertiary)]'
                  }`}
                >
                  <span
                    className={`w-5 h-5 rounded-full flex items-center justify-center text-[10px] font-bold ${
                      i === stepIndex
                        ? 'bg-white/25'
                        : i < stepIndex
                          ? 'bg-[var(--color-brand-primary)]/20'
                          : 'bg-[var(--color-border)]'
                    }`}
                  >
                    {i + 1}
                  </span>
                  {s.label}
                </button>
              </div>
            ))}
          </div>
        </div>

        {/* Step content */}
        <div className="flex-1 overflow-auto p-6">
          {wizard.step === 'configure' && (
            <WizardStepConfigure
              config={wizard.config}
              onConfigChange={wizard.setConfig}
              onGenerate={wizard.startGeneration}
            />
          )}
          {wizard.step === 'generate' && (
            <WizardStepGenerate
              suggestions={wizard.suggestions}
              isGenerating={wizard.isGenerating}
              onStatusChange={wizard.updateSuggestionStatus}
              onUpdate={wizard.updateSuggestion}
              onProceedToReview={() => wizard.setStep('review')}
            />
          )}
          {wizard.step === 'review' && (
            <WizardStepReview
              suggestions={wizard.suggestions}
              onStatusChange={wizard.updateSuggestionStatus}
              onUpdate={wizard.updateSuggestion}
              onBack={() => wizard.setStep('generate')}
              onProceedToSave={() => wizard.setStep('save')}
            />
          )}
          {wizard.step === 'save' && (
            <WizardStepSave
              suggestions={wizard.approvedSuggestions}
              config={wizard.config}
              onSavedCountChange={wizard.setSavedCount}
              onBack={() => wizard.setStep('review')}
              onReset={wizard.resetWizard}
            />
          )}
        </div>
      </div>

      {/* Right panel — Chat (fixed width) */}
      <div className="w-[400px] shrink-0 h-full">
        <WizardChatPanel chat={chat} />
      </div>
    </div>
  );
}
