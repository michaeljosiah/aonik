// @vitest-environment jsdom
import { act, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, it, vi } from 'vitest';
import { EditorSidePanel } from './EditorSidePanel';

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
