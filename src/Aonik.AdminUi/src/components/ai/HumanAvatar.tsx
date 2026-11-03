export function HumanAvatar({ name }: { name: string }) {
  const initials = name.trim().split(/[\s.@_-]+/).filter(Boolean).slice(0, 2).map(part => part[0]).join('').toUpperCase() || '?';
  return <span className="grid size-8 shrink-0 place-items-center rounded-full border bg-muted text-xs font-semibold text-muted-foreground" aria-label={`${name} · Human`}>{initials}</span>;
}
