import { useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { BookOpenIcon, MoonIcon, SettingsIcon, SparklesIcon, SunIcon } from 'lucide-react';

import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
  CommandShortcut,
} from '@/components/ui/command';
import { useTheme } from '@/contexts';
import type { NavItem } from '@/types';
import { getWorkspacePanelForRoute } from '@/workspace/registry';

import { AonikTemplateIcon } from './AonikTemplateIcon';
import { useVisibleNav } from './useVisibleNav';

interface PaletteEntry {
  id: string;
  label: string;
  href: string;
  icon: string;
  /** Parent labels, matched by search ("Ledger" finds "Journal entries"). */
  trail: string[];
}

function resolveHref(href: string): string {
  const panel = getWorkspacePanelForRoute(href);
  return panel ? `/workspace?panel=${panel.id}` : href;
}

function flatten(items: NavItem[], trail: string[], out: PaletteEntry[]) {
  for (const item of items) {
    if (item.href) out.push({ id: item.id, label: item.label, href: item.href, icon: item.icon, trail });
    const groups = item.childGroups ?? (item.children ? [{ label: '', items: item.children }] : []);
    for (const group of groups) flatten(group.items, [...trail, item.label, group.label].filter(Boolean), out);
  }
}

export interface CommandPaletteProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onAskAonik?: () => void;
  shortcutLabel: string;
}

/**
 * Ctrl/Cmd+K palette: every page the user can see in the sidebar (same
 * audience and module filtering) plus a few global actions.
 */
export function CommandPalette({ open, onOpenChange, onAskAonik, shortcutLabel }: CommandPaletteProps) {
  const navigate = useNavigate();
  const { sections } = useVisibleNav();
  const { resolvedTheme, setTheme } = useTheme();

  const groups = useMemo(
    () =>
      sections
        .map((section) => {
          const entries: PaletteEntry[] = [];
          flatten(section.items, [], entries);
          return { id: section.id, label: section.label ?? 'Pages', entries };
        })
        .filter((g) => g.entries.length > 0),
    [sections],
  );

  const run = (fn: () => void) => {
    onOpenChange(false);
    fn();
  };

  return (
    <CommandDialog open={open} onOpenChange={onOpenChange} title="Search Aonik" description="Go to a page or run an action">
      <CommandInput placeholder="Go to a page or run an action" />
      <CommandList>
        <CommandEmpty>No pages or actions match.</CommandEmpty>
        {groups.map((group) => (
          <CommandGroup key={group.id} heading={group.label}>
            {group.entries.map((entry) => (
              <CommandItem
                key={`${group.id}-${entry.id}`}
                value={`${group.id}-${entry.id}`}
                keywords={[entry.label, ...entry.trail]}
                onSelect={() => run(() => navigate(resolveHref(entry.href)))}
              >
                <AonikTemplateIcon name={entry.icon} size={16} />
                <span>{entry.label}</span>
                {entry.trail.length > 0 && (
                  <span className="truncate text-xs text-muted-foreground">{entry.trail.join(' / ')}</span>
                )}
              </CommandItem>
            ))}
          </CommandGroup>
        ))}
        <CommandSeparator />
        <CommandGroup heading="Actions">
          {onAskAonik && (
            <CommandItem value="action-ask" keywords={['ask', 'ai', 'agent', 'chat']} onSelect={() => run(onAskAonik)}>
              <SparklesIcon />
              Ask Aonik
              <CommandShortcut>{shortcutLabel}/</CommandShortcut>
            </CommandItem>
          )}
          <CommandItem
            value="action-theme"
            keywords={['theme', 'dark', 'light', 'appearance']}
            onSelect={() => run(() => setTheme(resolvedTheme === 'dark' ? 'light' : 'dark'))}
          >
            {resolvedTheme === 'dark' ? <SunIcon /> : <MoonIcon />}
            Switch to {resolvedTheme === 'dark' ? 'light' : 'dark'} theme
          </CommandItem>
          <CommandItem value="action-settings" keywords={['settings', 'preferences']} onSelect={() => run(() => navigate('/settings'))}>
            <SettingsIcon />
            Open settings
          </CommandItem>
          <CommandItem value="action-guides" keywords={['help', 'guides', 'docs']} onSelect={() => run(() => navigate('/setup-guides'))}>
            <BookOpenIcon />
            Open guides
          </CommandItem>
        </CommandGroup>
      </CommandList>
    </CommandDialog>
  );
}
