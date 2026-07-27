// UnderlineTabs (Spec 073 §5) — extract of the house detail-page tab bar (the
// border-b-2 button row CustomerDetailPage renders inline), shared so the
// six-plus-tab commerce pages (075/078/081/082) don't hand-roll it again.

export interface UnderlineTab {
  key: string;
  label: string;
  badge?: string | number;
}

interface UnderlineTabsProps {
  tabs: UnderlineTab[];
  active: string;
  onChange: (key: string) => void;
}

export function UnderlineTabs({ tabs, active, onChange }: UnderlineTabsProps) {
  return (
    <div className="flex items-center gap-1 border-b border-[var(--color-border-light)]">
      {tabs.map((tab) => {
        const isActive = tab.key === active;
        return (
          <button
            key={tab.key}
            type="button"
            onClick={() => onChange(tab.key)}
            className={
              'h-[38px] -mb-px flex items-center gap-1.5 border-b-2 px-3.5 text-[13px] transition-colors ' +
              (isActive
                ? 'border-[var(--color-brand-primary)] font-semibold text-[var(--color-text-primary)]'
                : 'border-transparent text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]')
            }
          >
            {tab.label}
            {tab.badge != null && tab.badge !== 0 && (
              <span className="rounded-full bg-[var(--color-surface-inset)] px-1.5 py-px font-mono text-[10px] font-semibold text-[var(--color-text-secondary)]">
                {tab.badge}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}
