import type { WorkspacePanelRenderProps } from '../types';

export function PlaceholderPanel({ title }: WorkspacePanelRenderProps) {
  return (
    <div className="flex items-center justify-center h-full">
      <div className="text-center">
        <h1 className="text-2xl font-bold text-[var(--color-text-primary)] mb-2">{title}</h1>
        <p className="text-[var(--color-text-secondary)]">This page is under construction.</p>
      </div>
    </div>
  );
}
