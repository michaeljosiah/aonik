# Simplified Food Business design workspace

The starter kit now presents all twenty-four Commerce, Make and Storefront artboards through a shared Food Business workspace, with an additional packing screen and sixteen connected customer, finance and sales views. Open `Templates/aonik-admin-starterkit/food-business.html` for the forty-one editable views, or `marketing/food-business.html` for the focused review gallery and three original/revised comparisons.

This is a design revision with fictional local interactions. It changes the editable canvas and marketing assets. It does not change live application routes, installed business profiles, APIs, permissions, agent tools or the database. Existing detailed Commerce screen files remain as implementation/specification references.

## What was simplified

- Eight primary destinations organize the Food Business pack inside AONIK: **Overview, Customers, Orders, Kitchen, Stock & purchasing, Finance, Shop, Reports**.
- One primary page action. Contextual tabs reveal only the current area's views. Stock and menu subviews use small local switches.
- Metric summaries appear only where they help, with at most three values. Tables give the next decision priority over exhaustive record metadata.
- Short business labels replace specification vocabulary in page headings. Stock impact, review consequences and price rules appear beside the relevant action.
- Selected-record drawers hold variants/pricing, cost/reorder history, reservations, supplier details and review forms. The batch view links to all batches and new planned snapshot drafts.
- AONIK's shared portrait appears in a compact daily brief. Suggestions lead to review, rather than executing stock or financial actions.
- A single fictional Abby's Table workspace uses GBP throughout. A separate NGN supplier example demonstrates the currency comparison guard without assuming an FX rate.

## Navigation and retained artboards

| Area | Revised view | Retained artboard ID |
| --- | --- | --- |
| Overview | Business overview | `commerce-overview` |
| Orders | Customer orders | `commerce-orders` |
| Orders | Packing | `food-packing` (new) |
| Orders | Open baskets | `commerce-carts` |
| Kitchen | Kitchen plan | `cm-prod-sheet` |
| Kitchen | Ingredients needed | `cm-prep-list` |
| Kitchen | Kitchen batches | `cm-prod-orders` |
| Kitchen | Saved kitchen sheet | `cm-kitchen-sheet` |
| Kitchen | Recipes | `cm-recipes` |
| Stock & purchasing | Ingredients | `cm-ingredients` |
| Stock & purchasing | Finished dishes | `commerce-inventory` |
| Stock & purchasing | Low stock | `cm-lowstock` |
| Stock & purchasing | Purchase orders | `cm-pos` |
| Stock & purchasing | Receive delivery | `cm-receipt` |
| Stock & purchasing | Suppliers | `cm-suppliers` |
| Shop | Menu dishes | `commerce-products` |
| Shop | Categories | `commerce-categories` |
| Shop | Customer choices | `cs-options` |
| Shop | Food labels | `cs-content` |
| Shop | Meal box sizes | `cs-boxplan` |
| Shop | Homepage collections | `cs-merch` |
| Shop | Discounts | `commerce-discounts` |
| Shop | Delivery schedule | `cs-delivery` |
| Shop | Shop settings | `cs-config` |
| Reports | Dish margins | `cm-margin` |
| Customers | Customer list | `food-customers` (new) |
| Customers | Customer overview | `food-customer` (new) |
| Customers | Customer orders | `food-customer-orders` (new) |
| Customers | Customer commerce | `food-customer-commerce` (new) |
| Customers | Customer finance | `food-customer-finance` (new) |
| Customers | Customer insights | `food-customer-insights` (new) |
| Customers | Customer documents | `food-customer-documents` (new) |
| Customers | Customer activity | `food-customer-activity` (new) |
| Orders | Order details | `food-order-detail` (new) |
| Finance | Invoices | `food-invoices` (new) |
| Finance | Invoice details | `food-invoice` (new) |
| Finance | Payments | `food-payments` (new) |
| Finance | Payment details | `food-payment` (new) |
| Finance | Credit notes | `food-credit-notes` (new) |
| Finance | Accounting evidence | `food-accounting` (new) |
| Reports | Sales report | `food-sales` (new) |


The main canvas keeps its three existing Commerce section IDs while giving them Food Business titles. Deep links/focus targets use the retained artboard IDs. Other starter-kit areas, including the new support designs, remain available.

## One platform and the customer journey

