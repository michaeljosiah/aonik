// @vitest-environment jsdom
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, it, vi } from 'vitest';
import { JsonField } from './DecisionTablePanel';

vi.mock('@/services/workflowService', () => ({ workflowService: {} }));

it('blocks invalid JSON and clears native validity when an external revision replaces it', async () => {
  vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
  const element = document.createElement('div'); document.body.appendChild(element);
  const root = createRoot(element); const onChange = vi.fn();
  try {
    await act(async () => root.render(<JsonField label="Fixtures" value={[]} onChange={onChange} />));
    const textarea = element.querySelector('textarea')!;
    await act(async () => {
      Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value')!.set!.call(textarea, '{');
      textarea.dispatchEvent(new Event('input', { bubbles: true }));
    });
    expect(textarea.checkValidity()).toBe(false);
    expect(element.textContent).toContain('Enter valid JSON before saving.');
    expect(onChange).not.toHaveBeenCalled();
    await act(async () => root.render(<JsonField label="Fixtures" value={[{ id: 'restored' }]} onChange={onChange} />));
    expect(textarea.checkValidity()).toBe(true);
    expect(textarea.value).toContain('restored');
    expect(textarea.getAttribute('aria-invalid')).toBe('false');
  } finally { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); }
});
