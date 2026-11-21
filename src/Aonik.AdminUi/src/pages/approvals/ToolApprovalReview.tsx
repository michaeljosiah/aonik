import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';

import { ServerApprovalCard, type ServerApprovalState } from '@/components/ai/chatSupport';
import { api } from '@/lib/api';
import type { ToolApprovalPreview } from '@/lib/toolApprovalPreview';

interface PendingReview {
  id: string;
  tool: string;
  tier: 'Medium' | 'High';
  actionKind: string;
  proposalId: string | null;
  expiresAt: string;
  preview: ToolApprovalPreview | null;
}

export function ToolApprovalReview({ requestId }: { requestId: string }) {
  const [approval, setApproval] = useState<ServerApprovalState | null>(null);
  const [loadFailed, setLoadFailed] = useState(false);
  const path = `/ai/tool-approvals/${encodeURIComponent(requestId)}`;

  useEffect(() => {
    const controller = new AbortController();
    void api.get<PendingReview>(path, { signal: controller.signal }).then((data) => {
      setApproval({
        id: data.id,
        approvalRequestId: data.id,
        proposalId: data.proposalId ?? undefined,
        tool: data.tool,
        tier: data.tier,
        kind: data.tier === 'High' ? 'high' : 'medium',
        actionKind: data.actionKind,
        preview: data.preview ?? undefined,
        status: 'pending',
        message: `Review expires ${new Date(data.expiresAt).toLocaleString()}.`,
      });
    }).catch(() => {
      if (!controller.signal.aborted) setLoadFailed(true);
    });
    return () => controller.abort();
  }, [path]);

  const decide = async (item: ServerApprovalState, decision: 'Approve' | 'Reject') => {
    setApproval({ ...item, status: 'deciding' });
    try {
      const data = await api.post<{ outcome: 'Approved' | 'Rejected'; message?: string }>(
        `${path}/decide`, { decision },
      );
      setApproval({ ...item, status: data.outcome === 'Approved' ? 'approved' : 'rejected', message: data.message });
    } catch {
      setApproval({ ...item, status: 'error', message: 'The decision could not be recorded. Refresh the request to check its current status.' });
    }
  };

  return <section className="mx-auto max-w-3xl space-y-4 p-6" aria-labelledby="tool-approval-title">
    <Link to="/approvals" className="text-sm text-primary underline">Open approvals queue</Link>
    <h1 id="tool-approval-title" className="text-xl font-semibold">Review approval request</h1>
    {loadFailed && <p role="alert">This approval request is unavailable.</p>}
    {!loadFailed && !approval && <p role="status">Loading approval request…</p>}
    {approval && <ServerApprovalCard approval={approval} onDecide={(item, decision) => { void decide(item, decision); }} />}
  </section>;
}
