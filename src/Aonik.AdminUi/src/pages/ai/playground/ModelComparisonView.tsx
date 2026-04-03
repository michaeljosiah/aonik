import { useCallback, useState } from 'react';
import { ModelSelector } from './ModelSelector';
import { PlaygroundChatPanel } from './PlaygroundChatPanel';
import { usePlaygroundChat, type PlaygroundConfig } from '@/hooks/usePlaygroundChat';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';

interface ModelComparisonViewProps {
  sharedConfig: PlaygroundConfig;
}

export function ModelComparisonView({ sharedConfig }: ModelComparisonViewProps) {
  const [modelA, setModelA] = useState<string | null>(null);
  const [modelB, setModelB] = useState<string | null>(null);
  const [sharedDraft, setSharedDraft] = useState('');

  const chatA = usePlaygroundChat();
  const chatB = usePlaygroundChat();

  const syncConfig = useCallback(
    (modelId: string | null, chat: ReturnType<typeof usePlaygroundChat>) => {
      chat.updateConfig({ ...sharedConfig, modelId });
    },
    [sharedConfig],
  );

  syncConfig(modelA, chatA);
  syncConfig(modelB, chatB);

  const handleSendBoth = (text: string) => {
    chatA.sendMessage(text);
    chatB.sendMessage(text);
  };

  const handleResetBoth = () => {
    chatA.resetChat();
    chatB.resetChat();
  };

  return (
    <div className="flex h-full flex-col">
      {/* Model selectors */}
      <div className="grid grid-cols-2 gap-4 border-b border-[var(--color-border-light)] p-4">
        <ModelSelector label="Model A" value={modelA} onChange={(id) => setModelA(id)} />
        <ModelSelector label="Model B" value={modelB} onChange={(id) => setModelB(id)} />
      </div>

      {/* Side-by-side panels */}
      <div className="grid flex-1 grid-cols-2 divide-x divide-[var(--color-border-light)] overflow-hidden">
        <PlaygroundChatPanel
          messages={chatA.messages}
          isStreaming={chatA.isStreaming}
          streamError={chatA.streamError}
          metrics={chatA.metrics}
          onSend={handleSendBoth}
          onStop={chatA.stopStreaming}
          onReset={handleResetBoth}
        />
        <PlaygroundChatPanel
          messages={chatB.messages}
          isStreaming={chatB.isStreaming}
          streamError={chatB.streamError}
          metrics={chatB.metrics}
          onSend={handleSendBoth}
          onStop={chatB.stopStreaming}
          onReset={handleResetBoth}
        />
      </div>

      {/* Shared input */}
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (sharedDraft.trim()) {
            handleSendBoth(sharedDraft.trim());
            setSharedDraft('');
          }
        }}
        className="border-t border-[var(--color-border-light)] p-4"
      >
        <div className="flex items-center gap-2">
          <Input
            value={sharedDraft}
            onChange={(e) => setSharedDraft(e.target.value)}
            placeholder="Send to both models... (Enter to send)"
            className="h-9 flex-1"
            disabled={chatA.isStreaming || chatB.isStreaming}
          />
          <Button
            type="submit"
            size="sm"
            disabled={!sharedDraft.trim() || chatA.isStreaming || chatB.isStreaming}
          >
            Run Both
          </Button>
        </div>
      </form>
    </div>
  );
}
