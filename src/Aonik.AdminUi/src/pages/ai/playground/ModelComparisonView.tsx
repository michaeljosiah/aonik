import { forwardRef, useEffect, useImperativeHandle, useRef, useState } from 'react';
import { ModelSelector } from './ModelSelector';
import { PlaygroundChatPanel } from './PlaygroundChatPanel';
import { usePlaygroundChat, type PlaygroundConfig } from '@/hooks/usePlaygroundChat';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import type { PlaygroundRunRecord } from '@/types/ai';

interface ModelComparisonViewProps {
  sharedConfig: PlaygroundConfig;
  onRunRecorded?: (record: PlaygroundRunRecord) => void;
}

export interface ModelComparisonViewHandle {
  resetBoth: () => void;
}

export const ModelComparisonView = forwardRef<
  ModelComparisonViewHandle,
  ModelComparisonViewProps
>(function ModelComparisonView({ sharedConfig, onRunRecorded }, ref) {
  const [modelA, setModelA] = useState<string | null>(null);
  const [modelB, setModelB] = useState<string | null>(null);
  const [sharedDraft, setSharedDraft] = useState('');

  const chatA = usePlaygroundChat();
  const chatB = usePlaygroundChat();

  // Track last-seen history length to detect new records
  const prevLenA = useRef(0);
  const prevLenB = useRef(0);

  // Bubble new run records up to the parent's history
  useEffect(() => {
    if (chatA.runHistory.length > prevLenA.current) {
      const newRecords = chatA.runHistory.slice(0, chatA.runHistory.length - prevLenA.current);
      newRecords.forEach((r) => onRunRecorded?.(r));
    }
    prevLenA.current = chatA.runHistory.length;
  }, [chatA.runHistory, onRunRecorded]);

  useEffect(() => {
    if (chatB.runHistory.length > prevLenB.current) {
      const newRecords = chatB.runHistory.slice(0, chatB.runHistory.length - prevLenB.current);
      newRecords.forEach((r) => onRunRecorded?.(r));
    }
    prevLenB.current = chatB.runHistory.length;
  }, [chatB.runHistory, onRunRecorded]);

  // Sync shared config into each chat instance via effect, not during render
  useEffect(() => {
    chatA.updateConfig({ ...sharedConfig, modelId: modelA });
  }, [sharedConfig, modelA]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    chatB.updateConfig({ ...sharedConfig, modelId: modelB });
  }, [sharedConfig, modelB]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleSendBoth = (text: string) => {
    chatA.sendMessage(text);
    chatB.sendMessage(text);
  };

  const handleResetBoth = () => {
    chatA.resetChat();
    chatB.resetChat();
    setSharedDraft('');
  };

  // Expose reset to the parent so the header's "Reset playground" works
  useImperativeHandle(ref, () => ({ resetBoth: handleResetBoth }), [handleResetBoth]); // eslint-disable-line react-hooks/exhaustive-deps

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
});
