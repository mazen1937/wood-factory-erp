# Sprint 9 — Sales and integrated finance

Prepared on 17 November 2026. This is a local implementation awaiting real PostgreSQL migration and connected factory acceptance. It is not a zero-error guarantee or approval to use unreconciled financial data in production.

## Delivered workflow

The main menu's **المبيعات والمالية** opens eight tabs. Read-only tables have column headings, reference/party search, scrollable rows and explicit totals. Amounts are in EGP.

| Tab | Work performed |
| --- | --- |
| المبيعات | Search finished products and warehouses; review quantity, unit price, line discount, approved separate tax and total before posting. See available finished stock and cost. Optional customer-order link. |
| العملاء والتحصيل | Customer advances/collections, immediate settlement of a selected debt, subsequent allocation across debts, and refund of unused credit. Optional order reference on advances. |
| الموردون والأجور والمصاريف | Link existing inventory receipts to supplier obligations without receiving inventory again; recognize a production wage payable from its earned source without adding WIP cost again; record unpaid industrial/admin/delivery expenses; settle supplier/worker debt; acquire externally purchased finished goods. |
| الفواتير والتسليم والتصحيح | Group a customer's posted sales into one internal invoice, record partial deliveries without a second stock issue, return delivered quantities, cancel undelivered quantities, and reverse supported financial documents with a reason. |
| الخزينة والبنوك | Cash/bank accounts, separately approved opening balances, paired transfers and movement history. No negative treasury balances. |
| الجداول والمراجعة | Document/line registers, customer/supplier/worker debt, payments/advances, unlinked source records and reconciliation exceptions. CSV and Arabic internal-document PDF export. |
| إعدادات معتمدة | Manager-approved accounts and effective-dated tax/exemption policies. There is no default tax rate. |
| طلبات الحفظ | Durable uncertain/rejected requests, their reasons, unchanged retries and cancellation of rejected local drafts. |

Every posted document has a UUID, operation ID, unique internal serial, business reference, actor, effective date, original request and historical values. Internal serials may have gaps after rollback; they are **not a statutory invoice sequence**. References are normalized for surrounding spaces and case. Payment references cannot be reused for another party, and supplier obligation references cannot be duplicated by choosing another payable workflow.

## Accounting boundaries and formulas

- **Sale:** stock deduction + COGS + customer obligation, in one transaction. Collection and invoice generation do not create a second sale. Each sale is allocated to at most one internal invoice. A sale may be invoiced individually or grouped with the same customer's other sales, including month-end grouping.
- **Invoice:** a linked internal document after posted sales. It creates no stock movement, revenue or second obligation. This implements a non-statutory pilot workflow while legal invoice timing/numbering remain open.
- **Customer collection:** treasury receipt + a payment/advance record. Selecting an existing debt also allocates it atomically; otherwise it remains visibly unallocated. One payment can be allocated across several debts through subsequent allocation documents.
- **Supplier/worker payment:** treasury outflow + settlement. Selecting a debt allocates immediately. No second purchase, wage or expense is recognized. Legacy supplier receipts are not silently treated as paid or inserted into payable balances: they appear as unlinked sources for explicit invoice matching.
- **Return/cancellation:** linked original-sale quantities and amounts. Delivered units may be returned; only undelivered units may be cancelled. Restore original COGS into the original warehouse, reverse the original net/tax, and credit the debt. Excess allocations are released to reusable customer credit; a cash refund is a separate document. Requested delivery-charge credits cannot exceed the original remaining charge.
- **Expenses:** industrial/admin/actual customer-delivery expenses create unpaid obligations. The customer's delivery charge remains distinct from actual delivery expense. Supplier/worker settlements never add WIP costs again. Production packaging/consumable supplier-liability matching remains a controlled extension; do not re-enter their direct WIP costs as industrial-pool expenses.
- **Transfer:** two equal/opposite treasury legs; no income or expense.
- **Reversal:** supported for collections, supplier payments, expenses and transfers. Requires a manager and reason. It restores treasury/allocations/obligations as applicable. Paid expenses require payment reversal first; partially refunded collections cannot be reversed again. Sales use the linked return/cancellation flow. Stock purchases, supplier-source matching, tax and account configuration do not have destructive edit controls.

Net sales = quantity × price rounded to four decimals − line discount. Tax is calculated separately from discounted net using the selected effective policy. Header values sum the rounded lines. Gross profit = net sales − COGS. Margin = gross profit / net sales × 100 when net sales is positive; otherwise it is undefined. Delivery charges are reported separately from product margin. This is gross profit, not a claim to full net profit after industrial/admin expenses.

