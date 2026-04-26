// Click-popover submenu for sidebar items with children.
// 1:1 port of templates/aonik-admin-starterkit/kit/shell-aonik.jsx NavPopover —
// fixed-position flyout anchored to the right of its trigger row, with a
// pointer triangle, a parent header, and active-child highlight. Closes on
// outside click, Escape, or selecting a child.

import { useEffect, useRef } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { ChevronRight } from 'lucide-react';
import type { ElementType } from 'react';
import { cn } from '@/lib/utils';
import type { NavItem, NavItemGroup } from '@/types';
import { getWorkspacePanelForRoute } from '@/workspace/registry';

interface NavPopoverProps {
  parent: NavItem;
  iconMap: Record<string, ElementType>;
  anchorRect: DOMRect;
  onClose: () => void;
}

function resolveHref(href: string | undefined): string {
  if (!href) return '#';
  const panel = getWorkspacePanelForRoute(href);
  return panel ? `/workspace?panel=${panel.id}` : href;
}

export function NavPopover({ parent, iconMap, anchorRect, onClose }: NavPopoverProps) {
  const ref = useRef<HTMLDivElement>(null);
  const location = useLocation();

  useEffect(() => {
    // Defer attaching listeners by one tick so the click that opened the
    // popover doesn't immediately close it via the document handler.
    const t = setTimeout(() => {
      const handleClick = (e: MouseEvent) => {
        if (ref.current && !ref.current.contains(e.target as Node)) onClose();
      };
      const handleKey = (e: KeyboardEvent) => {
        if (e.key === 'Escape') onClose();
      };
      document.addEventListener('mousedown', handleClick);
      document.addEventListener('keydown', handleKey);
      return () => {
        document.removeEventListener('mousedown', handleClick);
        document.removeEventListener('keydown', handleKey);
      };
    }, 0);
    return () => {
      clearTimeout(t);
    };
  }, [onClose]);

  const left = anchorRect.right + 8;
  const top = anchorRect.top;

  const groups: NavItemGroup[] =
    parent.childGroups ?? (parent.children ? [{ label: '', items: parent.children }] : []);

  const ParentIcon = iconMap[parent.icon];

  return (
    <div
      ref={ref}
      className="flyout-menu fixed z-[1000] min-w-[232px] rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)] p-1.5"
      style={{
        left,
        top,
        boxShadow: '0 18px 40px -10px rgb(0 0 0 / 0.22), 0 0 0 1px rgb(0 0 0 / 0.02)',
      }}
    >
      <span
        aria-hidden
        className="absolute -left-[5px] top-[14px] h-[9px] w-[9px] rotate-45 border-b border-l border-[var(--color-border-light)] bg-[var(--color-surface)]"
      />

      <div className="mb-1 flex items-center justify-between gap-2 border-b border-[var(--color-border-light)] px-2.5 pb-2.5 pt-2">
        <span className="flex items-center gap-2">
          {ParentIcon && (
            <ParentIcon className="h-[13px] w-[13px] text-[var(--color-brand-primary)]" />
          )}
          <span className="text-[12.5px] font-semibold text-[var(--color-text-primary)]">
            {parent.label}
          </span>
        </span>
        {parent.badge != null && (
          <span className="rounded-full bg-[var(--color-brand-secondary)] px-1.5 py-px font-mono text-[10px] font-semibold text-white">
            {parent.badge}
          </span>
        )}
      </div>

      {groups.map((group, groupIndex) => (
        <div key={group.label || groupIndex}>
          {group.label && (
            <div className="px-2.5 pb-0.5 pt-1.5">
              <span className="text-[10px] font-semibold uppercase tracking-wider text-[var(--color-text-tertiary)]">
                {group.label}
              </span>
            </div>
          )}
          {group.items.map((child) => {
            const ChildIcon = iconMap[child.icon];
            const isActive = !!child.href && child.href === location.pathname;
            const hasGrand =
              (child.children && child.children.length > 0) ||
              (child.childGroups && child.childGroups.length > 0);
            return (
              <Link
                key={child.id}
                to={resolveHref(child.href)}
                onClick={onClose}
                className={cn(
                  'flex items-center gap-2.5 rounded-md px-2.5 py-2 text-[12.5px] transition-colors',
                  isActive
                    ? 'bg-[var(--color-brand-primary-10)] font-semibold text-[var(--color-brand-primary)]'
                    : 'text-[var(--color-text-primary)] hover:bg-black/[0.04]',
                )}
              >
                {ChildIcon && (
                  <ChildIcon
                    className={cn(
                      'h-3.5 w-3.5 shrink-0',
                      isActive
                        ? 'text-[var(--color-brand-primary)]'
                        : 'text-[var(--color-text-secondary)]',
                    )}
                  />
                )}
                <span className="flex-1 truncate">{child.label}</span>
                {child.badge != null && (
                  <span className="rounded-full bg-[var(--color-brand-secondary)] px-1.5 py-px font-mono text-[9.5px] font-semibold text-white">
                    {child.badge}
                  </span>
                )}
                {hasGrand && (
                  <ChevronRight className="h-3 w-3 shrink-0 text-[var(--color-text-tertiary)]" />
                )}
              </Link>
            );
          })}
        </div>
      ))}

      {parent.viewAllHref && (
        <div className="mt-1 border-t border-[var(--color-border-light)] pt-1">
          <Link
            to={resolveHref(parent.viewAllHref)}
            onClick={onClose}
            className="flex items-center justify-center rounded-md px-2 py-1 text-sm text-[var(--color-brand-primary)] transition-colors hover:bg-[var(--color-sidebar-hover)]"
          >
            {parent.viewAllLabel ?? 'View all'}
          </Link>
        </div>
      )}
    </div>
  );
}
