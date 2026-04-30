// React hooks for the workflows pages. Each hook owns a single fetch +
// loading/error/refresh state. Pages call them and render the three states
// directly — no shared store or react-query for now (the pages aren't busy
// enough to warrant either yet).

import { useCallback, useEffect, useState } from 'react';
import {
  workflowService,
  type WorkflowGraphDto,
  type WorkflowRunDto,
  type WorkflowSummaryDto,
  type WorkflowVersionDto,
} from '@/services/workflowService';

interface AsyncState<T> {
  data: T;
  loading: boolean;
  error: string | null;
}

function errorMessage(err: unknown): string {
  if (err && typeof err === 'object' && 'userMessage' in err) {
    return String((err as { userMessage?: string }).userMessage ?? 'Request failed');
  }
  if (err instanceof Error) return err.message;
  return 'Request failed';
}

// ── List page ──────────────────────────────────────────────────────────

export interface UseWorkflowsResult {
  workflows: WorkflowSummaryDto[];
  loading: boolean;
  error: string | null;
  refresh: () => void;
}

export function useWorkflows(): UseWorkflowsResult {
  const [state, setState] = useState<AsyncState<WorkflowSummaryDto[]>>({
    data: [],
    loading: true,
    error: null,
  });

  const refresh = useCallback(() => {
    setState((s) => ({ ...s, loading: true, error: null }));
    workflowService
      .list()
      .then((data) => setState({ data, loading: false, error: null }))
      .catch((err) => setState({ data: [], loading: false, error: errorMessage(err) }));
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  return { workflows: state.data, loading: state.loading, error: state.error, refresh };
}

// ── Editor page ────────────────────────────────────────────────────────

export interface UseWorkflowResult {
  workflow: WorkflowGraphDto | null;
  loading: boolean;
  error: string | null;
  refresh: () => void;
}

export function useWorkflow(slug: string | undefined): UseWorkflowResult {
  const [state, setState] = useState<AsyncState<WorkflowGraphDto | null>>({
    data: null,
    loading: true,
    error: null,
  });

  const refresh = useCallback(() => {
    if (!slug) {
      setState({ data: null, loading: false, error: null });
      return;
    }
    setState((s) => ({ ...s, loading: true, error: null }));
    workflowService
      .getBySlug(slug)
      .then((data) => setState({ data, loading: false, error: null }))
      .catch((err) => setState({ data: null, loading: false, error: errorMessage(err) }));
  }, [slug]);

  useEffect(() => {
    refresh();
  }, [refresh]);

  return { workflow: state.data, loading: state.loading, error: state.error, refresh };
}

// ── Recent runs (detail rail + trace bar) ──────────────────────────────

export function useWorkflowRuns(workflowId: string | undefined, take = 20) {
  const [state, setState] = useState<AsyncState<WorkflowRunDto[]>>({
    data: [],
    loading: true,
    error: null,
  });

  useEffect(() => {
    if (!workflowId) {
      setState({ data: [], loading: false, error: null });
      return;
    }
    setState((s) => ({ ...s, loading: true, error: null }));
    workflowService
      .listRuns(workflowId, take)
      .then((data) => setState({ data, loading: false, error: null }))
      .catch((err) => setState({ data: [], loading: false, error: errorMessage(err) }));
  }, [workflowId, take]);

  return { runs: state.data, loading: state.loading, error: state.error };
}

// ── Version history (editor sidebar) ───────────────────────────────────

export function useWorkflowVersions(workflowId: string | undefined) {
  const [state, setState] = useState<AsyncState<WorkflowVersionDto[]>>({
    data: [],
    loading: true,
    error: null,
  });

  useEffect(() => {
    if (!workflowId) {
      setState({ data: [], loading: false, error: null });
      return;
    }
    setState((s) => ({ ...s, loading: true, error: null }));
    workflowService
      .listVersions(workflowId)
      .then((data) => setState({ data, loading: false, error: null }))
      .catch((err) => setState({ data: [], loading: false, error: errorMessage(err) }));
  }, [workflowId]);

  return { versions: state.data, loading: state.loading, error: state.error };
}
