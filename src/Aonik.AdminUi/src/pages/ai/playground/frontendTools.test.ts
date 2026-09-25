import { describe, expect, it, vi } from 'vitest';

import { createPlaygroundFrontendTools } from './frontendTools';

// Spec 098 P4 / Spec 032: without an in-app handler, agent tool calls must
// fail closed rather than fall back to native browser dialogs.
describe('createPlaygroundFrontendTools fallbacks', () => {
  const context = (name: string) => ({ toolCallId: 'call-1', toolCallName: name });

  it('rejects confirmAction when no handler is supplied, without a native dialog', async () => {
    const confirmSpy = vi.fn();
    vi.stubGlobal('window', { confirm: confirmSpy, prompt: vi.fn() });

    const tools = createPlaygroundFrontendTools();
    const result = await tools
      .get('confirmAction')!
      .handler({ action: 'Capture payment', description: 'GBP 1,250', severity: 'high' }, context('confirmAction'));

    expect(result).toBe('rejected');
    expect(confirmSpy).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });

  it('chooses no option when no selectOptions handler is supplied, without a native prompt', async () => {
    const promptSpy = vi.fn();
    vi.stubGlobal('window', { confirm: vi.fn(), prompt: promptSpy });

    const tools = createPlaygroundFrontendTools();
    const result = await tools.get('display_option_selector')!.handler(
      {
        question: 'Which account?',
        options: [{ label: 'Main account' }, { label: 'Savings' }],
        multiSelect: false,
      },
      context('display_option_selector'),
    );

    expect(result).toBe('');
    expect(promptSpy).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });

  it('still uses the supplied handlers', async () => {
    const confirmAction = vi.fn(async () => 'approved');
    const tools = createPlaygroundFrontendTools({ confirmAction });
    const result = await tools
      .get('confirmAction')!
      .handler({ action: 'Send invoice', description: '', severity: 'low' }, context('confirmAction'));

    expect(result).toBe('approved');
    expect(confirmAction).toHaveBeenCalledOnce();
  });
});
