import * as React from 'react';
import * as DialogPrimitive from '@radix-ui/react-dialog';
import { XIcon } from 'lucide-react';
import { cn } from '@/lib/utils';

/**
 * shadcn Sheet on Radix Dialog: focus trap, Escape-to-close, scroll lock and
 * accessible labelling. Use it for "Add / Create / Edit X" forms with up to
 * ~10 fields; for atomic 2–3 field actions use Dialog.
 *
 *   <Sheet>
 *     <SheetContent size="md">
 *       <SheetHeader title="New customer" subtitle="…" icon={<UserIcon />} />
 *       <SheetBody>…fields…</SheetBody>
 *       <SheetFooter>…actions…</SheetFooter>
 *     </SheetContent>
 *   </Sheet>
 *
 * SheetHeader keeps its props API (title/subtitle/icon/closeAffordance);
 * SheetTitle and SheetDescription are exported for fully custom headers.
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
    data-slot="sheet-overlay"
    className={cn(
      'fixed inset-0 z-(--z-overlay) bg-black/50 data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0',
      className,
    )}
    {...props}
  />
));
SheetOverlay.displayName = 'SheetOverlay';

type SheetSize = 'sm' | 'md' | 'lg';
type SheetSide = 'top' | 'right' | 'bottom' | 'left';

// Widths apply to left/right sheets; below `sm` the sheet takes the full width.
const SHEET_WIDTH: Record<SheetSize, string> = {
  sm: 'sm:max-w-[460px]',
  md: 'sm:max-w-[540px]',
  lg: 'sm:max-w-[720px]',
};

const SHEET_SIDE: Record<SheetSide, string> = {
  right:
    'inset-y-0 right-0 h-full w-full border-l data-[state=closed]:slide-out-to-right data-[state=open]:slide-in-from-right',
  left: 'inset-y-0 left-0 h-full w-full border-r data-[state=closed]:slide-out-to-left data-[state=open]:slide-in-from-left',
  top: 'inset-x-0 top-0 h-auto border-b data-[state=closed]:slide-out-to-top data-[state=open]:slide-in-from-top',
  bottom:
    'inset-x-0 bottom-0 h-auto border-t data-[state=closed]:slide-out-to-bottom data-[state=open]:slide-in-from-bottom',
};

interface SheetContentProps
  extends React.ComponentPropsWithoutRef<typeof DialogPrimitive.Content> {
  size?: SheetSize;
  side?: SheetSide;
}

const SheetContent = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Content>,
  SheetContentProps
>(({ className, children, size = 'md', side = 'right', ...props }, ref) => (
  <SheetPortal>
    <SheetOverlay />
    <DialogPrimitive.Content
      ref={ref}
      data-slot="sheet-content"
      className={cn(
        'fixed z-(--z-modal) flex flex-col bg-background text-foreground shadow-lg transition ease-in-out data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:duration-200 data-[state=open]:duration-300',
        SHEET_SIDE[side],
        (side === 'left' || side === 'right') && SHEET_WIDTH[size],
        className,
      )}
      {...props}
    >
      {children}
    </DialogPrimitive.Content>
  </SheetPortal>
));
SheetContent.displayName = 'SheetContent';

const SheetTitle = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Title>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Title>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Title
    ref={ref}
    data-slot="sheet-title"
    className={cn('font-semibold text-foreground', className)}
    {...props}
  />
));
SheetTitle.displayName = DialogPrimitive.Title.displayName;

const SheetDescription = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Description>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Description>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Description
    ref={ref}
    data-slot="sheet-description"
    className={cn('text-sm text-muted-foreground', className)}
    {...props}
  />
));
SheetDescription.displayName = DialogPrimitive.Description.displayName;

interface SheetHeaderProps extends Omit<React.HTMLAttributes<HTMLDivElement>, 'title'> {
  /** Optional icon shown in a small muted tile to the left of the title. */
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
    data-slot="sheet-header"
    className={cn('flex flex-none items-start gap-3 border-b p-4', className)}
    {...rest}
  >
    {icon ? (
      <div className="grid size-8 flex-none place-items-center rounded-md bg-muted text-muted-foreground [&_svg:not([class*='size-'])]:size-4">
        {icon}
      </div>
    ) : null}
    <div className="flex min-w-0 flex-1 flex-col gap-1.5">
      <SheetTitle className="truncate">{title}</SheetTitle>
      {subtitle ? <SheetDescription className="truncate">{subtitle}</SheetDescription> : null}
    </div>
    {closeAffordance === null ? null : closeAffordance ?? (
      <DialogPrimitive.Close
        data-slot="sheet-close"
        className="rounded-xs opacity-70 ring-offset-background transition-opacity hover:opacity-100 focus:outline-hidden focus:ring-2 focus:ring-ring focus:ring-offset-2 disabled:pointer-events-none"
      >
        <XIcon className="size-4" />
        <span className="sr-only">Close</span>
      </DialogPrimitive.Close>
    )}
  </div>
);
SheetHeader.displayName = 'SheetHeader';

/** Scrolling region between the header and footer. */
const SheetBody: React.FC<React.HTMLAttributes<HTMLDivElement>> = ({ className, ...rest }) => (
  <div
    data-slot="sheet-body"
    className={cn('flex flex-1 flex-col gap-4 overflow-auto p-4', className)}
    {...rest}
  />
);
SheetBody.displayName = 'SheetBody';

/** Footer for primary and secondary actions, pinned below the body. */
const SheetFooter: React.FC<React.HTMLAttributes<HTMLDivElement>> = ({ className, ...rest }) => (
  <div
    data-slot="sheet-footer"
    className={cn('flex flex-none items-center justify-between gap-2 border-t p-4', className)}
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
  SheetTitle,
  SheetDescription,
  SheetBody,
  SheetFooter,
};
