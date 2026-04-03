import { useCallback, useEffect, useRef, useState } from 'react';
import { ArrowDownToLine, GripHorizontal } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { PlaygroundRunMetrics } from '@/lib/playground-client';

interface PlaygroundOutputPanelProps {
  output: string;
  isStreaming: boolean;
  streamError: string | null;
  metrics: PlaygroundRunMetrics | null;
  modelName?: string | null;
  onAddToMessages?: () => void;
}

const MIN_HEIGHT = 48;
const DEFAULT_HEIGHT = 220;
const MAX_RATIO = 0.7; // max 70% of viewport

export function PlaygroundOutputPanel({
  output,
  isStreaming,
  streamError,
  metrics,
  modelName,
  onAddToMessages,
}: PlaygroundOutputPanelProps) {
  const [height, setHeight] = useState(DEFAULT_HEIGHT);
  const dragging = useRef(false);
  const startY = useRef(0);
  const startH = useRef(0);
  const panelRef = useRef<HTMLDivElement>(null);
  const scrollRef = useRef<HTMLDivElement>(null);
  const hasOutput = output || isStreaming || streamError;

  // Auto-scroll to bottom while streaming
  useEffect(() => {
    if (isStreaming && scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [output, isStreaming]);

  // Expand to default height when output first appears
  useEffect(() => {
    if (hasOutput && height < DEFAULT_HEIGHT) {
      setHeight(DEFAULT_HEIGHT);
    }
  }, [hasOutput]); // eslint-disable-line react-hooks/exhaustive-deps

  const onMouseDown = useCallback(
    (e: React.MouseEvent) => {
      e.preventDefault();
      dragging.current = true;
      startY.current = e.clientY;
      startH.current = height;
      document.body.style.cursor = 'row-resize';
      document.body.style.userSelect = 'none';
    },
    [height],
  );

  useEffect(() => {
    const onMouseMove = (e: MouseEvent) => {
      if (!dragging.current) return;
      // Dragging UP increases height (startY > clientY)
      const delta = startY.current - e.clientY;
      const maxH = window.innerHeight * MAX_RATIO;
      const next = Math.min(maxH, Math.max(MIN_HEIGHT, startH.current + delta));
      setHeight(next);
    };

    const onMouseUp = () => {
      if (!dragging.current) return;
      dragging.current = false;
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
    };

    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onMouseUp);
    return () => {
      window.removeEventListener('mousemove', onMouseMove);
      window.removeEventListener('mouseup', onMouseUp);
    };
  }, []);

  return (
    <div
      ref={panelRef}
      className="shrink-0 border-t border-[var(--color-border-light)] bg-[var(--color-surface)]"
      style={{ height }}
    >
      {/* Drag handle */}
      <div
        onMouseDown={onMouseDown}
        className="group flex cursor-row-resize items-center justify-center border-b border-[var(--color-border-light)] py-1"
      >
        <GripHorizontal className="h-4 w-4 text-[var(--color-text-tertiary)] transition-colors group-hover:text-[var(--color-text-secondary)]" />
      </div>

      {/* Header row */}
      <div className="flex items-center justify-between px-6 py-2">
        <div className="flex items-center gap-2">
          <span className="text-xs font-medium text-[var(--color-text-secondary)]">
            Output
          </span>
          {(metrics?.modelName || modelName) && (
            <span className="rounded bg-[var(--color-surface-inset)] px-1.5 py-0.5 text-[10px] font-medium text-[var(--color-text-tertiary)]">
              {metrics?.modelName || modelName}
            </span>
          )}
        </div>
        <div className="flex items-center gap-2">
          {/* Metrics inline */}
          {metrics && (
            <div className="flex items-center gap-3 text-[10px] tabular-nums text-[var(--color-text-tertiary)]">
              <span>{metrics.inputTokens} in</span>
              <span>{metrics.outputTokens} out</span>
              <span className="font-medium text-[var(--color-text-secondary)]">
                {metrics.totalTokens} total
              </span>
              <span>{(metrics.latencyMs / 1000).toFixed(1)}s</span>
              {metrics.estimatedCostUsd != null && (
                <span>${metrics.estimatedCostUsd.toFixed(4)}</span>
              )}
            </div>
          )}
          {output && !isStreaming && onAddToMessages && (
            <Button
              variant="ghost"
              size="sm"
              onClick={onAddToMessages}
              className="h-6 px-2 text-xs"
            >
              <ArrowDownToLine className="mr-1 h-3 w-3" />
              Add to messages
            </Button>
          )}
        </div>
      </div>

      {/* Scrollable output content */}
      <div
        ref={scrollRef}
        className="overflow-y-auto px-6 pb-4"
        style={{ height: `calc(100% - 68px)` }}
      >
        {hasOutput ? (
          <>
            <pre className="whitespace-pre-wrap font-sans text-sm leading-relaxed text-[var(--color-text-primary)]">
              {output || (isStreaming ? '...' : '')}
            </pre>
            {streamError && (
              <div className="mt-3 rounded-[2px] border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
                {streamError}
              </div>
            )}
          </>
        ) : (
          <p className="text-xs italic text-[var(--color-text-tertiary)]">
            Run the playground to see output here.
          </p>
        )}
      </div>
    </div>
  );
}
