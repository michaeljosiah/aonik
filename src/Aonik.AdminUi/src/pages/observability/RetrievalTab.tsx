import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { MetricCard, TimeSeriesChart } from '@/components/charts';
import type { RetrievalResponse } from '@/services/observabilityService';

function formatMs(v: number): string {
  if (!Number.isFinite(v)) return '—';
  if (v >= 1000) return `${(v / 1000).toFixed(1)}s`;
  return `${Math.round(v)}ms`;
}

function formatNumber(v: number): string {
  if (v >= 1_000_000) return `${(v / 1_000_000).toFixed(1)}M`;
  if (v >= 1_000) return `${(v / 1_000).toFixed(1)}K`;
  return v.toLocaleString();
}

export function RetrievalTab({ data }: { data: RetrievalResponse }) {
  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <MetricCard label="Qdrant Searches" value={formatNumber(data.totalSearches)} />
        <MetricCard label="Qdrant Upserts" value={formatNumber(data.totalUpserts)} />
        <MetricCard label="Embedding Calls" value={formatNumber(data.totalEmbeddingCalls)} />
        <MetricCard
          label="Embedding Errors"
          value={formatNumber(data.embeddingErrorCount)}
          status={data.embeddingErrorCount === 0 ? 'good' : data.embeddingErrorCount < 5 ? 'warning' : 'critical'}
        />
      </div>

      {/* Latency distributions */}
      <Card>
        <CardHeader>
          <CardTitle>Latency by instrument</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow className="hover:bg-transparent">
                <TableHead className="px-4 text-muted-foreground">Instrument</TableHead>
                <TableHead numeric className="px-4 text-muted-foreground">Samples</TableHead>
                <TableHead numeric className="px-4 text-muted-foreground">Avg</TableHead>
                <TableHead numeric className="px-4 text-muted-foreground">p50</TableHead>
                <TableHead numeric className="px-4 text-muted-foreground">p95</TableHead>
                <TableHead numeric className="px-4 text-muted-foreground">p99</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.latencies.map((l, idx) => (
                <TableRow key={l.instrument} className={idx % 2 === 1 ? 'bg-muted' : ''}>
                  <TableCell className="px-4 py-3 font-mono text-xs text-foreground">{l.instrument}</TableCell>
                  <TableCell numeric className="px-4 py-3 text-foreground">{formatNumber(l.samples)}</TableCell>
                  <TableCell numeric className="px-4 py-3 text-foreground">{formatMs(l.avgMs)}</TableCell>
                  <TableCell numeric className="px-4 py-3 text-foreground">{formatMs(l.p50Ms)}</TableCell>
                  <TableCell numeric className="px-4 py-3 text-foreground">{formatMs(l.p95Ms)}</TableCell>
                  <TableCell numeric className="px-4 py-3 text-foreground">{formatMs(l.p99Ms)}</TableCell>
                </TableRow>
              ))}
              {data.latencies.length === 0 && (
                <TableRow className="hover:bg-transparent">
                  <TableCell colSpan={6} className="whitespace-normal px-4 py-8 text-center text-muted-foreground">
                    No retrieval latency data yet. Ensure the <code>Aonik.VectorStore</code> meter is wired in OTel.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* Per-collection stats */}
      <Card>
        <CardHeader>
          <CardTitle>Searches by collection</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow className="hover:bg-transparent">
                <TableHead className="px-4 text-muted-foreground">Collection</TableHead>
                <TableHead numeric className="px-4 text-muted-foreground">Searches</TableHead>
                <TableHead numeric className="px-4 text-muted-foreground">Avg Results</TableHead>
                <TableHead numeric className="px-4 text-muted-foreground">Empty</TableHead>
                <TableHead numeric className="px-4 text-muted-foreground">Avg</TableHead>
                <TableHead numeric className="px-4 text-muted-foreground">p95</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.collections.map((c, idx) => (
                <TableRow key={c.collection} className={idx % 2 === 1 ? 'bg-muted' : ''}>
                  <TableCell className="px-4 py-3 font-medium text-foreground">{c.collection}</TableCell>
                  <TableCell numeric className="px-4 py-3 text-foreground">{formatNumber(c.searches)}</TableCell>
                  <TableCell numeric className="px-4 py-3 text-foreground">{c.avgResultCount.toFixed(1)}</TableCell>
                  <TableCell numeric className="px-4 py-3">
                    <span className={c.emptySearches > 0 ? 'text-warning font-medium' : 'text-foreground'}>
                      {formatNumber(c.emptySearches)}
                    </span>
                  </TableCell>
                  <TableCell numeric className="px-4 py-3 text-foreground">{formatMs(c.avgLatencyMs)}</TableCell>
                  <TableCell numeric className="px-4 py-3 text-foreground">{formatMs(c.p95LatencyMs)}</TableCell>
                </TableRow>
              ))}
              {data.collections.length === 0 && (
                <TableRow className="hover:bg-transparent">
                  <TableCell colSpan={6} className="px-4 py-8 text-center text-muted-foreground">
                    No per-collection search data yet.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* Time series */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <TimeSeriesChart
          data={data.searchLatencyTimeSeries}
          label="Qdrant search p95"
          formatValue={formatMs}
        />
        <TimeSeriesChart
          data={data.embeddingLatencyTimeSeries}
          label="Embedding p95"
          color="var(--chart-4)"
          formatValue={formatMs}
        />
      </div>
    </div>
  );
}
