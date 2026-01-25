# Create Bill Payment Order Specification

## Purpose
Define the requirements and UI flow for creating a bill payment order that supports multiple bill pay items. The form uses four functional cards (2x2 grid) and a fifth basket card on the far right that auto-updates as items are added or edited.

## Goals
- Capture bill payment intent as Orders (not Payments), consistent with AONIK order-first architecture.
- Support one order with multiple bill pay items (cart-style behavior).
- Allow receiver selection with ad-hoc creation and relationship mapping to the payer.
- Provide real-time pricing quotes (read-only) without mutating financial state.

## Non-Goals
- Executing payments or posting ledger entries.
- Final settlement or payout orchestration.
- Defining AI agent workflows (separate spec).

## Layout Summary
- Left area: 2x2 grid of functional cards.
- Far right: basket card spanning both rows.
- Each item added to the basket becomes a bill pay line within a single order draft.

## Card 1: Biller Discovery (Top-Left)
Purpose: Choose the biller corridor and service.

Inputs
- Country/corridor selection (destination country)
- Biller category
- Biller search and selection
- Service selection

Data Sources
- Catalog categories: `GET /catalog/biller-categories`
- Billers list: `GET /catalog/billers`
- Biller services: `GET /catalog/billers/{billerId}/services`
- Service detail for required fields: `GET /catalog/billers/{billerId}/services/{serviceId}`

Outputs
- `billerId`, `serviceId`, `serviceCode`, `destinationCountry`, `destinationCurrency`

## Card 2: Customer & Account (Top-Right)
Purpose: Identify payer and biller-required account fields.

Inputs
- Payer selection (existing party/customer)
- Customer tier (optional, auto-resolve from party if empty)
- Service fields (from service detail), including account/reference numbers
- Optional validation trigger if `RequiresValidation = true`

Outputs
- `payerPartyId`
- `customerId` (optional)
- `customerTier` (optional)
- `serviceFieldValues` (key/value)

## Card 3: Amounts & Funding (Bottom-Left)
Purpose: Capture amounts and request pricing quote.

Inputs
- Amount mode toggle: origin or destination
- Amount value
- Origin currency (payer currency)
- Funding source (wallet/bank reference, if applicable)
- Quote action

Data Sources
- Pricing quote: `POST /pricing/quote` (read-only)

Outputs
- `originAmount` or `destinationAmount`
- `originCurrency`
- `pricingQuoteId`, `fxRateId`, `feesTotal`, `totalAmount`, `exchangeRate`
- `fundingSourceRef` (placeholder for PaymentIntent)

## Card 4: Receiver & Compliance (Bottom-Right)
Purpose: Capture receiver details and relationship mapping.

Inputs
- Receiver option: same as payer or different
- Receiver selection (existing party) or create new party inline
- Relationship type between payer and receiver (e.g., Mother, Spouse, Friend)
- Purpose / payout reason
- Notes / compliance metadata (freeform + structured)

Outputs
- `receiverPartyId` (existing or newly created)
- `relationshipTypeCode`
- `purposeCode`, `notes`, `provenanceJson`

## Basket Card (Far Right, Spans Both Rows)
Purpose: Shopping basket for bill pay items and order-level actions.

Behavior
- Auto-updates as any card inputs change.
- Shows item draft state, validation errors, and pricing quote status.
- Supports multiple bill pay items per order.

Contents
- Items list with:
  - Biller + service name
  - Account reference summary
  - Payer + receiver names
  - Amounts, fees, totals, exchange rate
  - Quote timestamp and policy metadata
- Order totals (sum of item totals + fees)
- Actions:
  - Add item to basket
  - Edit item
  - Remove item
  - Create order (disabled until all items valid)

## Multi-Item Order Requirements
- The form represents a single order draft containing multiple bill pay items.
- Each item carries its own pricing quote snapshot and service field values.
- Basket totals are derived from item totals; no re-quoting at basket level.
- Editing an item re-runs its quote; basket totals update instantly.

## Order Creation Requirements
- Create an `Order` with `OrderType = "BillPayment"` and status `Draft`.
- Persist item-level data in a new entity (see Data Model Changes).
- Attach `OrderPartyRole` entries:
  - `Payer`
  - `Receiver` (per item)
- Persist pricing metadata on each item and optionally aggregate at order level.
- Create `OrderHistoryEvent` entry for `OrderCreated` with actor metadata.

## Receiver Creation and Relationship Mapping
- If receiver is not an existing party, create a new Party record inline.
- Create a relationship entry linking payer to receiver with a controlled type code.
- Store the relationship reference (or code) with the item details for audit.

## Data Model Changes (Proposed)
### 1) Party Relationship
Add a Party relationship entity to support payer-to-receiver mapping.

Proposed entity
- `PartyRelationship`
  - `TenantId`
  - `FromPartyId` (payer)
  - `ToPartyId` (receiver)
  - `RelationshipTypeCode`
  - `IsActive`
  - `Notes`

Reference data
- Add `RelationshipType` reference data (Mother, Spouse, Friend, etc.).

### 2) Bill Payment Order Items
Add an order item entity to persist multiple bill pay lines per order.

Proposed entity
- `BillPaymentOrderItem`
  - `TenantId`
  - `OrderId`
  - `BillerId`
  - `ServiceId`
  - `ServiceCode`
  - `ServiceFieldsJson`
  - `PayerPartyId`
  - `ReceiverPartyId`
  - `RelationshipTypeCode`
  - `AmountIn`, `CurrencyIn`
  - `AmountOut`, `CurrencyOut`
  - `FeesJson`
  - `PricingQuoteId`
  - `FxQuoteId`
  - `Status`

### 3) Optional Order-Level Aggregates
- `Order.AmountIn` / `Order.AmountOut` can reflect aggregated item totals.
- `Order.FeesJson` can store a summarized fee breakdown across items.

## Validation Rules
- Must choose biller + service before quote.
- Exactly one of `originAmount` or `destinationAmount` per item.
- Amount must be within min/max service limits (if provided).
- Required service fields enforced based on service detail.
- Receiver is required; relationship type required if receiver differs from payer.

## UX Flow Notes
- Default progression: Card 1 → Card 2 → Card 3 → Card 4 → Add to basket.
- Basket shows draft item even before add, with validation errors highlighted.
- Create Order only when all items pass validation and have pricing quotes.

## Audit and Compliance
- Store quote metadata and policy version per item.
- Record `OrderHistoryEvent` for add/edit/remove item actions.
- Include receiver relationship type in audit details.
