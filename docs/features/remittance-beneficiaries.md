# Remittance beneficiaries

**Remittance → Beneficiaries** opens `/remittance/beneficiaries`. Select a sending customer to view their recipients and saved payout destinations. The screen uses the application's shadcn components, semantic light/dark tokens and the shared Beneficiaries, Bank, Mobile money and Wallet SVG icons.

Operators can search by recipient, account holder, masked identifier, currency or rail; filter verified/unverified destinations; and open a destination's details. **Add destination** creates a new recipient or adds another rail to an existing recipient for that customer. The form supports Bank, MobileMoney and Wallet, using the tenant's receiving-country and currency catalogues and configured payout routing.

**Verify & save destination** invokes the existing partner registration pipeline. Bank name enquiry remains best-effort; the account-holder name is used when enquiry is unavailable. A successful provider registration stores its reusable token, masked identifier and one-way rail fingerprint. “Verified” describes that registration state; customer KYC and transfer compliance remain separate services. Registration does not move funds.

Raw account numbers, mobile-money numbers and wallet identifiers exist transiently in the form/request and connector call. They are not stored in browser draft storage or persisted as rail fields. Switching destination type discards the other rail inputs, and closing or successfully submitting the form discards its draft. Saved lists/details never expose the provider beneficiary token or full identifier. Provider failures return a generic operator message rather than an exception that might echo raw details. Registration is explicitly retried by the operator; the existing provider registration and fingerprint upsert provide replay behavior.

## Managing existing destinations

**Send transfer** is available on each destination row and in destination details for users allowed to open the transfer editor. Unverified destinations have a disabled action. The link carries customer/destination IDs only; the editor reloads both selections from the current tenant’s APIs, fills the receiving currency, and leaves amounts, corridor and purpose for the operator. The selection survives reload. A removed or no-longer-verified destination produces an error rather than being silently selected. An existing pending confirmation takes precedence over a new beneficiary link and retains its original request/key. Opening the editor does not request a quote or execute a transfer.

- **Edit recipient notes** updates the customer's directed `Recipient` relationship. Notes apply across that recipient's destinations for this customer. The same recipient shared by another customer retains that other customer's notes. Empty text clears the notes.
- **Add another destination** registers a new rail for the same recipient. Account, bank, network, currency and verified holder details are not edited in place; a changed rail must be verified before it is eligible for a transfer.
- **Remove destination** confirms removal of that individual saved rail. It soft-deletes the account with the normal deleted-at/by audit metadata, preserving its party, relationship, other destinations and historical references. It does not cancel a confirmed transfer. Repeating removal is idempotent and cannot disclose or remove another customer's rail.

New transfers exclude removed destinations through the existing soft-delete filter. An already-confirmed order can still resolve its original provider-bound destination to resume execution: that narrow lookup explicitly checks the order's tenant and payer, including when reading the retained soft-deleted record. It cannot start a new transfer. No order, payout or ledger evidence is rewritten.

The transfer editor links to **Manage beneficiaries** with the selected customer, and offers **Refresh destinations** when returning to it. The management screen also links to **New transfer** for users allowed to open that route.

## Authorization and API

| Endpoint | Policy | Permissions |
| --- | --- | --- |
| `GET /remittance/customers/{customerPartyId}/beneficiaries` | `AdminReadPolicy` | `Payment.Read`, `Customers.Read` |
| `POST /remittance/beneficiaries/verify` | `AdminWritePolicy` | `Payment.Create`, `Customers.Write` |
| `PATCH /remittance/customers/{customerPartyId}/beneficiaries/{beneficiaryPartyId}/notes` | `AdminWritePolicy` | `Payment.Create`, `Customers.Write` |
| `DELETE /remittance/customers/{customerPartyId}/destinations/{destinationId}` | `AdminWritePolicy` | `Payment.Create`, `Customers.Write` |

Registration accepts `{ "beneficiary": { ...VerifyPayoutBeneficiaryRequest } }`; notes accept `{ "notes": "..." }` up to 1024 characters. The list wraps each masked destination and its customer-scoped notes. The server independently checks permissions and the tenant customer before reading or writing; supplied existing beneficiary parties must also belong to that tenant. Writes require both customer-write and payment-create permission, but no payment-capture permission because they move no funds.

The Remittance profile includes the screen in Money movement. The general profile includes the supporting route; other business profiles retain their existing menus. Route visibility still intersects modules, policies and read permissions. Read-only staff can inspect the list/details without seeing mutation controls. The existing customer-owned verify endpoint retains its UserParty ownership check; operators use the separate staff entry point without impersonation.

This extends the existing Finance service and Platform party seam. Relationship responses now include their existing notes as an optional DTO field. No entity schema change, migration, new AI tool or change to AI mutation approval is involved.

## Validation and captures

Service coverage includes all three rails, masked responses, idempotent registration, permission refusal before connector calls, foreign tenant/customer/recipient isolation, customer-specific notes, audited individual removal, and transfer recovery after removal. API tests cover staff audience, read/write permissions and invalid registration/notes. UI tests cover rail-specific payloads, transient input resets, limits, transport context and profile/module filtering.

With the Admin UI running on port 4175 using `VITE_AUTH_PROVIDER=mock`, run from the repository root:

```powershell
node src/Aonik.AdminUi/scripts/verify-beneficiaries.mjs
```

The script intercepts API calls with fictional fixtures and verifies listing, search/filtering, form rails, manual retry, note readback, confirmed removal, tenant headers and read-only access. It writes four 3200 × 2560 screenshots and `beneficiaries-report.json` under `Templates/aonik-admin-starterkit/marketing/screenshots/`. These are captures of the actual React UI using fictional data; no live provider registration or transfer is executed.
