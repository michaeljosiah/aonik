import { ChevronDown, ChevronRight, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import type { PlaygroundRunRecord } from '@/types/ai';

interface RunHistoryPanelProps {
  runs: PlaygroundRunRecord[];
  onClear: () => void;
  onSelect?: (run: PlaygroundRunRecord) => void;
}

export function RunHistoryPanel({ runs, onClear, onSelect }: RunHistoryPanelProps) {
  const [expanded, setExpanded] = useState(false);

  if (runs.length === 0) return null;

  return (
    <div className="border-t border-[var(--color-border-light)]">
      {/* Header */}
      <button
        onClick={() => setExpanded(!expanded)}
        className="flex w-full items-center justify-between px-5 py-2 text-xs font-medium text-[var(--color-text-secondary)] hover:bg-[var(--color-background)]"
      >
        <div className="flex items-center gap-1.5">
          {expanded ? (
            <ChevronDown className="h-3.5 w-3.5" />
          ) : (
            <ChevronRight className="h-3.5 w-3.5" />
          )}
          Run History ({runs.length})
        </div>
        {expanded && (
          <Button
            variant="ghost"
            size="sm"
            onClick={(e) => {
              e.stopPropagation();
              onClear();
            }}
            className="h-6 px-2 text-xs text-[var(--color-error)]"
          >
            <Trash2 className="mr-1 h-3 w-3" />
            Clear
          </Button>
        )}
      </button>

      {expanded && (
        <div className="max-h-48 overflow-y-auto">
          <table className="w-full text-xs">
            <thead>
              <tr className="border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)]">
                <th className="px-5 py-1.5 text-left font-medium text-[var(--color-text-secondary)]">Time</th>
                <th className="px-5 py-1.5 text-left font-medium text-[var(--color-text-secondary)]">Agent</th>
                <th className="px-5 py-1.5 text-left font-medium text-[var(--color-text-secondary)]">Message</th>
                <th className="px-5 py-1.5 text-right font-medium text-[var(--color-text-secondary)]">In</th>
                <th className="px-5 py-1.5 text-right font-medium text-[var(--color-text-secondary)]">Out</th>
                <th className="px-5 py-1.5 text-right font-medium text-[var(--color-text-secondary)]">Latency</th>
              </tr>
            </thead>
            <tbody>
              {runs.map((run) => (
                <tr
                  key={run.id}
                  className="cursor-pointer border-b border-[var(--color-border-light)] last:border-b-0 hover:bg-[var(--color-background)]"
                  onClick={() => onSelect?.(run)}
                >
                  <td className="whitespace-nowrap px-5 py-1.5 text-[var(--color-text-tertiary)]">
                    {run.timestamp.toLocaleTimeString([], {
                      hour: '2-digit',
                      minute: '2-digit',
                      second: '2-digit',
                    })}
                  </td>
                  <td className="px-5 py-1.5 text-[var(--color-text-primary)]">
                    {run.agentName ?? 'Raw'}
                  </td>
                  <td className="max-w-[200px] truncate px-5 py-1.5 text-[var(--color-text-primary)]">
                    {run.userMessage}
                  </td>
                  <td className="px-5 py-1.5 text-right tabular-nums text-[var(--color-text-tertiary)]">
                    {run.metrics.inputTokens}
                  </td>
                  <td className="px-5 py-1.5 text-right tabular-nums text-[var(--color-text-tertiary)]">
                    {run.metrics.outputTokens}
                  </td>
                  <td className="px-5 py-1.5 text-right tabular-nums text-[var(--color-text-tertiary)]">
                    {(run.metrics.latencyMs / 1000).toFixed(1)}s
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
