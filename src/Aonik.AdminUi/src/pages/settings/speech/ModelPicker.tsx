import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

/**
 * Vendor-aware model picker — companion to <see cref="VoicePicker"/>. Model selection is
 * optional everywhere it appears (recipe / chat-speech / test panel), so the picker always
 * surfaces a "(use provider default)" sentinel at the top of the list. Each vendor's known
 * models are hardcoded here; vendors without a known catalog (Azure today) fall back to a
 * free-text input so the admin can paste a custom id.
 *
 * Stale or experimental ids show as "(custom)" in the dropdown rather than getting silently
 * normalised away — the same pattern as VoicePicker.
 */

export interface ModelPickerProps {
  vendor: string;
  value: string;
  onChange: (v: string) => void;
  disabled?: boolean;
  id?: string;
  label?: string;
  /**
   * Whether to surface a `Refresh` link or any vendor-API integration. None today — we keep
   * model lists static because vendors don't expose a stable model-list API the way they do
   * voices. Reserved for future use.
   */
}

interface ModelOption {
  value: string;
  label: string;
  description?: string;
}

const STATIC_MODELS: Record<string, ModelOption[]> = {
  // OpenAI TTS chained — keep aligned with the vendor catalog defaults.
  openai: [
    { value: 'tts-1', label: 'tts-1', description: 'Standard quality, lower latency' },
    { value: 'tts-1-hd', label: 'tts-1-hd', description: 'Higher fidelity' },
    { value: 'gpt-4o-mini-tts', label: 'gpt-4o-mini-tts', description: 'GPT-4o-based' },
  ],
  // OpenAI Whisper STT — only one model published today.
  'openai-whisper': [{ value: 'whisper-1', label: 'whisper-1', description: 'Standard Whisper' }],
  elevenlabs: [
    {
      value: 'eleven_multilingual_v2',
      label: 'eleven_multilingual_v2',
      description: 'Stable, multi-language',
    },
    { value: 'eleven_turbo_v2_5', label: 'eleven_turbo_v2_5', description: 'Lower latency' },
    { value: 'eleven_flash_v2_5', label: 'eleven_flash_v2_5', description: 'Lowest latency' },
  ],
  mistral: [
    {
      value: 'voxtral-mini-tts-2603',
      label: 'voxtral-mini-tts-2603',
      description: 'Production Voxtral mini TTS',
    },
  ],
  'openai-realtime': [
    { value: 'gpt-realtime-mini', label: 'gpt-realtime-mini', description: 'Cost-optimised' },
    { value: 'gpt-realtime', label: 'gpt-realtime', description: 'Highest fidelity' },
  ],
  'azure-voice-live': [
    { value: 'gpt-realtime-mini', label: 'gpt-realtime-mini' },
    { value: 'gpt-realtime', label: 'gpt-realtime' },
    { value: 'phi4-mm-realtime', label: 'phi4-mm-realtime' },
  ],
};

const DEFAULT_SENTINEL = '__provider_default__';

export function ModelPicker({
  vendor,
  value,
  onChange,
  disabled,
  id,
  label = 'Model override',
}: ModelPickerProps) {
  const normalised = (vendor ?? '').trim().toLowerCase();
  const options = STATIC_MODELS[normalised];

  if (!options) {
    // Unknown vendor — degrade to free text. Same fallback strategy as VoicePicker.
    return (
      <div className="space-y-1.5">
        <Label htmlFor={id}>{label}</Label>
        <Input
          id={id}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          disabled={disabled}
          placeholder="leave blank to use provider default"
        />
      </div>
    );
  }

  // Empty value → render "(use provider default)" sentinel selected.
  // Custom value not in the static list → render it as a disabled "(custom)" item so the
  // admin sees what's persisted. Same defensive UX as VoicePicker's "(not in loaded list)".
  const matchesKnown = options.some((o) => o.value === value);
  const selectValue = value === '' ? DEFAULT_SENTINEL : value;

  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Select
        value={selectValue}
        onValueChange={(v) => onChange(v === DEFAULT_SENTINEL ? '' : v)}
        disabled={disabled}
      >
        <SelectTrigger id={id}>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={DEFAULT_SENTINEL}>
            <span className="text-[var(--color-text-tertiary)]">(use provider default)</span>
          </SelectItem>
          {value && !matchesKnown && (
            <SelectItem value={value} disabled>
              {value} (custom)
            </SelectItem>
          )}
          {options.map((opt) => (
            <SelectItem key={opt.value} value={opt.value}>
              {opt.label}
              {opt.description ? ` — ${opt.description}` : ''}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
