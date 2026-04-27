// Click-popover submenu for sidebar items with children.
// 1:1 port of templates/aonik-admin-starterkit/kit/shell-aonik.jsx NavPopover —
// fixed-position flyout anchored to the right of its trigger row, with a
// pointer triangle, a parent header, and active-child highlight. Closes on
// outside click, Escape, or selecting a child.

import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { cn } from '@/lib/utils';
import type { NavItem, NavItemGroup } from '@/types';
import { getWorkspacePanelForRoute } from '@/workspace/registry';
import { AonikTemplateIcon } from './AonikTemplateIcon';
import { getViewportFlyoutPosition } from './flyoutPosition';
import { collectNavItemHrefs } from './starterkitSidebarNav';

interface NavPopoverProps {
  parent: NavItem;
  anchorRect: DOMRect;
  onClose: () => void;
}

function resolveHref(href: string | undefined): string {
  if (!href) return '#';
  const panel = getWorkspacePanelForRoute(href);
  return panel ? `/workspace?panel=${panel.id}` : href;
}

export function NavPopover({ parent, anchorRect, onClose }: NavPopoverProps) {
  const ref = useRef<HTMLDivElement>(null);
  const location = useLocation();
  const [nestedParent, setNestedParent] = useState<NavItem | null>(null);
  const [nestedAnchor, setNestedAnchor] = useState<DOMRect | null>(null);

  useEffect(() => {
    // Defer attaching listeners by one tick so the click that opened the
    // popover doesn't immediately close it via the document handler.
    let cleanup: (() => void) | undefined;
    const t = setTimeout(() => {
      const handleClick = (e: MouseEvent) => {
        const target = e.target as Element | null;
        if (target?.closest('.flyout-menu')) return;
        if (ref.current && !ref.current.contains(e.target as Node)) onClose();
      };
      const handleKey = (e: KeyboardEvent) => {
        if (e.key === 'Escape') onClose();
      };
      document.addEventListener('mousedown', handleClick);
      document.addEventListener('keydown', handleKey);
      cleanup = () => {
        document.removeEventListener('mousedown', handleClick);
        document.removeEventListener('keydown', handleKey);
      };
    }, 0);
    return () => {
      clearTimeout(t);
      cleanup?.();
    };
  }, [onClose]);

  const { left, top, maxHeight, pointerTop } = getViewportFlyoutPosition(anchorRect);
  const nestedPosition = useMemo(
    () => (nestedAnchor ? getViewportFlyoutPosition(nestedAnchor) : null),
    [nestedAnchor],
  );

  const groups: NavItemGroup[] =
    parent.childGroups ?? (parent.children ? [{ label: '', items: parent.children }] : []);

  return (
    <>
      <div
        ref={ref}
        className="flyout-menu fixed z-[1000] min-w-[232px] rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)] p-1.5"
        style={{
          left,
          top,
          maxHeight,
          boxShadow: '0 18px 40px -10px rgb(0 0 0 / 0.22), 0 0 0 1px rgb(0 0 0 / 0.02)',
        }}
      >
        <span
          aria-hidden
          className="absolute -left-[5px] h-[9px] w-[9px] rotate-45 border-b border-l border-[var(--color-border-light)] bg-[var(--color-surface)]"
          style={{ top: pointerTop }}
        />

        <div className="overflow-y-auto" style={{ maxHeight: maxHeight - 12 }}>
          <div className="mb-1 flex items-center justify-between gap-2 border-b border-[var(--color-border-light)] px-2.5 pb-2.5 pt-2">
            <span className="flex items-center gap-2">
              <AonikTemplateIcon name={parent.icon} size={13} color="var(--color-brand-primary)" />
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
                const isActive = collectNavItemHrefs(child).includes(location.pathname);
                const hasGrand =
                  (child.children && child.children.length > 0) ||
                  (child.childGroups && child.childGroups.length > 0);

                if (hasGrand) {
                  return (
                    <button
                      key={child.id}
                      type="button"
                      onMouseEnter={(event) => {
                        setNestedParent(child);
                        setNestedAnchor(event.currentTarget.getBoundingClientRect());
                      }}
                      className={cn(
                        'flex w-full items-center gap-2.5 rounded-md px-2.5 py-2 text-left text-[12.5px] transition-colors',
                        isActive || nestedParent?.id === child.id
                          ? 'bg-[var(--color-brand-primary-10)] font-semibold text-[var(--color-brand-primary)]'
                          : 'text-[var(--color-text-primary)] hover:bg-black/[0.04]',
                      )}
                    >
                      <AonikTemplateIcon
                        name={child.icon}
                        size={14}
                        color={isActive || nestedParent?.id === child.id ? 'var(--color-brand-primary)' : 'var(--color-text-secondary)'}
                      />
                      <span className="flex-1 truncate">{child.label}</span>
                      {child.badge != null && (
                        <span className="rounded-full bg-[var(--color-brand-secondary)] px-1.5 py-px font-mono text-[9.5px] font-semibold text-white">
                          {child.badge}
                        </span>
                      )}
                      <AonikTemplateIcon name="chevron" size={11} color="var(--color-text-tertiary)" />
                    </button>
                  );
                }

                return (
                  <Link
                    key={child.id}
                    to={resolveHref(child.href)}
                    onClick={onClose}
                    onMouseEnter={() => {
                      setNestedParent(null);
                      setNestedAnchor(null);
                    }}
                    className={cn(
                      'flex items-center gap-2.5 rounded-md px-2.5 py-2 text-[12.5px] transition-colors',
                      isActive
                        ? 'bg-[var(--color-brand-primary-10)] font-semibold text-[var(--color-brand-primary)]'
                        : 'text-[var(--color-text-primary)] hover:bg-black/[0.04]',
                    )}
                  >
                    <AonikTemplateIcon
                      name={child.icon}
                      size={14}
                      color={isActive ? 'var(--color-brand-primary)' : 'var(--color-text-secondary)'}
                    />
                    <span className="flex-1 truncate">{child.label}</span>
                    {child.badge != null && (
                      <span className="rounded-full bg-[var(--color-brand-secondary)] px-1.5 py-px font-mono text-[9.5px] font-semibold text-white">
                        {child.badge}
                      </span>
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
      </div>

      {nestedParent && nestedAnchor && nestedPosition && (
        <div
          className="flyout-menu fixed z-[1001] min-w-[232px] rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)] p-1.5"
          style={{
            left: nestedPosition.left,
            top: nestedPosition.top,
            maxHeight: nestedPosition.maxHeight,
            boxShadow: '0 18px 40px -10px rgb(0 0 0 / 0.22), 0 0 0 1px rgb(0 0 0 / 0.02)',
          }}
          onMouseLeave={() => {
            setNestedParent(null);
            setNestedAnchor(null);
          }}
        >
          <span
            aria-hidden
            className="absolute -left-[5px] h-[9px] w-[9px] rotate-45 border-b border-l border-[var(--color-border-light)] bg-[var(--color-surface)]"
            style={{ top: nestedPosition.pointerTop }}
          />

          <div className="overflow-y-auto" style={{ maxHeight: nestedPosition.maxHeight - 12 }}>
            <div className="mb-1 flex items-center gap-2 border-b border-[var(--color-border-light)] px-2.5 pb-2.5 pt-2">
              <AonikTemplateIcon name={nestedParent.icon} size={13} color="var(--color-brand-primary)" />
              <span className="text-[12.5px] font-semibold text-[var(--color-text-primary)]">
                {nestedParent.label}
              </span>
            </div>

            {(nestedParent.childGroups ?? (nestedParent.children ? [{ label: '', items: nestedParent.children }] : [])).map((group, groupIndex) => (
              <div key={group.label || groupIndex}>
                {group.label && (
                  <div className="px-2.5 pb-0.5 pt-1.5">
                    <span className="text-[10px] font-semibold uppercase tracking-wider text-[var(--color-text-tertiary)]">
                      {group.label}
                    </span>
                  </div>
                )}

                {group.items.map((child) => {
                  const isActive = collectNavItemHrefs(child).includes(location.pathname);
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
                      <AonikTemplateIcon
                        name={child.icon}
                        size={14}
                        color={isActive ? 'var(--color-brand-primary)' : 'var(--color-text-secondary)'}
                      />
                      <span className="flex-1 truncate">{child.label}</span>
                    </Link>
                  );
                })}
              </div>
            ))}
          </div>
        </div>
      )}
    </>
  );
}