The sidebar identifies AONIK as the product and Abby’s Table as the selected tenant. Global record search, notifications, chat and workspace settings remain in the shared shell. The Shop area controls customer-facing merchandising; it is not a second administration product.

The owner sees eight destinations. The kitchen-role preview shows Overview, Orders, Kitchen and Stock & purchasing, defaults to the kitchen plan, and omits owner search/notifications and customer/finance destinations. This is a visual local role preview, not an authorization implementation; the live registry, tenant permissions and server policies remain authoritative.

Open Nadia Mensah (CUS-1001) → ORD-2048 → INV-1048 → PAY-3048 → balanced ledger evidence. Back-links preserve the selected customer/order/invoice/payment. Order detail links into packing for the same order; unpaid Alex Morgan / ORD-2045 / INV-1045 has no successful payment and remains ineligible for packing. Search and notification links open their specific records.

The 3 November sales fixture has five orders/invoices totaling £180, four successful allocated payments totaling £156, and £24 outstanding, due 5 November. Report filters recalculate totals and CSV export uses the same displayed rows. Ordered sales represent intent, not recognized revenue; accounting revenue comes from the ledger. Dish margins uses a separately labelled historical period and fixture.

Credit note CN-0048 is an unposted £9.50 draft against INV-1048. Reviewing it neither issues a credit nor refunds money. Accounting previews show balanced immutable-posting references, not editable financial totals. Customer contact review displays fictional saved details; actual contact edits, invoice issue, credit issuance, payment capture/refund and full accounting tools remain implementation work using shared domain services and approvals.

## Customer detail parity

`customer-detail.jsx` (corporate and individual designs), the production `CustomerDetailPage.tsx`, its `commerce/CommerceTab.tsx` and `lib/visibleTabs.ts` are the references for the expanded customer view. The food design keeps one customer identity across seven domain tabs:

- **Overview:** contact/profile, delivery/billing addresses, transactional preferences, separate marketing consent, recent activity and local staff notes. Note creation is attributed and appears in that customer's activity.
- **Orders:** customer-scoped order references, type, saved selection, totals and separate payment/preparation statuses.
- **Commerce:** shop relationship, guest/account status, recorded shop identity and box/dish history from the same order fixtures.
- **Finance:** invoices, successful payments and draft credit notes, linked into the shared record detail views. Draft credits do not change balances or refund payments.
- **Insights:** illustrative, source-referenced summaries; one purchase does not justify a buying trend. No live AI run is claimed.
- **Documents:** purpose, access scope and content preview for a fictional delivery note. No file transfer or upload occurs.
- **Activity:** customer, order, invoice/payment and note events with actors, UTC times, filters and record links.

Every tab has a matching native artboard. Tabs and record links retain customer scope; notes are stored locally by customer ID and cannot leak into another customer's history. Tab keyboard navigation supports arrows and Home/End. Email verification is shop-account state, not KYC verification. The Commerce tab follows the current production constraint: recorded facts only; no invented guest/cart adoption timeline or inferred buying trend.

This is a prospective food profile that illustrates Finance, Commerce and Documents enabled. In production `visibleTabs.ts` must continue to resolve optional modules and permissions. The existing Food Commerce provisioning manifest does not itself enable all of those modules, and no provisioning changes are made here. Personal Finance sub-tabs (budgets, commitments, financial graph), Household and Beneficiaries are outside this food profile. The proposed customer Finance invoice/payment/credit lens will need integration with the shared services; the current production Finance tab hosts Personal Finance content.

## Connected review examples

The primary stock example starts with 10 kg of rice on hand and 5 kg reserved, leaving 5 kg available. The saved 40-portion kitchen batch requires 8 kg, so release is blocked for a 3 kg shortfall. The buying review rounds this to one 5 kg pack. A foreign-currency supplier cannot be used as though its price were GBP.

Creating a purchase draft does not change stock. Submitting it moves Draft to Pending. Receiving its 5 kg delivery moves rice on hand to 15 kg and available to 10 kg. The reviewed batch release consumes its saved 8 kg of rice, alongside its other ingredients, leaving 7 kg on hand and 2 kg available. Cancelling a released batch does not automatically restock it.

