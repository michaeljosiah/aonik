const VIEWPORT_MARGIN = 12;
const POINTER_OFFSET = 14;
const MIN_FLYOUT_HEIGHT = 160;

export interface FlyoutPosition {
  left: number;
  top: number;
  maxHeight: number;
  pointerTop: number;
}

export function getViewportFlyoutPosition(
  anchorRect: DOMRect,
  options?: { width?: number; viewportMargin?: number },
): FlyoutPosition {
  const width = options?.width ?? 232;
  const margin = options?.viewportMargin ?? VIEWPORT_MARGIN;
  const viewportWidth = window.innerWidth;
  const viewportHeight = window.innerHeight;

  const unclampedLeft = anchorRect.right + 8;
  const maxLeft = Math.max(margin, viewportWidth - width - margin);
  const left = Math.min(unclampedLeft, maxLeft);

  const preferredTop = anchorRect.top;
  const maxTop = Math.max(margin, viewportHeight - margin - MIN_FLYOUT_HEIGHT);
  const top = Math.max(margin, Math.min(preferredTop, maxTop));

  const maxHeight = Math.max(MIN_FLYOUT_HEIGHT, viewportHeight - top - margin);
  const pointerAnchorY = anchorRect.top + POINTER_OFFSET;
  const pointerTop = Math.min(
    Math.max(pointerAnchorY - top, 10),
    Math.max(10, maxHeight - 18),
  );

  return { left, top, maxHeight, pointerTop };
}
