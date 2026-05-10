import { useEffect, useState } from 'react';
import { AudioLines } from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';

import { ProvidersTab } from './speech/ProvidersTab';
import { RecipesTab } from './speech/RecipesTab';

/**
 * Consolidated Speech & Voice settings page (spec 024). v1.1 ships the
 * Providers tab fully populated; Recipes / Voice mode / Chat speech tabs are
 * stubbed and light up as Phase B / C land.
 *
 * Route: `/settings/speech`. The legacy `/settings/voice` and
 * `/settings/text-to-speech` pages will redirect here in the next phase.
 */
export function SettingsSpeechPage() {
  // Persist active tab to URL query so deep links work (e.g.
  // `/settings/speech?tab=voice-mode` from a "Configure" link in another panel).
  const [activeTab, setActiveTab] = useState<string>(() => {
    if (typeof window === 'undefined') return 'providers';
    const params = new URLSearchParams(window.location.search);
    return params.get('tab') ?? 'providers';
  });

  useEffect(() => {
    if (typeof window === 'undefined') return;
    const url = new URL(window.location.href);
    url.searchParams.set('tab', activeTab);
    window.history.replaceState(null, '', url.toString());
  }, [activeTab]);

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-start gap-3">
        <AudioLines className="mt-1 h-6 w-6 text-primary" />
        <div>
          <h1 className="text-xl font-semibold">Speech &amp; Voice</h1>
          <p className="text-sm text-muted-foreground">
            One library of configured speech providers (STT, TTS, Composite). Compose them into
            named voice recipes; pick one as your active voice mode and one TTS for chat speech.
          </p>
        </div>
      </div>

      <Tabs value={activeTab} onValueChange={setActiveTab}>
        <TabsList>
          <TabsTrigger value="providers">Providers</TabsTrigger>
          <TabsTrigger value="recipes">Recipes</TabsTrigger>
          <TabsTrigger value="voice-mode">Voice mode</TabsTrigger>
          <TabsTrigger value="chat-speech">Chat speech</TabsTrigger>
        </TabsList>

        <TabsContent value="providers" className="space-y-6">
          <ProvidersTab />
        </TabsContent>

        <TabsContent value="recipes">
          <RecipesTab />
        </TabsContent>

        <TabsContent value="voice-mode">
          <ComingSoonCard
            title="Voice mode"
            description="Pick the active voice recipe and run a live pipeline test. Lands in Phase C of spec 024 once the recipe library is in. The legacy voice settings page at /settings/voice still works in the meantime."
          />
        </TabsContent>

        <TabsContent value="chat-speech">
          <ComingSoonCard
            title="Chat speech"
            description="Pick the active TTS provider for AGUI streaming voice synth and helper-text TTS. Lands in Phase C; the legacy /settings/text-to-speech page still works in the meantime."
          />
        </TabsContent>
      </Tabs>
    </div>
  );
}

function ComingSoonCard({ title, description }: { title: string; description: string }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{title}</CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent>
        <p className="text-sm text-muted-foreground">
          Configure providers in the <strong>Providers</strong> tab today; this tab activates when
          the next phase ships.
        </p>
      </CardContent>
    </Card>
  );
}
