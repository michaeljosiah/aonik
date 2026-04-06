# Dashboard Metrics — Calculation Reference

> Endpoint: `GET /personal-finance/dashboard`
> Service: `DashboardService` (`Aonik.Finance`)

This document explains how every metric in the Payabo dashboard BFF response is
calculated. All computations run server-side so the mobile client receives
pre-formatted display strings alongside raw numeric values.

---

## 1. Data Sources

| Query | Scope | Filter |
|---|---|---|
| **Active Accounts** | `PersonalAccounts` for tenant + user | `!IsArchived` |
| **Upcoming Bills** | `Bills` for tenant + user | `Status == "Active"`, `NextDueDate` within 30 days |
| **Month Transactions** | `PersonalTransactions` for tenant + user | `OccurredAt` in current calendar month (UTC) |
| **Recent Orders** | Orders where user is `Payer` | Latest 5, resolved via `PersonalProfile.PartyId` → `OrderPartyRole` |

All four queries run in parallel via `Task.WhenAll`.

---

## 2. Primary Currency

The user's **primary currency** is determined by the most frequently occurring
currency across their active accounts. Falls back to `GBP` when the user has
no accounts.

---

## 3. Net Worth

```
Total Assets      = Σ CurrentBalance  where AccountType ∈ AssetTypes
Total Liabilities = Σ |CurrentBalance| where AccountType ∈ LiabilityTypes
Net Worth         = Total Assets − Total Liabilities
```

### Asset types
Checking, Savings, Investment, Brokerage, Retirement, Cash, MoneyMarket, CD, Prepaid

### Liability types
CreditCard, Loan, Mortgage, LineOfCredit, StudentLoan, AutoLoan

Accounts whose `AccountType` does not match either set are treated as **assets**
(conservative default).

---

## 4. Net Worth Change & Trend

For V1 the month-over-month net-worth change is approximated by net income:

```
Monthly Income  = Σ |Amount| where TransactionType == "Income"  (or Amount > 0)
Monthly Expenses = Σ |Amount| where TransactionType == "Expense" (or Amount < 0)
Net Change       = Monthly Income − Monthly Expenses
Trend %          = (Net Change / Net Worth) × 100   (guard: 0% when Net Worth == 0)
Trend Direction  = "up" when Net Change ≥ 0, otherwise "down"
```

A future version will store monthly balance snapshots for accurate month-over-month delta.

---

## 5. Available to Spend

```
Upcoming Bills Total = Σ ExpectedAmount for active bills due within 30 days
Available to Spend   = max(0, Monthly Income − Monthly Expenses − Upcoming Bills Total)
```

This represents the user's remaining disposable income after what has been spent
this month and what is still committed to upcoming bills.

---

## 6. Spendable Progress

```
Spendable Progress = clamp(0, 1, Available to Spend / Monthly Income)
Progress Label     = round(Spendable Progress × 100) + "% free"
```

- `1.0` = nothing spent yet (full income remaining)
- `0.0` = all income consumed or committed

When Monthly Income is zero, progress defaults to `0.0`.

---

## 7. Spendable Subtitle

A human-readable explanation:

- `"After £X spent and £Y in upcoming bills this month."`
- `"After £X spent this month."` (when no upcoming bills)
- `"After £Y in upcoming bills this month."` (when no spending recorded)
- `"No spending recorded this month yet."` (when both are zero)

---

## 8. Overview Donut (Income / Expenses / Investments)

Transactions are classified into three slices:

| Slice | Filter | Color Key |
|---|---|---|
| **Income** | `TransactionType == "Income"` (or `Amount > 0`) | `success` |
| **Expenses** | `TransactionType == "Expense"` (or `Amount < 0`) minus investment categories | `primary` |
| **Investments** | Expense transactions where category contains "Investment", "Retirement", or "Brokerage" | `info` |

Investment transactions are subtracted from the Expenses slice to avoid
double-counting. Slices with zero amounts are omitted.

---

## 9. Upcoming Bills

Up to **10** active bills with `NextDueDate` within the next 30 days, ordered by
due date ascending. Each bill includes:

- Payee name
- Expected amount (raw + formatted)
- Currency
- Due date (raw + formatted as "d MMM")

---

## 10. Recent Orders

Up to **5** orders where the authenticated user is the `Payer`, ordered by
`CreatedAt` descending. Beneficiary names are resolved by finding the
`Receiver` or `Payee` party role and looking up `PartyReadModel.DisplayName`.

Each order includes:

- Beneficiary name
- Amount (raw + formatted)
- Order type
- Status
- Date (formatted as "d MMM")

---

## 11. Currency Formatting

| Format | Example | Usage |
|---|---|---|
| Standard | `£1,285.00` | Spendable, net worth, bill amounts |
| Signed | `+£620.00` / `-£620.00` | Net worth change |
| Compact | `£20.1k`, `£1.7m` | Assets label, bills label |
| Date | `18 Mar` | Bill due dates, order dates |

Supported currency symbols: GBP (£), USD ($), EUR (€), NGN (₦), GHS, KES, ZAR (R), CAD (CA$), AUD (A$).
Unknown currencies fall back to the ISO code prefix (e.g., `XOF 1,000.00`).
