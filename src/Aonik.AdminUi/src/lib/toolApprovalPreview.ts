export interface ToolApprovalPreview {
  title: string;
  before: unknown;
  after: unknown;
}

export function parseToolApprovalPreview(value: unknown): ToolApprovalPreview | undefined {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return undefined;
  const preview = value as Record<string, unknown>;
  if (typeof preview.title !== 'string') return undefined;
  return { title: preview.title, before: preview.before ?? null, after: preview.after ?? null };
}

export interface ApprovalChange { path: string; before: string; after: string }

/** Display only: the server binds approval to the original arguments, never to this diff. */
export function approvalChanges(before: unknown, after: unknown, path = '$'): ApprovalChange[] {
  if (JSON.stringify(before) === JSON.stringify(after)) return [];
  if (before && after && typeof before === 'object' && typeof after === 'object'
    && Array.isArray(before) === Array.isArray(after)) {
    const a = before as Record<string, unknown>;
    const b = after as Record<string, unknown>;
    return [...new Set([...Object.keys(a), ...Object.keys(b)])].sort()
      .flatMap(key => approvalChanges(a[key], b[key], `${path}.${key}`));
  }
  const show = (value: unknown) => value === undefined ? '(absent)' : JSON.stringify(value, null, 2);
  return [{ path, before: show(before), after: show(after) }];
}
