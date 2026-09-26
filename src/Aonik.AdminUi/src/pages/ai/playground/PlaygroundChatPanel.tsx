import { useRef, useEffect, useState } from 'react';
import { Send, Square, RotateCcw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import type { PlaygroundRunMetrics } from '@/lib/playground-client';

interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
}

interface PlaygroundChatPanelProps {
  messages: ChatMessage[];
  isStreaming: boolean;
  streamError: string | null;
  metrics: PlaygroundRunMetrics | null;
  onSend: (text: string) => void;
  onStop: () => void;
  onReset: () => void;
}

export function PlaygroundChatPanel({
  messages,
  isStreaming,
  streamError,
  metrics,
  onSend,
  onStop,
  onReset,
}: PlaygroundChatPanelProps) {
  const [draft, setDraft] = useState('');
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [messages]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (draft.trim() && !isStreaming) {
      onSend(draft.trim());
      setDraft('');
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
      e.preventDefault();
      handleSubmit(e as unknown as React.FormEvent);
    }
  };

  return (
    <div className="flex h-full flex-col">
      {/* Messages */}
      <div ref={scrollRef} className="flex-1 overflow-y-auto p-5 space-y-3">
        {messages.length === 0 && (
          <div className="flex h-full items-center justify-center">
            <p className="text-sm text-muted-foreground">
              Send a message to start testing
            </p>
          </div>
        )}
        {messages.map((msg) => (
          <div
            key={msg.id}
            className={`flex ${msg.role === 'user' ? 'justify-end' : 'justify-start'}`}
          >
            <div
              className={`max-w-[85%] rounded-lg px-3.5 py-2.5 text-sm ${
                msg.role === 'user'
                  ? 'bg-primary text-primary-foreground'
                  : 'border border-border bg-card text-foreground'
              }`}
            >
              <pre className="whitespace-pre-wrap font-sans leading-relaxed">
                {msg.content || (isStreaming && msg.role === 'assistant' ? '...' : '')}
              </pre>
            </div>
          </div>
        ))}

        {streamError && (
          <div className="rounded-md border border-destructive bg-destructive/10 px-3 py-2 text-xs text-destructive">
            {streamError}
          </div>
        )}
      </div>

      {/* Metrics bar */}
      {metrics && (
        <div className="flex items-center gap-4 border-t border-border bg-muted px-5 py-1.5 text-xs text-muted-foreground">
          <span>{metrics.inputTokens} in</span>
          <span>{metrics.outputTokens} out</span>
          <span className="font-medium text-muted-foreground">{metrics.totalTokens} total</span>
          <span>{(metrics.latencyMs / 1000).toFixed(1)}s</span>
          {metrics.estimatedCostUsd !== undefined && metrics.estimatedCostUsd !== null && (
            <span>${metrics.estimatedCostUsd.toFixed(4)}</span>
          )}
        </div>
      )}

      {/* Input */}
      <form onSubmit={handleSubmit} className="border-t border-border p-4">
        <div className="flex items-end gap-2">
          <Textarea
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Type a message... (Ctrl+Enter to send)"
            rows={2}
            className="flex-1 resize-none text-sm"
            disabled={isStreaming}
          />
          <div className="flex flex-col gap-1">
            {isStreaming ? (
              <Button type="button" variant="outline" size="sm" onClick={onStop} title="Stop">
                <Square className="h-4 w-4" />
              </Button>
            ) : (
              <Button type="submit" size="sm" disabled={!draft.trim()} title="Send (Ctrl+Enter)">
                <Send className="h-4 w-4" />
              </Button>
            )}
            <Button type="button" variant="ghost" size="sm" onClick={onReset} title="Reset chat">
              <RotateCcw className="h-4 w-4" />
            </Button>
          </div>
        </div>
      </form>
    </div>
  );
}
