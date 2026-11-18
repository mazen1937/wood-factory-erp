# Monthly sales/finance workspace — 18 November 2026

The first finance tab is **دفتر الشهر**: month selector, headline totals, one searchable/filterable activity table, source/order drill-down, reconciliation exceptions and full-month CSV export. Existing operational forms expand one at a time. All-period balances remain in a collapsed section and are not shown as selected-month figures.

## Source-driven read model

The month is one read-only PostgreSQL REPEATABLE READ transaction with current finance-role authorization. NUMERIC values cross the API as decimal strings. The desktop uses decimal formatting and renders 75 rows per page from the complete month result. Search covers all loaded rows; filtering never changes month totals. CSV exports all pages and the summary. Drafts and pending outbox entries are excluded.

Activities cover customer/job orders, inventory movements, production cost attribution, execution/quality/waste events, financial documents, worker creation and finance configuration. Operational/document dates determine the month; master/configuration creation uses its recorded date. A stock issue, its cost attribution and its payable have separate effects. Their mixed displayed source amounts must not be added as one financial total.

Relationships use IDs, source-document ancestry, wage source links, grouped invoice links and payment allocations. Recursive document/order pairs are deduplicated; monetary totals aggregate source documents directly. A grouped invoice can therefore appear in several order histories without extra revenue. Equal names/amounts are never used to guess duplicates.

Order drill-down shows full history across months: identity/customer, declared requirements, child jobs/costs/history, financial documents, all activity and clickable attachments. Requirements are distinguished from actual stock issues. Source details expose recorded quantities/rates, cost treatment and original references.

## Meaning of the figures

| Figure | Treatment |
| --- | --- |
| Net sales, discounts, sales tax, delivery charges, COGS | Posted sales minus returns, in each document's own month. Grouped invoices/deliveries/payments do not create revenue. |
| Gross profit/margin | Net sales minus COGS; margin only for positive net sales. Tax excluded. |
| Direct manufacturing | Direct work costs, wages and packaging, once per cost source. |
| Industrial costs incurred | Direct manufacturing + pooled wages + industrial expense documents minus reversals. Includes running production; never a second deduction from gross profit. |
| Materials consumed | Issues minus original-cost returns plus direct consumable materials; separate from manufacturing overhead. |
| Period expenses | Expense documents minus their reversals. Supplier invoices/wage payables link existing sources without a second production charge. |
| External cash received/paid | Treasury legs excluding transfers/their reversals and opening entries. Opening entries shown separately. |
| Opening/closing treasury | Ledger balance before month / before next month. Internal transfers net to zero. |
| AR/AP at month end | Obligations minus credits/allocations posted before the next month. Later activity does not rewrite earlier as-of balances. |
| Unallocated advances | Settlements minus dated allocations, refunds and reversals. Displayed separately from specific debts. |
| Received/purchased stock | Acquisition activity, not automatically current-month expense/COGS. |
| Pending source liabilities | Selected-month receipts/wages not linked to a supplier invoice/wage payable by month end are explicit. |

The arithmetic result before closing is gross profit + delivery charges − recorded period expenses. It is not approved net profit. Manufacturing allocation, finished-goods valuation/transfer, formal WIP roll-forward and period close remain the later costing milestone. No provisional manufactured stock/zero-cost sale path is introduced. Sales tax is a recorded output-tax amount, not a statutory return or net tax payable. No tax rate is hardcoded.

## Integrity review/corrections

- Raw receipt, production issue and return quantities enforce item unit precision centrally.
- Source-based returns/deliveries/refunds/reversals inherit the original order. Conflicting attribution is rejected. Retry hashes/audit keep the original request.
- Cost reconciliation compares each material/labor/wage/packaging/work source to its cost-ledger entry: amount/sign, job, item, treatment and date.
- Stock-to-job reconciliation compares physical movements to production postings: quantity, cost, item, warehouse, date and movement kind; missing postings are flagged.
- Current stock balances/valuation/item-wide quantity are compared to the ledger, clearly separate from historical month activity.
- Reports do not rewrite history or guess corrections. Exceptions require investigation.
- Month changes clear old figures immediately; async responses are guarded by month version/user. Cache state/snapshot time are visible. Sign-out clears monthly/manufacturing reports and open drill-down windows.

## Activation and verification

No migration beyond **016** is added. Rebuild/restart API and desktop; SQL alone does not update an existing Node process.

```powershell
npm.cmd --prefix apps/api run build
dotnet build apps/desktop/WoodFactoryErp.Desktop/WoodFactoryErp.Desktop.csproj --no-restore -o apps/desktop/WoodFactoryErp.Desktop/bin/MonthlyFinanceVerification
node --test tests/*.test.cjs
dotnet run --project tests/FinanceUiSmoke/FinanceUiSmoke.csproj --no-restore -p:DesktopVerificationOutput=MonthlyFinanceVerification
```

Focused tests cover date/permission checks, exact strings, atomic read snapshots, rollback/release, missing orders, original-order attribution and stock unit precision. The Avalonia runtime test covers month-first navigation, totals, complete paging/search, stable totals during filtering, collapsed order trees and month isolation.

Verification completed locally: API and desktop builds pass; 144 Node regressions pass and four real PostgreSQL suites are explicitly skipped. The finance runtime/draft/PDF checks and monthly UI checks pass. Export captures one immutable month snapshot before file selection so a background refresh cannot mix rows and totals during writing.

The opt-in `tests/monthly-finance-postgres.test.cjs` uses `MONTHLY_FINANCE_TEST_DATABASE_URL` pointing only at a disposable database. It migrates an isolated random schema, posts real inventory/production/finance workflows, verifies January totals, February returns/expense and transfer reversals, order drill-down and deliberate source/balance corruption detection, then removes its schema. It skips without that URL. Real PostgreSQL/isolation/performance and connected acceptance remain unverified here; simulated-client/UI checks are not proof that all ERP data is error-free.
