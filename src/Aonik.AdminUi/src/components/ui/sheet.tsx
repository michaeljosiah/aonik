import * as React from 'react';
import * as DialogPrimitive from '@radix-ui/react-dialog';
import { X } from 'lucide-react';
import { cn } from '@/lib/utils';

/**
 * Right-anchored slide-out panel. Visual port of the starter template's
 * <c>SlideOutPanel</c> at <c>Templates/aonik-admin-starterkit/screens/forms.jsx</c>:
 *
 *   • Container: 460px (sm) / 540px (default) / 720px (lg) wide, full
 *     height, anchored to the right edge with a subtle left shadow.
 *   • Sticky header with a brand-tinted icon badge, title, optional
 *     subtitle, and a close button.
 *   • Scrolling body with the form fields.
 *   • Sticky footer with primary + secondary actions.
 *
 * Built on Radix Dialog so we get focus trapping, Escape-to-close,
 * scroll lock, and accessible labelling for free. Use this for "Add /
 * Create / Edit X" forms with up to ~10 fields. For longer multi-section
 * setups, fall back to a full-page form; for atomic 2-3 field actions,
 * use Dialog (centered modal).
 */

const Sheet = DialogPrimitive.Root;
const SheetTrigger = DialogPrimitive.Trigger;
const SheetClose = DialogPrimitive.Close;
const SheetPortal = DialogPrimitive.Portal;

const SheetOverlay = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Overlay>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Overlay>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Overlay
    ref={ref}
    className={cn(
      'fixed inset-0 z-[100] bg-black/40 backdrop-blur-sm data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0',
      className,
    )}
    {...props}
  />
));
SheetOverlay.displayName = 'SheetOverlay';

type SheetSize = 'sm' | 'md' | 'lg';

const SHEET_WIDTH: Record<SheetSize, string> = {
  sm: 'w-[460px]',
  md: 'w-[540px]',
  lg: 'w-[720px]',
};

interface SheetContentProps
  extends React.ComponentPropsWithoutRef<typeof DialogPrimitive.Content> {
  size?: SheetSize;
  /** When true, the default close button in the header is suppressed. */
  hideCloseButton?: boolean;
}

const SheetContent = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Content>,
  SheetContentProps
>(({ className, children, size = 'md', ...props }, ref) => (
  <SheetPortal>
    <SheetOverlay />
    <DialogPrimitive.Content
      ref={ref}
      className={cn(
        // Right-anchored, full-height slide-out. The shadow on the left
        // edge mirrors the starter template (-12px 0 32px -8px black/8).
        'fixed inset-y-0 right-0 z-[110] flex max-w-full flex-col border-l border-[var(--color-border-light)] bg-[var(--color-surface)] shadow-[-12px_0_32px_-8px_rgb(0_0_0/_0.10)]',
        SHEET_WIDTH[size],
        'data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:slide-out-to-right data-[state=open]:slide-in-from-right duration-200',
        className,
      )}
      {...props}
    >
      {children}
    </DialogPrimitive.Content>
  </SheetPortal>
));
SheetContent.displayName = 'SheetContent';

interface SheetHeaderProps extends Omit<React.HTMLAttributes<HTMLDivElement>, 'title'> {
  /** Optional brand-tinted icon shown in a 32x32 rounded badge to the left of the title. */
  icon?: React.ReactNode;
  title: React.ReactNode;
  subtitle?: React.ReactNode;
  /** Override the default close affordance; pass null to suppress it. */
  closeAffordance?: React.ReactNode | null;
}

const SheetHeader: React.FC<SheetHeaderProps> = ({
  icon,
  title,
  subtitle,
  closeAffordance,
  className,
  ...rest
}) => (
  <div
    className={cn(
      'flex flex-none items-center gap-3 border-b border-[var(--color-border-light)] px-5 py-4',
      className,
    )}
    {...rest}
  >
    {icon ? (
      <div className="grid h-8 w-8 flex-none place-items-center rounded-lg bg-[var(--color-brand-primary)]/10 text-[var(--color-brand-primary)]">
        {icon}
      </div>
    ) : null}
    <div className="min-w-0 flex-1">
      <DialogPrimitive.Title className="truncate text-[14px] font-semibold text-[var(--color-text-primary)]">
        {title}
      </DialogPrimitive.Title>
      {subtitle ? (
        <DialogPrimitive.Description className="mt-0.5 truncate text-[11.5px] text-[var(--color-text-secondary)]">
          {subtitle}
        </DialogPrimitive.Description>
      ) : null}
    </div>
    {closeAffordance === null ? null : closeAffordance ?? (
      <DialogPrimitive.Close
        aria-label="Close"
        className="grid h-7 w-7 flex-none place-items-center rounded-full text-[var(--color-text-secondary)] transition-colors hover:bg-[var(--color-surface-inset)] hover:text-[var(--color-text-primary)]"
      >
        <X className="h-3.5 w-3.5" />
      </DialogPrimitive.Close>
    )}
  </div>
);
SheetHeader.displayName = 'SheetHeader';

/**
 * Scrolling region between the header and footer. Pads the content
 * 20px on every side and stacks children with 14px gaps to match the
 * starter template's field rhythm.
 */
const SheetBody: React.FC<React.HTMLAttributes<HTMLDivElement>> = ({
  className,
  ...rest
}) => (
  <div
    className={cn(
      'flex flex-1 flex-col gap-4 overflow-auto px-5 py-5',
      className,
    )}
    {...rest}
  />
);
SheetBody.displayName = 'SheetBody';

/**
 * Sticky footer for primary + secondary actions. Uses the inset
 * surface tone so it visually separates from the body when the form
 * scrolls behind it.
 */
const SheetFooter: React.FC<React.HTMLAttributes<HTMLDivElement>> = ({
  className,
  ...rest
}) => (
  <div
    className={cn(
      'flex flex-none items-center justify-between gap-3 border-t border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-5 py-3',
      className,
    )}
    {...rest}
  />
);
SheetFooter.displayName = 'SheetFooter';

export {
  Sheet,
  SheetTrigger,
  SheetClose,
  SheetPortal,
  SheetOverlay,
  SheetContent,
  SheetHeader,
  SheetBody,
  SheetFooter,
};