The financial API accepts decimal strings, not JSON numeric money. BigInt fixed-point arithmetic and PostgreSQL NUMERIC preserve four decimal places; half values round away from zero. Returns use the difference between cumulative rounded entitlement and prior credits, preventing repeated partial returns from accumulating rounding gains/losses. Full stock depletion consumes the remaining recorded value. Monetary limits and NUMERIC constraints reject overflow and roll back the transaction.

Cash, bank, receivables and payables derive from their source ledgers/allocations; users cannot type over a balance. Reports use one repeatable-read database snapshot. Totals use all posted rows; the document and treasury registers currently display the newest 1,000 rows and label this limit. Other source/line/balance tables are unpaged in this pilot and need pagination before large-volume rollout.

## Costing and legal gates

Manufactured goods are **not** promoted from `pending_costing` to sellable stock by this sprint. The preceding allocation/WIP/finished-goods transfer milestone remains incomplete. The sales service only consumes real finished-goods warehouse stock; entering an order or accepting QC creates none. Purchased finished goods can be received at acquisition cost and then sold. Never use a fictitious purchase to bypass manufactured costing.

Monthly industrial allocation, approved completion-cost attribution, formal WIP close snapshots and accountant-approved period close/reopening still belong to the costing/close milestone. There is no close button that pretends these reconciliations have occurred. Existing closed months are enforced by financial and inventory posting; a historical correction posts in an open month and retains its source link. No unrestricted closed-period edit is exposed.

Tax policies require a rate, effective date and recorded basis of approval, including explicit zero/exemption. A newer effective version does not rewrite stored sales. This module does not claim Egyptian statutory/e-invoice compliance, a legal invoice format, tax-return reporting or a formal double-entry general ledger. The reference contracts leave those decisions open.

## Transaction and retry controls

Financial posts are deliberately serialized using a PostgreSQL transaction advisory lock at the initial factory scale. Shared stock item/balance locks protect against concurrent production/inventory posting. Header, source links, stock, COGS, debt, allocations, treasury legs and durable acknowledgment either all commit or all roll back. Unique indexes and deferred commit-time reconciliation triggers supplement application validation. Posted financial tables reject UPDATE/DELETE; corrections append source-linked records.

An operation namespace registry prevents an unrelated inventory/production/financial transaction from reusing the same operation ID. The intentional shared identity between a production issue movement and its execution event remains valid. Exact request replay returns the committed result; changed payload/actor is rejected. Current database permissions are checked instead of trusting old token roles.

The desktop uses a separate `finance.sqlite` store in the existing local application-data folder. Forms, line drafts, queue payloads and acknowledgments are retained by signed-in user. Timeouts preserve the original operation ID; transient retries use backoff. A rejected request stops later sends until reviewed. Cancelling a rejected local draft does not reverse any official posting. An uncertain pending request cannot be abandoned through this UI. Signing out clears visible financial data. Cached reports are labeled stale and are not proof of current stock/cash. Back up SQLite with the app closed, including any journal files if applicable.

## Installation

1. Close the desktop and stop the API. Take and verify a fresh database backup.
2. Apply all preceding migrations through **012**. Do not reset the database or delete Docker volumes.
3. From the repository root, apply **013**:

```powershell
Set-Location "C:\Users\HP\source\wood-factory-erp"
docker cp .\database\postgres\migrations\013_sales_finance.sql wood-factory-erp-postgres:/tmp/013.sql
if ($LASTEXITCODE -ne 0) { throw "Could not copy migration 013." }
docker exec wood-factory-erp-postgres sh -lc 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /tmp/013.sql'
if ($LASTEXITCODE -ne 0) { throw "Migration 013 failed. Stop and inspect the error." }
```

Migration 013 is transactional. If it detects old cross-domain operation-ID collisions, reconcile those records before retrying; never delete historical rows to silence it.

4. Build/start the API in one window, then build/start the desktop in another:

```powershell
Set-Location "C:\Users\HP\source\wood-factory-erp\apps\api"
npm.cmd run build
if ($LASTEXITCODE -ne 0) { throw "API build failed." }
npm.cmd run start
```

```powershell
Set-Location "C:\Users\HP\source\wood-factory-erp\apps\desktop\WoodFactoryErp.Desktop"
dotnet run
```

