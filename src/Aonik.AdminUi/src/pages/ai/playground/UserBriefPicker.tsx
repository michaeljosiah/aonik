import { useState } from 'react';
import { sampleBriefs } from '@/data/sampleBriefs';
import { playgroundService } from '@/services/aiService';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { Check, AlertCircle } from 'lucide-react';

interface UserBriefPickerProps {
  value: string | null;
  onChange: (json: string | null) => void;
}

export function UserBriefPicker({ value, onChange }: UserBriefPickerProps) {
  const [userId, setUserId] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleLoadRealBrief = async () => {
    if (!userId.trim()) return;
    setLoading(true);
    setError(null);
    try {
      const brief = await playgroundService.projectUserBrief(userId.trim());
      onChange(JSON.stringify(brief, null, 2));
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-1.5">
      <Label className="text-xs">User Brief</Label>

      <Tabs defaultValue="samples">
        <TabsList className="w-full">
          <TabsTrigger value="samples" className="flex-1 text-xs">Samples</TabsTrigger>
          <TabsTrigger value="real" className="flex-1 text-xs">Real User</TabsTrigger>
          <TabsTrigger value="manual" className="flex-1 text-xs">Manual</TabsTrigger>
        </TabsList>

        {/* Sample briefs */}
        <TabsContent value="samples">
          <div className="space-y-1">
            <button
              onClick={() => onChange(null)}
              className={`w-full rounded-[2px] border px-3 py-2 text-left text-xs transition-colors ${
                value === null
                  ? 'border-[var(--color-brand-primary)] bg-[var(--color-brand-primary-light)]'
                  : 'border-[var(--color-border-light)] hover:border-[var(--color-border)]'
              }`}
            >
              <span className="font-medium text-[var(--color-text-primary)]">None</span>
            </button>
            {sampleBriefs.map((brief) => (
              <button
                key={brief.id}
                onClick={() => onChange(brief.json)}
                className={`w-full rounded-[2px] border px-3 py-2 text-left text-xs transition-colors ${
                  value === brief.json
                    ? 'border-[var(--color-brand-primary)] bg-[var(--color-brand-primary-light)]'
                    : 'border-[var(--color-border-light)] hover:border-[var(--color-border)]'
                }`}
              >
                <div className="flex items-center gap-1.5">
                  <span className="font-medium text-[var(--color-text-primary)]">{brief.name}</span>
                  {value === brief.json && (
                    <Check className="h-3 w-3 text-[var(--color-brand-primary)]" />
                  )}
                </div>
                <span className="text-[var(--color-text-tertiary)]">{brief.description}</span>
              </button>
            ))}
          </div>
        </TabsContent>

        {/* Real user lookup */}
        <TabsContent value="real">
          <div className="space-y-2">
            <div className="flex gap-2">
              <Input
                placeholder="User ID (GUID)"
                value={userId}
                onChange={(e) => setUserId(e.target.value)}
                className="h-9 flex-1"
              />
              <Button
                variant="outline"
                size="sm"
                onClick={handleLoadRealBrief}
                disabled={loading || !userId.trim()}
              >
                {loading ? '...' : 'Load'}
              </Button>
            </div>
            {error && (
              <div className="flex items-center gap-2 text-xs text-[var(--color-error)]">
                <AlertCircle className="h-3 w-3" />
                {error}
              </div>
            )}
          </div>
        </TabsContent>

        {/* Manual JSON editor */}
        <TabsContent value="manual">
          <Textarea
            value={value ?? ''}
            onChange={(e) => onChange(e.target.value || null)}
            placeholder="Paste User Brief JSON..."
            rows={6}
            className="font-mono text-xs"
          />
        </TabsContent>
      </Tabs>

      {value && (
        <p className="text-xs text-[var(--color-text-tertiary)]">
          Brief loaded (~{Math.round(value.length / 4)} tokens)
        </p>
      )}
    </div>
  );
}
