import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Sparkles, ImageIcon } from 'lucide-react';
import type { WizardConfig } from '@/types/contentWizard';
import { AREA_OPTIONS, FORMAT_OPTIONS, TONE_OPTIONS, IMAGE_DIMENSION_PRESETS, AREA_IMAGE_DEFAULTS } from '@/types/contentWizard';

interface WizardStepConfigureProps {
  config: WizardConfig;
  onConfigChange: (config: WizardConfig) => void;
  onGenerate: () => void;
}

export function WizardStepConfigure({ config, onConfigChange, onGenerate }: WizardStepConfigureProps) {
  const update = (partial: Partial<WizardConfig>) =>
    onConfigChange({ ...config, ...partial });

  const canGenerate = config.topic.trim().length > 0;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Sparkles className="w-5 h-5 text-[var(--color-brand-primary)]" />
          Content Generation Settings
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-5">
        {/* Topic */}
        <div className="space-y-2">
          <Label htmlFor="wizard-topic">Topic / Description *</Label>
          <Textarea
            id="wizard-topic"
            value={config.topic}
            onChange={(e) => update({ topic: e.target.value })}
            placeholder="e.g., Budget planning tips for young professionals, saving strategies for beginners..."
            rows={3}
          />
          <p className="text-xs text-[var(--color-text-secondary)]">
            Describe what kind of content you want the AI to generate. Be specific for better results.
          </p>
        </div>

        <div className="grid gap-4 md:grid-cols-2">
          {/* Area */}
          <div className="space-y-2">
            <Label htmlFor="wizard-area">Content Area</Label>
            <Select value={config.area} onValueChange={(v) => update({ area: v, imageDimensions: AREA_IMAGE_DEFAULTS[v] ?? AREA_IMAGE_DEFAULTS['General'] })}>
              <SelectTrigger id="wizard-area">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {AREA_OPTIONS.map((option) => (
                  <SelectItem key={option} value={option}>
                    {option}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Format */}
          <div className="space-y-2">
            <Label htmlFor="wizard-format">Content Format</Label>
            <Select value={config.format} onValueChange={(v) => update({ format: v })}>
              <SelectTrigger id="wizard-format">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {FORMAT_OPTIONS.map((option) => (
                  <SelectItem key={option} value={option}>
                    {option}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Tone */}
          <div className="space-y-2">
            <Label htmlFor="wizard-tone">Tone</Label>
            <Select value={config.tone} onValueChange={(v) => update({ tone: v })}>
              <SelectTrigger id="wizard-tone">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {TONE_OPTIONS.map((option) => (
                  <SelectItem key={option} value={option}>
                    {option}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Locale */}
          <div className="space-y-2">
            <Label htmlFor="wizard-locale">Locale</Label>
            <Input
              id="wizard-locale"
              value={config.locale}
              onChange={(e) => update({ locale: e.target.value })}
              placeholder="en"
            />
          </div>

          {/* Count */}
          <div className="space-y-2">
            <Label htmlFor="wizard-count">Number of Suggestions</Label>
            <Input
              id="wizard-count"
              type="number"
              min={1}
              max={10}
              value={config.count}
              onChange={(e) => update({ count: Math.max(1, Math.min(10, parseInt(e.target.value) || 1)) })}
            />
          </div>
        </div>

        {/* Include Images */}
        <div className="rounded-lg border border-[var(--color-border)] p-4 space-y-3">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <ImageIcon className="w-5 h-5 text-[var(--color-text-secondary)]" />
              <div>
                <Label htmlFor="wizard-images" className="text-sm font-medium">
                  Generate Hero Images
                </Label>
                <p className="text-xs text-[var(--color-text-secondary)]">
                  AI will generate a banner image for each article (uses image generation API)
                </p>
              </div>
            </div>
            <Switch
              id="wizard-images"
              checked={config.includeImages}
              onCheckedChange={(checked) => update({ includeImages: checked })}
            />
          </div>
          {config.includeImages && (
            <div className="space-y-2 pl-8">
              <Label htmlFor="wizard-image-size">Image Dimensions</Label>
              <Select
                value={`${config.imageDimensions.width}x${config.imageDimensions.height}`}
                onValueChange={(v) => {
                  const preset = IMAGE_DIMENSION_PRESETS.find((p) => `${p.width}x${p.height}` === v);
                  if (preset) update({ imageDimensions: preset });
                }}
              >
                <SelectTrigger id="wizard-image-size">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {IMAGE_DIMENSION_PRESETS.map((preset) => (
                    <SelectItem key={`${preset.width}x${preset.height}`} value={`${preset.width}x${preset.height}`}>
                      {preset.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-[var(--color-text-secondary)]">
                Auto-set based on content area. Override if needed.
              </p>
            </div>
          )}
        </div>

        <div className="pt-4 flex justify-end">
          <Button onClick={onGenerate} disabled={!canGenerate} className="gap-2">
            <Sparkles className="w-4 h-4" />
            Generate Content
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
