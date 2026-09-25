import { useState, type ReactNode } from 'react';
import {
  ArrowUpRightIcon,
  CopyIcon,
  MailIcon,
  MoonIcon,
  MoreHorizontalIcon,
  PencilIcon,
  PlusIcon,
  SearchIcon,
  SendIcon,
  SunIcon,
  Trash2Icon,
  UserIcon,
} from 'lucide-react';
import { toast } from 'sonner';

import { useTheme } from '@/contexts/ThemeContext';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuShortcut,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  Field,
  FieldDescription,
  FieldError,
  FieldGroup,
  FieldLabel,
  FieldLegend,
  FieldSet,
} from '@/components/ui/field';
import { HoverCard, HoverCardContent, HoverCardTrigger } from '@/components/ui/hover-card';
import { Input } from '@/components/ui/input';
import {
  InputGroup,
  InputGroupAddon,
  InputGroupButton,
  InputGroupInput,
  InputGroupText,
} from '@/components/ui/input-group';
import { Label } from '@/components/ui/label';
import { NativeSelect } from '@/components/ui/native-select';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectSeparator,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Separator } from '@/components/ui/separator';
import { Sheet, SheetBody, SheetContent, SheetFooter, SheetHeader, SheetTrigger } from '@/components/ui/sheet';
import { Skeleton } from '@/components/ui/skeleton';
import { Spinner } from '@/components/ui/spinner';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';

/**
 * Dev-only kitchen sink for the Spec 098 primitives: every variant and state,
 * in whichever theme is active. Mounted at /dev/ui by App.tsx in dev builds
 * only, ahead of auth, so it renders without a backend.
 */
