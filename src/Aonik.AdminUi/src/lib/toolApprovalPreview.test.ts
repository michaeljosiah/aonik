import { describe, expect, it } from 'vitest';
import { approvalChanges, parseToolApprovalPreview } from './toolApprovalPreview';

describe('server approval previews', () => {
  it('shows an exact changed threshold and keeps the server preview separate from the decision', () => {
    const preview = parseToolApprovalPreview({ title: 'Update invoice routing',
      before: { nodes: [{ key: 'decision', parameters: { threshold: '1000.00' } }] },
      after: { nodes: [{ key: 'decision', parameters: { threshold: '2500.00' } }] },
    })!;
    expect(approvalChanges(preview.before, preview.after)).toEqual([
      { path: '$.nodes.0.parameters.threshold', before: '"1000.00"', after: '"2500.00"' },
    ]);
  });

  it('represents creation, removal and malformed optional previews', () => {
    expect(approvalChanges(null, { name: 'Route' })).toEqual([
      { path: '$', before: 'null', after: '{\n  "name": "Route"\n}' },
    ]);
    expect(approvalChanges({ name: 'Route' }, null)[0].after).toBe('null');
    expect(parseToolApprovalPreview({ before: '<script>bad()</script>' })).toBeUndefined();
    expect(approvalChanges({ a: 1 }, { a: 1 })).toEqual([]);
  });
});