5. Existing active `system_admin` accounts can use the module. New `finance_viewer`, `finance_operator` and `finance_manager` roles are created but are not automatically assigned. Operator permits posting; manager also configures settings/openings/reversals. Viewer is read-only on the API. UI buttons are convenience controls; the server remains authoritative.
6. Define actual cash/bank accounts, approved opening balances and approved tax/exemption policies. Do not create artificial opening balances or stock to make a test pass.

## Verification and review

Latest local result: API and desktop builds pass (desktop: 23 existing warnings, zero errors); **97 automated tests pass**, with the real PostgreSQL suite explicitly **skipped**. Finance runtime UI/draft/PDF and production SQLite persistence checks also pass. These results do not validate migration 013 against a running PostgreSQL server.

```powershell
# From the repository root, after API build:
node --test tests/complete-orders.test.cjs tests/order-job-sheets.test.cjs tests/production-execution.test.cjs tests/finance.test.cjs tests/auth-current-permissions.test.cjs
dotnet run --project tests/ProductionOutboxSmoke/ProductionOutboxSmoke.csproj
dotnet build apps/desktop/WoodFactoryErp.Desktop/WoodFactoryErp.Desktop.csproj -o apps/desktop/WoodFactoryErp.Desktop/bin/FinanceVerification
dotnet run --project tests/FinanceUiSmoke/FinanceUiSmoke.csproj -p:NuGetAudit=false
```

The service tests simulate database responses; they verify arithmetic, validation, transaction orchestration and rollback/replay branches, not SQL execution. The UI test constructs the actual Avalonia forms without a server, checks searchable selectors/inline errors, saves/restores account-scoped drafts with real SQLite, and generates a real Arabic PDF.

`tests/finance-postgres.test.cjs` is an opt-in real PostgreSQL test. Point `FINANCE_TEST_DATABASE_URL` at a **disposable test database**, then run it with `node --test tests/finance-postgres.test.cjs`. It creates an isolated random schema, applies all migrations, exercises sales/returns/collections/payments/receipts, checks immutable history and parallel posting, and removes only that schema. It never reads `.env`. Without the variable, it explicitly reports SKIP. Docker access and package/network access were unavailable here, so that acceptance test has not been executed by the agent.

Before approving use, run the PostgreSQL test and the connected desktop acceptance below, reconcile opening data, and verify backup/restore. See `FINANCIAL-INTEGRITY-REVIEW.md` for review findings and limits.

## Connected acceptance

Use an isolated test database first.

1. Cash 1,000 + bank 2,000; transfer 500 cash to bank. Expect 500/2,500 and combined 3,000. Insufficient cash must roll back both legs.
2. Receive 10 raw units at 100 and 10 at 200. Expect 20/3,000/150. Retry the first operation and verify only one receipt. Link its payable source only once.
3. Buy finished goods at 2,000. Sell one at 3,000 with 300 discount and an approved test tax policy. Expect net 2,700, COGS 2,000, profit 700 and margin 25.9259%. Tax and delivery charges stay separate.
4. Generate an internal invoice and retry. Stock, COGS and debt must not change. Try placing the same sale in another invoice: reject.
5. Receive a 4,000 customer payment against a 10,000 sale obligation. Expect debt 6,000; no second revenue. Repeat the payment reference with a different operation ID: reject.
6. Receive a 2,000 advance before a sale. Verify no revenue, then allocate it to that customer's debt. Wrong-party, over-allocation and repeated allocation beyond the remaining amount must reject.
7. Record partial deliveries, return a delivered unit and cancel an undelivered unit. Neither route may exceed eligible quantities. Verify original COGS/tax recovery, stock restoration and remaining customer credit. Refund only unused credit.
8. Link a 10,000 supplier receipt, pay 4,000 against that debt, and verify payable 6,000 and treasury minus 4,000 with no second receipt/expense. Settle an earned wage source and verify production costs do not increase again.
9. Reverse a supported transaction with a manager and reason. Original rows remain; all opposite effects reconcile. Ordinary users and unsupported reversals must reject.
10. Block a month in the isolated test setup and attempt backdated receipts/sales/payments. They must reject; replaying an already committed operation must return its original outcome. Production costing/period-close actions remain gated.
11. Disconnect during a save, restart the desktop and reconnect. Confirm one official document/effect using the same saved operation ID. Verify another user sees no previous user's forms or cached financial tables.
12. Test two concurrent users selling the last stock and spending the same available payment/cash. Only the centrally valid combination may commit. All reconciliation exception counts must be zero before approval.
