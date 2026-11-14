export interface ConnectionRequestResult { id: string; status: string; nextAction: string; reviewPath: string }

/** Only server results of the typed request tools can create a connection review card. */
export function connectionRequestResult(toolName: string, result?: string): ConnectionRequestResult | null {
  if (!['aonik_create_connection_request', 'aonik_get_connection_request', 'aonik_cancel_connection_request'].includes(toolName) || !result || result.length > 65_536) return null;
  try {
    const record: unknown = JSON.parse(result);
    if (!record || typeof record !== 'object' || Array.isArray(record)) return null;
    const value = record as Record<string, unknown>;
    const id = value.id ?? value.Id; const status = value.status ?? value.Status; const nextAction = value.nextAction ?? value.NextAction; const reviewPath = value.reviewPath ?? value.ReviewPath;
    if (typeof id !== 'string' || !/^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(id) || typeof status !== 'string'
      || !['Pending', 'AwaitingConsent', 'Fulfilled', 'Rejected', 'Cancelled', 'Expired'].includes(status) || typeof nextAction !== 'string' || nextAction.length > 500
      || (reviewPath !== `/settings/my-connections?requestId=${id}` && reviewPath !== `/settings/tenant-integrations?requestId=${id}`)) return null;
    return { id, status, nextAction, reviewPath };
  } catch { return null; }
}
