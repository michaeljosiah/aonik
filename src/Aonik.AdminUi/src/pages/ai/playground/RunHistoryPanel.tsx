import { ChevronDown, ChevronRight, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
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
    <div className="border-t border-border">
      {/* Header */}
      <button
        onClick={() => setExpanded(!expanded)}
        className="flex w-full items-center justify-between px-5 py-2 text-xs font-medium text-muted-foreground hover:bg-accent"
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
            className="h-6 px-2 text-xs text-destructive"
          >
            <Trash2 className="mr-1 h-3 w-3" />
            Clear
          </Button>
        )}
      </button>

      {expanded && (
        <div className="max-h-48 overflow-y-auto">
          <Table className="text-xs">
            <TableHeader>
              <TableRow className="bg-muted hover:bg-muted">
                <TableHead className="h-auto px-5 py-1.5 text-muted-foreground">Time</TableHead>
                <TableHead className="h-auto px-5 py-1.5 text-muted-foreground">Agent</TableHead>
                <TableHead className="h-auto px-5 py-1.5 text-muted-foreground">Model</TableHead>
                <TableHead className="h-auto px-5 py-1.5 text-muted-foreground">Message</TableHead>
                <TableHead numeric className="h-auto px-5 py-1.5 text-muted-foreground">In</TableHead>
                <TableHead numeric className="h-auto px-5 py-1.5 text-muted-foreground">Out</TableHead>
                <TableHead numeric className="h-auto px-5 py-1.5 text-muted-foreground">Latency</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {runs.map((run) => (
                <TableRow
                  key={run.id}
                  className="cursor-pointer hover:bg-accent"
                  onClick={() => onSelect?.(run)}
                >
                  <TableCell className="px-5 py-1.5 text-muted-foreground">
                    {run.timestamp.toLocaleTimeString([], {
                      hour: '2-digit',
                      minute: '2-digit',
                      second: '2-digit',
                    })}
                  </TableCell>
                  <TableCell className="px-5 py-1.5 text-foreground">
                    {run.agentName ?? 'Raw'}
                  </TableCell>
                  <TableCell className="max-w-[150px] truncate px-5 py-1.5 text-muted-foreground">
                    {run.modelName ?? run.modelId ?? 'Default'}
                  </TableCell>
                  <TableCell className="max-w-[200px] truncate px-5 py-1.5 text-foreground">
                    {run.userMessage}
                  </TableCell>
                  <TableCell numeric className="px-5 py-1.5 text-muted-foreground">
                    {run.metrics.inputTokens}
                  </TableCell>
                  <TableCell numeric className="px-5 py-1.5 text-muted-foreground">
                    {run.metrics.outputTokens}
                  </TableCell>
                  <TableCell numeric className="px-5 py-1.5 text-muted-foreground">
                    {(run.metrics.latencyMs / 1000).toFixed(1)}s
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  );
}
