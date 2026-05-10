import { useEffect, useState } from 'react';
import { RefreshCw } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { textToSpeechSettingsService } from '@/services/textToSpeechSettingsService';
import type { TextToSpeechVoiceOptionResponse } from '@/types';

/**
 * Vendor-aware voice picker. Three rendering modes, picked from the vendor shortcode:
 *
 *   - `static` — renders a `<Select>` over a fixed list shipped in this file. OpenAI's
 *     six TTS voices (alloy/echo/fable/onyx/nova/shimmer), the eight realtime voices,
 *     and Azure Voice Live's three voices live here.
 *   - `remote` — fetches from `/tts-settings/voices?provider=X` on mount and on Refresh
 *     click. The endpoint runs through the unified credential resolver, so it reads the
 *     SpeechProvider row's encrypted key without any extra plumbing. ElevenLabs and
 *     Mistral fall here; Azure TTS would too once the catalog endpoint is wired
 *     (today Azure stays free-text).
 *   - `free-text` — fallback for vendors without a known catalog. Plain `<Input>` so
 *     the admin can paste whatever id the vendor expects.
 *
 * A "value not in loaded list" indicator shows when the saved value doesn't appear in the
 * fetched options — useful when ElevenLabs/Mistral revoke a voice or the credential changes.
 */
export interface VoicePickerProps {
  /** Vendor shortcode from `SpeechProvider.vendor` (e.g. `openai`, `elevenlabs`, `mistral`). */
  vendor: string;
  value: string;
  onChange: (v: string) => void;
  disabled?: boolean;
  required?: boolean;
  placeholder?: string;
  id?: string;
  /** Optional label override; defaults to "Voice id". */
  label?: string;
}

interface StaticVoice {
  value: string;
  label: string;
  description?: string;
}

const STATIC_VOICES: Record<string, StaticVoice[]> = {
  // OpenAI TTS — alloy etc. Stable across tts-1 / tts-1-hd / gpt-4o-mini-tts.
  openai: [
    { value: 'alloy', label: 'Alloy', description: 'Balanced, neutral' },
    { value: 'echo', label: 'Echo', description: 'Warm, slightly lower' },
    { value: 'fable', label: 'Fable', description: 'Expressive, narrative' },
    { value: 'onyx', label: 'Onyx', description: 'Deep, authoritative' },
    { value: 'nova', label: 'Nova', description: 'Bright, energetic' },
    { value: 'shimmer', label: 'Shimmer', description: 'Soft, friendly' },
  ],
  // OpenAI Realtime — different voice catalog from chained TTS (the realtime model has
  // additional voices like ash/ballad/coral/sage/verse).
  'openai-realtime': [
    { value: 'alloy', label: 'Alloy' },
    { value: 'ash', label: 'Ash' },
    { value: 'ballad', label: 'Ballad' },
    { value: 'coral', label: 'Coral' },
    { value: 'echo', label: 'Echo' },
    { value: 'sage', label: 'Sage' },
    { value: 'shimmer', label: 'Shimmer' },
    { value: 'verse', label: 'Verse' },
  ],
  // Azure Voice Live — limited regional set today; we mirror what the vendors catalog
  // declares for parity.
  'azure-voice-live': [
    { value: 'alloy', label: 'Alloy' },
    { value: 'nova', label: 'Nova' },
    { value: 'shimmer', label: 'Shimmer' },
  ],
};

const REMOTE_VENDORS: Record<string, string> = {
  // Map vendor shortcode → provider name expected by `/tts-settings/voices?provider=…`.
  // The endpoint runs through the unified credential resolver so it reads the
  // SpeechProvider row's encrypted key automatically.
  elevenlabs: 'ElevenLabs',
  mistral: 'Mistral',
};

export function VoicePicker({
  vendor,
  value,
  onChange,
  disabled,
  required,
  placeholder,
  id,
  label = 'Voice id',
}: VoicePickerProps) {
  const normalised = (vendor ?? '').trim().toLowerCase();
  const staticOptions = STATIC_VOICES[normalised];
  const remoteProvider = REMOTE_VENDORS[normalised];

  if (staticOptions) {
    return (
      <StaticVoiceSelect
        id={id}
        label={label}
        required={required}
        options={staticOptions}
        value={value}
        onChange={onChange}
        disabled={disabled}
        placeholder={placeholder ?? 'Pick a voice…'}
      />
    );
  }
  if (remoteProvider) {
    return (
      <RemoteVoiceSelect
        id={id}
        label={label}
        required={required}
        provider={remoteProvider}
        value={value}
        onChange={onChange}
        disabled={disabled}
        placeholder={placeholder ?? `Pick a ${remoteProvider} voice…`}
      />
    );
  }
  // Unknown vendor — degrade gracefully so admin can still type a custom id.
  return (
    <FreeTextVoice
      id={id}
      label={label}
      required={required}
      value={value}
      onChange={onChange}
      disabled={disabled}
      placeholder={placeholder ?? 'Vendor-specific voice id'}
      hint={`No catalog known for vendor "${vendor || 'unknown'}" — type the voice id manually.`}
    />
  );
}