export default function DevUiPage() {
  const { resolvedTheme, toggleTheme } = useTheme();
  const [notify, setNotify] = useState(true);
  const [columns, setColumns] = useState({ customer: true, amount: true, status: false });

  return (
    <div className="h-full overflow-auto bg-background text-foreground">
      <div className="mx-auto flex max-w-5xl flex-col gap-10 px-6 py-8">
        <header className="flex items-end justify-between gap-6">
          <div className="flex flex-col gap-1">
            <h1 className="text-2xl font-semibold tracking-tight">Primitives</h1>
            <p className="text-sm text-muted-foreground">
              Every control in <code className="font-mono text-xs">components/ui</code>, as Spec 098 restyles it.
            </p>
          </div>
          <Button variant="outline" onClick={toggleTheme}>
            {resolvedTheme === 'dark' ? <SunIcon /> : <MoonIcon />}
            {resolvedTheme === 'dark' ? 'Light theme' : 'Dark theme'}
          </Button>
        </header>

        <Section title="Button" description="Coral (agent) is only for applying an agent proposal.">
          <Row>
            <Button>Create invoice</Button>
            <Button variant="secondary">Export</Button>
            <Button variant="outline">Filters</Button>
            <Button variant="ghost">Cancel</Button>
            <Button variant="link">View ledger</Button>
            <Button variant="destructive">Void invoice</Button>
            <Button variant="agent">Apply proposal</Button>
          </Row>
          <Row>
            <Button size="sm">Small</Button>
            <Button>Default</Button>
            <Button size="lg">Large</Button>
            <Button size="icon-sm" variant="outline" aria-label="Copy order ID">
              <CopyIcon />
            </Button>
            <Button size="icon" variant="outline" aria-label="Edit">
              <PencilIcon />
            </Button>
            <Button size="icon-lg" variant="outline" aria-label="Add">
              <PlusIcon />
            </Button>
            <Button>
              <SendIcon /> Send invoice
            </Button>
            <Button disabled>
              <Spinner /> Send invoice
            </Button>
            <Button disabled variant="outline">
              Disabled
            </Button>
          </Row>
        </Section>

        <Section title="Badge" description="Status colours appear only on status.">
          <Row>
            <Badge>Default</Badge>
            <Badge variant="secondary">Draft</Badge>
            <Badge variant="outline">Manual</Badge>
            <Badge variant="success">Settled</Badge>
            <Badge variant="warning">Pending</Badge>
            <Badge variant="info">Quoted</Badge>
            <Badge variant="destructive">Failed</Badge>
            <Badge variant="success">
              <span className="size-1.5 rounded-full bg-current" aria-hidden="true" />
              Live
            </Badge>
          </Row>
        </Section>

        <Section title="Form fields" description="Label, control, description and error laid out by Field.">
          <div className="grid gap-8 md:grid-cols-2">
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="dev-name">Customer name</FieldLabel>
                <Input id="dev-name" placeholder="Adaeze Okafor" />
                <FieldDescription>Shown on invoices and receipts.</FieldDescription>
              </Field>
              <Field data-invalid="true">
                <FieldLabel htmlFor="dev-email">Email</FieldLabel>
                <Input
                  id="dev-email"
                  type="email"
                  defaultValue="adaeze@"
                  aria-invalid="true"
                  aria-describedby="dev-email-error"
                />
                <FieldError id="dev-email-error">Enter a full email address, like name@example.com.</FieldError>
              </Field>
              <Field>
                <FieldLabel htmlFor="dev-notes">Notes</FieldLabel>
                <Textarea id="dev-notes" placeholder="Anything the finance team should know" />
              </Field>
              <Field>
                <FieldLabel htmlFor="dev-disabled">Tenant ID</FieldLabel>
                <Input id="dev-disabled" disabled defaultValue="tnt_01J8Z4" className="font-mono" />
              </Field>
            </FieldGroup>

            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="dev-currency">Currency</FieldLabel>
                <Select defaultValue="GBP">
                  <SelectTrigger id="dev-currency">
                    <SelectValue placeholder="Choose a currency" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectGroup>
                      <SelectLabel>Send</SelectLabel>
                      <SelectItem value="GBP">GBP, British pound</SelectItem>
                      <SelectItem value="USD">USD, US dollar</SelectItem>
                    </SelectGroup>
                    <SelectSeparator />
                    <SelectGroup>
                      <SelectLabel>Receive</SelectLabel>
                      <SelectItem value="NGN">NGN, Nigerian naira</SelectItem>
                      <SelectItem value="GHS">GHS, Ghanaian cedi</SelectItem>
                    </SelectGroup>
                  </SelectContent>
                </Select>
              </Field>
              <Field>
                <FieldLabel htmlFor="dev-native">Country (native)</FieldLabel>
                <NativeSelect id="dev-native" defaultValue="NG">
                  <option value="GB">United Kingdom</option>
                  <option value="NG">Nigeria</option>
                  <option value="GH">Ghana</option>
                </NativeSelect>
              </Field>
              <Field>
                <FieldLabel htmlFor="dev-search">Search</FieldLabel>
                <InputGroup>
                  <InputGroupAddon>
                    <SearchIcon />
                  </InputGroupAddon>
                  <InputGroupInput id="dev-search" placeholder="Order ID or customer" />
                  <InputGroupAddon align="inline-end">
                    <InputGroupButton size="xs">Search</InputGroupButton>
                  </InputGroupAddon>
                </InputGroup>
              </Field>
              <Field>
                <FieldLabel htmlFor="dev-amount">Amount</FieldLabel>
                <InputGroup>
                  <InputGroupAddon>
                    <InputGroupText>£</InputGroupText>
                  </InputGroupAddon>
                  <InputGroupInput id="dev-amount" inputMode="decimal" placeholder="0.00" className="font-mono tabular-nums" />
                  <InputGroupAddon align="inline-end">
                    <InputGroupText>GBP</InputGroupText>
                  </InputGroupAddon>
                </InputGroup>
              </Field>
            </FieldGroup>
          </div>
        </Section>

        <Section title="Choices" description="Checkbox, radio group, choice cards and switch.">
          <div className="grid gap-8 md:grid-cols-2">
            <FieldSet>
              <FieldLegend variant="label">Notify the customer when</FieldLegend>
              <FieldGroup className="gap-3">
                <Field orientation="horizontal">
                  <Checkbox id="dev-c1" defaultChecked />
                  <FieldLabel htmlFor="dev-c1" className="font-normal">The invoice is issued</FieldLabel>
                </Field>
                <Field orientation="horizontal">
                  <Checkbox id="dev-c2" />
                  <FieldLabel htmlFor="dev-c2" className="font-normal">A payment settles</FieldLabel>
                </Field>
                <Field orientation="horizontal">
                  <Checkbox id="dev-c3" checked="indeterminate" />
                  <FieldLabel htmlFor="dev-c3" className="font-normal">Some reminders (indeterminate)</FieldLabel>
                </Field>
                <Field orientation="horizontal" data-disabled="true">
                  <Checkbox id="dev-c4" disabled />
                  <FieldLabel htmlFor="dev-c4" className="font-normal">SMS (not configured)</FieldLabel>
                </Field>
              </FieldGroup>
            </FieldSet>

            <FieldSet>
              <FieldLegend variant="label">Payout speed</FieldLegend>
              <RadioGroup defaultValue="standard">
                <FieldLabel htmlFor="dev-r1">
                  <Field orientation="horizontal">
                    <RadioGroupItem value="standard" id="dev-r1" />
                    <div className="flex flex-col gap-1">
                      <span className="font-medium">Standard</span>
                      <FieldDescription>Arrives in 1 to 2 working days. No fee.</FieldDescription>
                    </div>
                  </Field>
                </FieldLabel>
                <FieldLabel htmlFor="dev-r2">
                  <Field orientation="horizontal">
                    <RadioGroupItem value="instant" id="dev-r2" />
                    <div className="flex flex-col gap-1">
                      <span className="font-medium">Instant</span>
                      <FieldDescription>Arrives within minutes. 1% fee.</FieldDescription>
                    </div>
                  </Field>
                </FieldLabel>
              </RadioGroup>
            </FieldSet>

            <Field orientation="horizontal" className="md:col-span-2">
              <Switch id="dev-switch" checked={notify} onCheckedChange={setNotify} />
              <Label htmlFor="dev-switch">Email me when a proposal needs review</Label>
            </Field>
          </div>
        </Section>

        <Section title="Card" description="Header with an action, content, footer.">
          <div className="grid gap-4 md:grid-cols-3">
            <Card>
              <CardHeader>
                <CardDescription>Settled this month</CardDescription>
                <CardTitle className="font-mono text-2xl tabular-nums">£48,210.00</CardTitle>
                <CardAction>
                  <Badge variant="success">+12.4%</Badge>
                </CardAction>
              </CardHeader>
              <CardFooter className="text-sm text-muted-foreground">Compared with August</CardFooter>
            </Card>
            <Card className="md:col-span-2">
              <CardHeader>
                <CardTitle>Invoice INV-2041</CardTitle>
                <CardDescription>Issued to Mensah Logistics on 12 September</CardDescription>
                <CardAction>
                  <Button variant="outline" size="sm">
                    Open <ArrowUpRightIcon />
                  </Button>
                </CardAction>
              </CardHeader>
              <CardContent className="grid grid-cols-[minmax(8rem,auto)_1fr] gap-x-6 gap-y-2 text-sm">
                <span className="text-muted-foreground">Amount</span>
                <span className="font-mono tabular-nums">£1,250.00</span>
                <span className="text-muted-foreground">Due</span>
                <span>26 September 2026</span>
              </CardContent>
            </Card>
          </div>
        </Section>

        <Section title="Tabs" description="Pill tabs for in-card views, line tabs for page sections.">
          <div className="grid gap-8 md:grid-cols-2">
            <Tabs defaultValue="all">
              <TabsList>
                <TabsTrigger value="all">All</TabsTrigger>
                <TabsTrigger value="open">Open</TabsTrigger>
                <TabsTrigger value="settled">Settled</TabsTrigger>
              </TabsList>
              <TabsContent value="all" className="text-sm text-muted-foreground">1,204 orders</TabsContent>
              <TabsContent value="open" className="text-sm text-muted-foreground">38 open orders</TabsContent>
              <TabsContent value="settled" className="text-sm text-muted-foreground">1,166 settled orders</TabsContent>
            </Tabs>
            <Tabs defaultValue="overview">
              <TabsList variant="line">
                <TabsTrigger value="overview">Overview</TabsTrigger>
                <TabsTrigger value="orders">Orders</TabsTrigger>
                <TabsTrigger value="finance">Finance</TabsTrigger>
              </TabsList>
              <TabsContent value="overview" className="text-sm text-muted-foreground">Customer overview</TabsContent>
              <TabsContent value="orders" className="text-sm text-muted-foreground">Customer orders</TabsContent>
              <TabsContent value="finance" className="text-sm text-muted-foreground">Accounts and balances</TabsContent>
            </Tabs>
          </div>
        </Section>

        <Section title="Overlays" description="Dialog, alert dialog, sheet, popover, hover card, tooltip, menu, toast.">
          <Row>
            <Dialog>
              <DialogTrigger asChild>
                <Button variant="outline">Open dialog</Button>
              </DialogTrigger>
              <DialogContent>
                <DialogHeader>
                  <DialogTitle>Rename workspace</DialogTitle>
                  <DialogDescription>People in this tenant see the new name straight away.</DialogDescription>
                </DialogHeader>
                <Field>
                  <FieldLabel htmlFor="dev-ws">Workspace name</FieldLabel>
                  <Input id="dev-ws" defaultValue="Aonik Finance" />
                </Field>
                <DialogFooter>
                  <DialogClose asChild>
                    <Button variant="outline">Cancel</Button>
                  </DialogClose>
                  <Button>Save changes</Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>

            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button variant="outline">
                  <Trash2Icon /> Delete attachment
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>Delete attachment?</AlertDialogTitle>
                  <AlertDialogDescription>
                    receipt-2041.pdf will be removed from this invoice. This can't be undone.
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel>Cancel</AlertDialogCancel>
                  <AlertDialogAction variant="destructive">Delete</AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>

            <Sheet>
              <SheetTrigger asChild>
                <Button variant="outline">Open sheet</Button>
              </SheetTrigger>
              <SheetContent size="sm">
                <SheetHeader icon={<UserIcon />} title="New customer" subtitle="Add a person or business" />
                <SheetBody>
                  <Field>
                    <FieldLabel htmlFor="dev-sheet-name">Name</FieldLabel>
                    <Input id="dev-sheet-name" />
                  </Field>
                  <Field>
                    <FieldLabel htmlFor="dev-sheet-email">Email</FieldLabel>
                    <InputGroup>
                      <InputGroupAddon>
                        <MailIcon />
                      </InputGroupAddon>
                      <InputGroupInput id="dev-sheet-email" type="email" />
                    </InputGroup>
                  </Field>
                </SheetBody>
                <SheetFooter>
                  <Button variant="outline">Cancel</Button>
                  <Button>Create customer</Button>
                </SheetFooter>
              </SheetContent>
            </Sheet>

            <Popover>
              <PopoverTrigger asChild>
                <Button variant="outline">Open popover</Button>
              </PopoverTrigger>
              <PopoverContent className="flex flex-col gap-2">
                <p className="text-sm font-medium">FX quote</p>
                <p className="text-sm text-muted-foreground">
                  1 GBP = <span className="font-mono tabular-nums">2,041.50</span> NGN, valid for 30 seconds.
                </p>
              </PopoverContent>
            </Popover>

            <HoverCard>
              <HoverCardTrigger asChild>
                <Button variant="link">@finance-agent</Button>
              </HoverCardTrigger>
              <HoverCardContent className="text-sm">
                <p className="font-medium">Finance agent</p>
                <p className="text-muted-foreground">Drafts invoices and reconciles payments. Proposes; never posts.</p>
              </HoverCardContent>
            </HoverCard>

            <Tooltip>
              <TooltipTrigger asChild>
                <Button variant="outline" size="icon" aria-label="Copy order ID">
                  <CopyIcon />
                </Button>
              </TooltipTrigger>
              <TooltipContent>Copy order ID</TooltipContent>
            </Tooltip>

            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" aria-label="Order actions">
                  <MoreHorizontalIcon />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-52">
                <DropdownMenuLabel>Order ORD-10442</DropdownMenuLabel>
                <DropdownMenuItem>
                  <PencilIcon /> Edit
                  <DropdownMenuShortcut>E</DropdownMenuShortcut>
                </DropdownMenuItem>
                <DropdownMenuItem>
                  <CopyIcon /> Copy order ID
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuLabel>Columns</DropdownMenuLabel>
                {(Object.keys(columns) as Array<keyof typeof columns>).map((key) => (
                  <DropdownMenuCheckboxItem
                    key={key}
                    checked={columns[key]}
                    onCheckedChange={(checked) => setColumns((c) => ({ ...c, [key]: checked === true }))}
                    className="capitalize"
                  >
                    {key}
                  </DropdownMenuCheckboxItem>
                ))}
                <DropdownMenuSeparator />
                <DropdownMenuItem variant="destructive">
                  <Trash2Icon /> Cancel order
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>

            <Button variant="outline" onClick={() => toast.success('Invoice sent', { description: 'INV-2041 to Mensah Logistics' })}>
              Show toast
            </Button>
          </Row>
        </Section>

        <Section title="Loading and structure" description="Skeleton, spinner, separator.">
          <div className="flex flex-col gap-4">
            <div className="flex items-center gap-4">
              <Skeleton className="size-10 rounded-full" />
              <div className="flex flex-1 flex-col gap-2">
                <Skeleton className="h-4 w-1/3" />
                <Skeleton className="h-4 w-1/2" />
              </div>
            </div>
            <Separator />
            <div className="flex h-5 items-center gap-4 text-sm">
              <span className="flex items-center gap-2 text-muted-foreground">
                <Spinner /> Syncing ledger
              </span>
              <Separator orientation="vertical" />
              <span>Orders</span>
              <Separator orientation="vertical" />
              <span>Payments</span>
            </div>
          </div>
        </Section>
      </div>
    </div>
  );
}

function Section({ title, description, children }: { title: string; description: string; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-col gap-1">
        <h2 className="text-base font-semibold">{title}</h2>
        <p className="text-sm text-muted-foreground">{description}</p>
      </div>
      {children}
    </section>
  );
}

function Row({ children }: { children: ReactNode }) {
  return <div className="flex flex-wrap items-center gap-3">{children}</div>;
}
