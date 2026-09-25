import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-border">
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Instrument</th>
                <th className="px-4 py-3 text-right font-medium text-muted-foreground">Samples</th>
                <th className="px-4 py-3 text-right font-medium text-muted-foreground">Avg</th>
                <th className="px-4 py-3 text-right font-medium text-muted-foreground">p50</th>
                <th className="px-4 py-3 text-right font-medium text-muted-foreground">p95</th>
                <th className="px-4 py-3 text-right font-medium text-muted-foreground">p99</th>
              </tr>
            </thead>
            <tbody>
              {data.latencies.map((l, idx) => (
                <tr
                  key={l.instrument}
                  className={`border-b border-border ${idx % 2 === 1 ? 'bg-muted' : ''}`}
                >
                  <td className="px-4 py-3 font-mono text-xs text-foreground">{l.instrument}</td>
                  <td className="px-4 py-3 text-right text-foreground">{formatNumber(l.samples)}</td>
                  <td className="px-4 py-3 text-right text-foreground">{formatMs(l.avgMs)}</td>
                  <td className="px-4 py-3 text-right text-foreground">{formatMs(l.p50Ms)}</td>
                  <td className="px-4 py-3 text-right text-foreground">{formatMs(l.p95Ms)}</td>
                  <td className="px-4 py-3 text-right text-foreground">{formatMs(l.p99Ms)}</td>
                </tr>
              ))}
              {data.latencies.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-muted-foreground">
                    No retrieval latency data yet. Ensure the <code>Aonik.VectorStore</code> meter is wired in OTel.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </CardContent>
      </Card>

      {/* Per-collection stats */}
      <Card>
        <CardHeader>
          <CardTitle>Searches by collection</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-border">
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Collection</th>
                <th className="px-4 py-3 text-right font-medium text-muted-foreground">Searches</th>
                <th className="px-4 py-3 text-right font-medium text-muted-foreground">Avg Results</th>
                <th className="px-4 py-3 text-right font-medium text-muted-foreground">Empty</th>
                <th className="px-4 py-3 text-right font-medium text-muted-foreground">Avg</th>
                <th className="px-4 py-3 text-right font-medium text-muted-foreground">p95</th>
              </tr>
            </thead>
            <tbody>
              {data.collections.map((c, idx) => (
                <tr
                  key={c.collection}
                  className={`border-b border-border ${idx % 2 === 1 ? 'bg-muted' : ''}`}
                >
                  <td className="px-4 py-3 font-medium text-foreground">{c.collection}</td>
                  <td className="px-4 py-3 text-right text-foreground">{formatNumber(c.searches)}</td>
                  <td className="px-4 py-3 text-right text-foreground">{c.avgResultCount.toFixed(1)}</td>
                  <td className="px-4 py-3 text-right">
                    <span className={c.emptySearches > 0 ? 'text-amber-600 font-medium' : 'text-foreground'}>
                      {formatNumber(c.emptySearches)}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right text-foreground">{formatMs(c.avgLatencyMs)}</td>
                  <td className="px-4 py-3 text-right text-foreground">{formatMs(c.p95LatencyMs)}</td>
                </tr>
              ))}
              {data.collections.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-muted-foreground">
                    No per-collection search data yet.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
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
          color="#8b5cf6"
          formatValue={formatMs}
        />
      </div>
    </div>
  );
}