Purchase orders retain Draft, Pending, Complete and Cancelled. Partial receipt is displayed as progress, not a new status. The receiving form rejects quantities above what remains due. A receipt request slot applies once; starting another physical receipt is an explicit action. These are local illustrations of the existing server's durable idempotency/payload-hash contract, not a replacement for it.

Kitchen batches follow Planned → Released → In progress → Completed. The kitchen sheet replays saved quantities. Changing the live Jollof recipe's rice quantity leaves the existing 8 kg batch snapshot unchanged. New planned drafts save their own quantities. The interactive release/complete/cancel example is KT-1103-A; other batch records illustrate list/detail and snapshot review. Completion records actual portions and adds finished yield only if explicitly selected. Incomplete recipe authoring remains outside the kitchen plan.

The finished-stock count review requires a reason and cannot undermine reservations. The packing example keeps an unpaid order on hold; eligible orders need content/label checks and explicit readiness confirmation. Live fulfilment policy and permissions remain server-owned.

## Shop and reporting constraints

- Food labels are exact-authored or withheld. Ingredients, allergens and heating text are never copied from another preparation. The example missing Beef label begins empty, needs authored fields and a review confirmation, and publishes for that choice only. Changing the recommended default flags affected labels for review. Illustrative nutrition fallback is labelled standard preparation.
- Customer choice prices are signed differences from the recommended default. Existing orders retain their saved choices.
- Box presets override the formula at their exact sizes. The default example costs £49 at seven dishes and £54 at eight, so growing from seven to eight costs £5. Saving prices validates a non-negative, non-decreasing illustrated price curve. Savings copy is authored separately. Boxes have no independent stock; the expanded component example distinguishes actual selections from a general availability promise.
- Delivery after Tuesday's 18:00 UTC cutoff rolls to the next supported Friday. A blackout moves the promise forward. Disabling the schedule shows no date. The date selector explores a mock order time, not the production clock or an alternate source of delivery truth.
- Unknown costs stay visible and outside the aggregate margin denominator. The initial report has £990 of costed sales and £634.80 gross profit; £96 of prawn sales is excluded. Recording an ingredient price of £24/kg produces a £4.60 prawn-dish standard cost from 0.15 kg plus £1.00 of other known components. The report then shows £1,086 costed sales and £694 gross profit. This illustrates current standard-cost rollup, consistent with the reporting service; it does not post or rewrite ledger entries.
- Financial review stays separate from order preparation. No mock payment capture, refund, payout or ledger posting is performed. Any future agent-invoked mutation must use the existing server risk/approval gate and AiRun audit path.

All people, orders, amounts, recipes, label text, stock levels and deadlines are fictional design data. Food-label examples are not approved production labels. Local forms demonstrate representative interactions; complete backend validation, authorization, historical costs, partner transmission and persistence remain with the existing domain services.

## Source and exports

- Scene inventory and mock data: `screens/food-business-data.js`.
- Shared views: `screens/food-business-views.jsx`.
- Workspace, drawers and local review state: `screens/food-business.jsx`.
- Scoped semantic styles: `design-system/food-business.css`.
- Focused editable canvas: `food-business.html`.
- Dedicated review gallery: `marketing/food-business.html`.
- Original screen references: `screens/commerce-*.jsx`, Specs 042 and 050–072, and the owning Commerce services.

Run from the repository root:

```powershell
node Templates/aonik-admin-starterkit/scripts/verify-food-business.mjs
node Templates/aonik-admin-starterkit/scripts/build-food-review.mjs
```

The browser check covers all forty-one clean layouts in light/dark, eight primary menu areas, one primary page action, local search, buying/receiving/release, saved recipes, label review, box pricing, delivery promises, unknown-cost margins, packing and stock counts. It focuses all forty-one artboards in the standalone canvas and checks the main canvas's retained production artboard ID. No application API calls or runtime exceptions are allowed.

Exports include eighty-two clean scene PNGs, twenty-two focused-canvas previews and two blocked-release previews. Three original canvas captures are retained for review. The machine-readable results are `marketing/screenshots/food-business-design-report.json`. Clean product views are 3200 × 2200; focused canvas previews are 3200 × 2560.

A direct preview is `food-business.html?capture=1&scene=today&theme=light`. Every scene ID in `FOOD_SCENES` is supported, with `theme=dark` as well. Canvas focus, ordering and editable labels retain the existing host behavior.
