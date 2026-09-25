import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';

import { Badge } from './badge';
import { Button } from './button';
import { Switch } from './switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from './tabs';

// Spec 098 P1 rebuilt Switch and Tabs on Radix and changed Button/Badge
// variants. These tests pin the contract existing call sites rely on
// (controlled props, ARIA, legacy variant names) without a DOM environment.

describe('Switch', () => {
  it('renders a controlled checked switch with switch semantics', () => {
    const html = renderToStaticMarkup(<Switch checked onCheckedChange={() => {}} aria-label="Notify" />);
    expect(html).toContain('role="switch"');
    expect(html).toContain('aria-checked="true"');
    expect(html).toContain('data-state="checked"');
  });

  it('renders unchecked by default', () => {
    const html = renderToStaticMarkup(<Switch aria-label="Notify" />);
    expect(html).toContain('aria-checked="false"');
    expect(html).toContain('data-state="unchecked"');
  });

  it('passes disabled through to the button', () => {
    const html = renderToStaticMarkup(<Switch disabled aria-label="Notify" />);
    expect(html).toMatch(/<button[^>]*disabled=""/);
  });
});

describe('Tabs', () => {
  const tabs = (props: { value?: string; defaultValue?: string }) =>
    renderToStaticMarkup(
      <Tabs {...props}>
        <TabsList>
          <TabsTrigger value="all">All</TabsTrigger>
          <TabsTrigger value="open">Open</TabsTrigger>
        </TabsList>
        <TabsContent value="all">All orders</TabsContent>
        <TabsContent value="open">Open orders</TabsContent>
      </Tabs>,
    );

  it('marks the controlled value active and renders only its panel', () => {
    const html = tabs({ value: 'open' });
    expect(html).toContain('role="tablist"');
    expect(html).toMatch(/role="tab"[^>]*aria-selected="true"[^>]*>Open</);
    expect(html).toContain('Open orders');
    expect(html).not.toContain('All orders');
  });

  it('honours defaultValue when uncontrolled', () => {
    const html = tabs({ defaultValue: 'all' });
    expect(html).toMatch(/role="tab"[^>]*aria-selected="true"[^>]*>All</);
    expect(html).toContain('All orders');
  });

  it('links triggers to their panels for assistive tech', () => {
    const html = tabs({ defaultValue: 'all' });
    const controls = html.match(/role="tab"[^>]*aria-controls="([^"]+)"/)?.[1];
    expect(controls).toBeTruthy();
    expect(html).toContain(`id="${controls}"`);
  });

  it('supports the line variant on the list', () => {
    const html = renderToStaticMarkup(
      <Tabs defaultValue="a">
        <TabsList variant="line">
          <TabsTrigger value="a">A</TabsTrigger>
        </TabsList>
      </Tabs>,
    );
    expect(html).toContain('data-variant="line"');
  });
});

describe('Button', () => {
  it('reserves coral for the agent variant', () => {
    expect(renderToStaticMarkup(<Button variant="agent">Apply</Button>)).toContain('bg-agent');
    expect(renderToStaticMarkup(<Button variant="secondary">Export</Button>)).toContain('bg-secondary');
    expect(renderToStaticMarkup(<Button variant="secondary">Export</Button>)).not.toContain('bg-agent');
  });
});

describe('Badge', () => {
  it('keeps deprecated variant names rendering on the new tokens', () => {
    expect(renderToStaticMarkup(<Badge variant="error">Failed</Badge>)).toContain('bg-destructive');
    expect(renderToStaticMarkup(<Badge variant="pending">Pending</Badge>)).toContain('bg-warning-subtle');
    expect(renderToStaticMarkup(<Badge variant="success">Settled</Badge>)).toContain('text-success-foreground');
  });
});
