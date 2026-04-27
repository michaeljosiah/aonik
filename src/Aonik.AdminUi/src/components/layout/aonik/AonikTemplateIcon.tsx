import type { CSSProperties, SVGProps } from 'react';

import { cn } from '@/lib/utils';

const ICON_PATHS: Record<string, string> = {
  home: '<path d="M3 11l9-7 9 7"/><path d="M5 10v10h14V10"/>',
  dashboard: '<rect x="3" y="3" width="7" height="9"/><rect x="14" y="3" width="7" height="5"/><rect x="14" y="12" width="7" height="9"/><rect x="3" y="16" width="7" height="5"/>',
  ledger: '<path d="M4 4h13a2 2 0 0 1 2 2v14H6a2 2 0 0 1-2-2V4z"/><path d="M4 4v14a2 2 0 0 0 2 2"/><path d="M8 8h8M8 12h8M8 16h5"/>',
  invoice: '<path d="M6 3h9l4 4v14H6z"/><path d="M14 3v5h5"/><path d="M9 13h6M9 17h6"/>',
  bank: '<path d="M3 10l9-6 9 6"/><path d="M5 10v7M10 10v7M14 10v7M19 10v7"/><path d="M3 20h18"/>',
  payout: '<path d="M3 12h14"/><path d="M13 6l6 6-6 6"/><circle cx="4" cy="12" r="1.5"/>',
  shield: '<path d="M12 3l8 3v6c0 5-3.5 8.5-8 9-4.5-.5-8-4-8-9V6z"/>',
  chart: '<path d="M3 20h18"/><rect x="5" y="12" width="3" height="6"/><rect x="11" y="7" width="3" height="11"/><rect x="17" y="3" width="3" height="15"/>',
  users: '<circle cx="9" cy="8" r="3"/><path d="M3 20c0-3 3-5 6-5s6 2 6 5"/><circle cx="17" cy="8" r="2.5"/><path d="M14 20c0-2.5 2.5-4 4-4"/>',
  settings: '<circle cx="12" cy="12" r="3"/><path d="M19 12a7 7 0 0 0-.2-1.6l2-1.6-2-3.4-2.4.9a7 7 0 0 0-2.8-1.6L13 2h-4l-.6 2.7a7 7 0 0 0-2.8 1.6l-2.4-.9-2 3.4 2 1.6A7 7 0 0 0 3 12c0 .5.1 1.1.2 1.6l-2 1.6 2 3.4 2.4-.9a7 7 0 0 0 2.8 1.6L9 22h4l.6-2.7a7 7 0 0 0 2.8-1.6l2.4.9 2-3.4-2-1.6c.1-.5.2-1.1.2-1.6z"/>',
  search: '<circle cx="11" cy="11" r="7"/><path d="m21 21-4.3-4.3"/>',
  bell: '<path d="M6 8a6 6 0 0 1 12 0c0 7 3 8 3 8H3s3-1 3-8"/><path d="M10 20a2 2 0 0 0 4 0"/>',
  chevron: '<path d="m9 6 6 6-6 6"/>',
  chevdown: '<path d="m6 9 6 6 6-6"/>',
  plus: '<path d="M12 5v14M5 12h14"/>',
  more: '<circle cx="5" cy="12" r="1.5"/><circle cx="12" cy="12" r="1.5"/><circle cx="19" cy="12" r="1.5"/>',
  check: '<path d="M5 12l5 5L20 7"/>',
  close: '<path d="M6 6l12 12M18 6L6 18"/>',
  x: '<path d="M6 6l12 12M18 6L6 18"/>',
  sparkles: '<path d="M12 3l1.5 4.5L18 9l-4.5 1.5L12 15l-1.5-4.5L6 9l4.5-1.5z"/><path d="M19 15l.8 2.2L22 18l-2.2.8L19 21l-.8-2.2L16 18l2.2-.8z"/>',
  bot: '<rect x="4" y="8" width="16" height="12" rx="3"/><path d="M12 3v5"/><circle cx="9" cy="14" r="1"/><circle cx="15" cy="14" r="1"/><path d="M2 14h2M20 14h2"/>',
  filter: '<path d="M3 5h18l-7 9v6l-4-2v-4z"/>',
  calendar: '<rect x="3" y="5" width="18" height="16" rx="2"/><path d="M3 10h18M8 3v4M16 3v4"/>',
  download: '<path d="M12 3v13"/><path d="m7 11 5 5 5-5"/><path d="M4 20h16"/>',
  upload: '<path d="M12 20V7"/><path d="m7 12 5-5 5 5"/><path d="M4 4h16"/>',
  help: '<circle cx="12" cy="12" r="9"/><path d="M9.5 9a2.5 2.5 0 0 1 5 0c0 1.5-2.5 2-2.5 3.5"/><path d="M12 17v.01"/>',
  fullscreen: '<path d="M3 9V3h6M21 9V3h-6M3 15v6h6M21 15v6h-6"/>',
  inbox: '<path d="M22 12H16l-2 3h-4l-2-3H2"/><path d="M5 3h14l3 9v7a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2v-7z"/>',
  building: '<rect x="4" y="3" width="16" height="18" rx="1"/><path d="M8 7h2M8 11h2M8 15h2M14 7h2M14 11h2M14 15h2"/>',
  users2: '<circle cx="9" cy="8" r="3"/><path d="M3 20c0-3 3-5 6-5s6 2 6 5"/><circle cx="17" cy="8" r="2.5"/><path d="M14 20c0-2.5 2.5-4 4-4"/>',
  network: '<circle cx="12" cy="5" r="2"/><circle cx="5" cy="19" r="2"/><circle cx="19" cy="19" r="2"/><path d="M12 7v3M9 13l-3 4M15 13l3 4M9 13h6"/><rect x="8" y="10" width="8" height="4" rx="1"/>',
  globe2: '<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3a14 14 0 0 1 0 18M12 3a14 14 0 0 0 0 18"/>',
  tag: '<path d="M3 3h9l9 9-9 9-9-9z"/><circle cx="8" cy="8" r="1.5" fill="currentColor"/>',
  arrows: '<path d="M7 3v18M3 7l4-4 4 4"/><path d="M17 21V3M13 17l4 4 4-4"/>',
  book: '<path d="M4 4h13a2 2 0 0 1 2 2v14H6a2 2 0 0 1-2-2V4z"/><path d="M4 4v14a2 2 0 0 0 2 2"/><path d="M8 8h8M8 12h8M8 16h5"/>',
  receipt: '<path d="M6 3h12v18l-2-1.5-2 1.5-2-1.5-2 1.5-2-1.5-2 1.5z"/><path d="M9 8h6M9 12h6M9 16h4"/>',
  list: '<path d="M8 6h13M8 12h13M8 18h13"/><circle cx="4" cy="6" r="1" fill="currentColor"/><circle cx="4" cy="12" r="1" fill="currentColor"/><circle cx="4" cy="18" r="1" fill="currentColor"/>',
  book2: '<path d="M4 4h13a2 2 0 0 1 2 2v14H6a2 2 0 0 1-2-2V4z"/><path d="M4 4v14a2 2 0 0 0 2 2"/>',
  wrench: '<path d="M15 3a6 6 0 0 1 5 9l-12 12-4-4L16 8a6 6 0 0 1-1-5z"/>',
  landmark: '<path d="M3 21h18M5 10v10M9 10v10M15 10v10M19 10v10M2 8l10-5 10 5v2H2z"/>',
  clipcheck: '<rect x="7" y="4" width="10" height="4" rx="1"/><path d="M7 6H5a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-2"/><path d="M9 14l2 2 4-4"/>',
  verified: '<path d="M12 2l3 2 3-1 1 3 3 2-1 3 1 3-3 2-1 3-3-1-3 2-3-2-3 1-1-3-3-2 1-3-1-3 3-2 1-3 3 1z"/><path d="M9 12l2 2 4-4"/>',
  activity: '<path d="M3 12h4l3-8 4 16 3-8h4"/>',
  gitbranch: '<circle cx="6" cy="5" r="2"/><circle cx="18" cy="19" r="2"/><circle cx="6" cy="19" r="2"/><path d="M6 7v10"/><path d="M18 17V9a4 4 0 0 0-4-4h-2"/>',
  terminal: '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="m6 9 3 3-3 3M12 15h5"/>',
  route: '<circle cx="5" cy="6" r="2"/><circle cx="19" cy="18" r="2"/><path d="M7 6h4a4 4 0 0 1 4 4v4a4 4 0 0 0 4 4"/>',
};

interface AonikTemplateIconProps extends Omit<SVGProps<SVGSVGElement>, 'color'> {
  name: string;
  size?: number;
  color?: string;
}

export function AonikTemplateIcon({
  name,
  size = 18,
  color = 'currentColor',
  className,
  style,
  ...props
}: AonikTemplateIconProps) {
  const paths = ICON_PATHS[name];
  if (!paths) return null;

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke={color}
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={cn('inline-block shrink-0 align-[-3px]', className)}
      style={{ flex: 'none', display: 'inline-block', verticalAlign: '-3px', ...(style as CSSProperties | undefined) }}
      dangerouslySetInnerHTML={{ __html: paths }}
      aria-hidden="true"
      {...props}
    />
  );
}
