import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';

import { AonikTemplateIcon } from './AonikTemplateIcon';

describe('AonikTemplateIcon', () => {
  it('renders shared menu glyphs with theme-inherited colour and the starter-kit stroke contract', () => {
    const markup = renderToStaticMarkup(<AonikTemplateIcon name="menu-invoices" size={16} className="text-sidebar-primary" />);

    expect(markup).toContain('data-icon="menu-invoices"');
    expect(markup).toContain('viewBox="0 0 24 24"');
    expect(markup).toContain('stroke="currentColor"');
    expect(markup).toContain('stroke-width="1.75"');
    expect(markup).toContain('stroke-linecap="round"');
    expect(markup).toContain('aria-hidden="true"');
    expect(markup).toContain('text-sidebar-primary');
    expect(markup).toContain('<path');
  });

  it('keeps existing non-menu icons available', () => {
    expect(renderToStaticMarkup(<AonikTemplateIcon name="sparkles" />)).toContain('<path');
    expect(renderToStaticMarkup(<AonikTemplateIcon name="missing-icon" />)).toBe('');
  });
});
