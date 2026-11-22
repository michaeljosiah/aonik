// @vitest-environment jsdom
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, it, vi } from 'vitest';
import { TooltipProvider } from '@/components/ui/tooltip';
import { EditorPalette } from './EditorPalette';

vi.mock('@/services/workflowService', () => ({ workflowService: { capabilities: async () => ({ nodeKinds: [
  { kind: 'Decision', canExecute: true }, { kind: 'Tool', canExecute: false },
] }) } }));

it('adds an available node by button in the compact palette without requiring a drag through the sheet', async () => {
  vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
  const element = document.createElement('div'); document.body.appendChild(element); const root = createRoot(element);
  const onAdd = vi.fn();
  try {
    await act(async () => root.render(<TooltipProvider><EditorPalette collapsed={false} setCollapsed={vi.fn()} onAdd={onAdd} /></TooltipProvider>));
    const add = element.querySelector<HTMLButtonElement>('button[aria-label="Add Decision node"]');
    expect(add).not.toBeNull();
    expect(element.querySelector('button[aria-label="Add Tool call node"]')).toBeNull();
    expect(element.querySelector('[draggable="true"]')).toBeNull();
    await act(async () => add!.click());
    expect(onAdd).toHaveBeenCalledExactlyOnceWith('decision');
  } finally { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); }
});
