// @vitest-environment jsdom
import { act, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, it, vi } from 'vitest';
import { EditorSidePanel } from './EditorSidePanel';
import { JsonField } from './WorkflowJsonField';

it('opens the compact properties sheet and closes it with Escape', async () => {
  vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
  const element = document.createElement('div'); document.body.appendChild(element); const root = createRoot(element);
  function Preview() {
    const [open, setOpen] = useState(false);
    return <><button onClick={() => setOpen(true)}>Properties</button><EditorSidePanel compact open={open} onOpenChange={setOpen} title="Workflow properties"><p>Step settings</p></EditorSidePanel></>;
  }
  try {
    await act(async () => root.render(<Preview />));
    expect(document.querySelector('[role="dialog"]')).toBeNull();
    await act(async () => element.querySelector('button')!.click());
    expect(document.querySelector('[role="dialog"]')?.textContent).toContain('Step settings');
    await act(async () => { document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true })); });
    expect(document.querySelector('[role="dialog"]')).toBeNull();
  } finally { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); }
});

it('keeps incomplete JSON through attempted dismissal until it is fixed or explicitly discarded', async () => {
  vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
  const element = document.createElement('div'); document.body.appendChild(element); const root = createRoot(element);
  const onChange = vi.fn();
  function Preview() {
    const [open, setOpen] = useState(true);
    return <><button onClick={() => setOpen(true)}>Properties</button><EditorSidePanel compact open={open} onOpenChange={setOpen} title="Workflow properties"><JsonField label="Fixtures" value={[]} onChange={onChange} /></EditorSidePanel></>;
  }
  try {
    await act(async () => root.render(<Preview />));
    const input = document.querySelector('textarea')!;
    await act(async () => {
      Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value')!.set!.call(input, '{');
      input.dispatchEvent(new Event('input', { bubbles: true }));
    });
    expect(input.getAttribute('aria-invalid')).toBe('true');
    await act(async () => document.querySelector<HTMLButtonElement>('[data-slot="sheet-close"]')!.click());
    expect(document.querySelector('[role="dialog"]')).not.toBeNull();
    expect(input.value).toBe('{');
    expect(document.activeElement).toBe(input);
    await act(async () => document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true })));
    expect(document.querySelector('[role="dialog"]')).not.toBeNull();
    const discard = [...document.querySelectorAll('button')].find(button => button.textContent === 'Discard invalid edit')!;
    await act(async () => discard.click());
    expect(input.value).toBe('[]');
    await act(async () => document.querySelector<HTMLButtonElement>('[data-slot="sheet-close"]')!.click());
    expect(document.querySelector('[role="dialog"]')).toBeNull();
    expect(onChange).not.toHaveBeenCalled();
  } finally { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); }
});