// ── Internal renderers ─────────────────────────────────────────────────────

function StaticVoiceSelect({
  id,
  label,
  required,
  options,
  value,
  onChange,
  disabled,
  placeholder,
}: {
  id?: string;
  label: string;
  required?: boolean;
  options: StaticVoice[];
  value: string;
  onChange: (v: string) => void;
  disabled?: boolean;
  placeholder: string;
}) {
  const matchesKnown = options.some((o) => o.value === value);
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>
        {label}
        {required && <span className="text-destructive"> *</span>}
      </Label>
      <Select value={value || undefined} onValueChange={onChange} disabled={disabled}>
        <SelectTrigger id={id}>
          <SelectValue placeholder={placeholder} />
        </SelectTrigger>
        <SelectContent>
          {/* Surface saved-but-unknown values so the admin sees what's persisted even if it's
              been removed from the static list since (no migration path otherwise). */}
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

function RemoteVoiceSelect({
  id,
  label,
  required,
  provider,
  value,
  onChange,
  disabled,
  placeholder,
}: {
  id?: string;
  label: string;
  required?: boolean;
  provider: string;
  value: string;
  onChange: (v: string) => void;
  disabled?: boolean;
  placeholder: string;
}) {
  const [options, setOptions] = useState<TextToSpeechVoiceOptionResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await textToSpeechSettingsService.listVoices(provider);
      setOptions(list);
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        (err as { message?: string })?.message ??
        `Failed to load ${provider} voices.`;
      setError(message);
      setOptions([]);
    } finally {
      setLoading(false);
    }
  };

  // Re-fetch when the vendor changes (e.g. user flips TTS provider in a recipe).
  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [provider]);

  const savedNotInList =
    value.length > 0 && !loading && !options.some((opt) => opt.voiceId === value);

  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between gap-2">
        <Label htmlFor={id}>
          {label}
          {required && <span className="text-destructive"> *</span>}
        </Label>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="h-6 px-2 text-[11px]"
          onClick={() => void load()}
          disabled={loading || disabled}
        >
          <RefreshCw className={`h-3 w-3 ${loading ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
      </div>
      <Select value={value || undefined} onValueChange={onChange} disabled={disabled}>
        <SelectTrigger id={id}>
          <SelectValue
            placeholder={
              loading
                ? `Loading ${provider} voices…`
                : options.length === 0
                  ? 'No voices available'
                  : placeholder
            }
          />
        </SelectTrigger>
        <SelectContent>
          {savedNotInList && value && (
            <SelectItem value={value} disabled>
              {value} (not in loaded list)
            </SelectItem>
          )}
          {options.map((opt) => (
            <SelectItem key={opt.voiceId} value={opt.voiceId}>
              {opt.name}
              {opt.labels?.gender ? ` · ${opt.labels.gender}` : ''}
              {opt.labels?.accent ? ` · ${opt.labels.accent}` : ''}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      {error && (
        <p className="text-xs text-[var(--color-error)]">
          {error}{' '}
          <span className="text-[var(--color-text-tertiary)]">
            Set the {provider} API key on the provider in the Providers tab.
          </span>
        </p>
      )}
      {!error && !loading && options.length === 0 && (
        <p className="text-xs text-[var(--color-text-tertiary)]">
          No voices loaded yet. Save a {provider} API key on the provider, then click Refresh.
        </p>
      )}
    </div>
  );
}

function FreeTextVoice({
  id,
  label,
  required,
  value,
  onChange,
  disabled,
  placeholder,
  hint,
}: {
  id?: string;
  label: string;
  required?: boolean;
  value: string;
  onChange: (v: string) => void;
  disabled?: boolean;
  placeholder: string;
  hint: string;
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>
        {label}
        {required && <span className="text-destructive"> *</span>}
      </Label>
      <Input
        id={id}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        disabled={disabled}
        placeholder={placeholder}
      />
      <p className="text-xs text-[var(--color-text-tertiary)]">{hint}</p>
    </div>
  );
}
