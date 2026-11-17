# Production execution — 17 November 2026

## Delivered workflow

The main menu's **الإنتاج / تحت الإنتاج** opens the production module. It lists existing job orders with product/customer identity and highlights **تحت الإنتاج**, **متوقف مؤقتاً**, **مقبول — بانتظار التكلفة والترحيل**, and work not yet started. It uses existing job-order records and preserves the order form and A3 PDF.

Select a job, choose the relevant production route, and start. The available stages are **تقطيع، شريط، التجميع وتركيب الإكسسوارات، تعبئة وتغليف، الفحص والقبول**. QC is always included. Migration 012 retires drilling, painting, upholstery and the separate accessory stage from existing routes while preserving their historical progress, costs and audit records. Assembly includes accessory installation.

Each action has its own quantity input and persistent success/error feedback beside its button. Wood and accessories have separate searchable item/warehouse forms. Returns use the same separation, show only issued quantities still available to return, and identify the original warehouse. Empty return/history views explain why no records appear. Technical retry controls are hidden until there are pending or rejected saves and require a selected rejected save.

Every progress quantity is **an increment**, measured in finished-product units. Partial work can move through successive stages: a downstream stage cannot exceed the previous stage's completed quantity or the planned batch quantity. Pauses and resumes require a reason. Progress and acceptance cannot be dated earlier than the most recent physical production event; future dates are rejected. Costs may be recorded in an earlier open month, but never before production started. Existing A3 instructions are locked once a draft job enters production.

## Materials, actual waste, labor and costs

- Issues require an active stock-tracked material/accessory and an active raw-material warehouse. Both item-wide valuation and warehouse balances are locked; insufficient stock is rejected. Quantities use the item's base unit. All arithmetic on quantity/value uses PostgreSQL NUMERIC with four-decimal rounding, not JavaScript floating-point calculations.
- One issue atomically updates stock, creates the movement, links consumption to the job/stage, adds exactly one direct cost, and saves audit/idempotency information. Failure rolls back all effects. The existing inventory ledger now labels receipts, production issues and production returns separately.
- Material returns refer to an original issue from the same job, use the original warehouse and issue cost, and cannot exceed unreturned/non-wasted quantities. Final full returns use the uncredited original value residual to prevent rounding leftovers. Returns credit direct production costs once.
- Actual waste is a quantity/reason observation against an existing issue, separate from the engineer's planned waste percentage on the customer order. Waste cannot exceed the original issue less returns and previous waste. It cannot then be returned as unused material. Its cost was already captured in the issue: observation does not deduct stock or add cost again. Scrap sale/reuse/write-off valuation remains an unresolved factory/accounting policy and is not invented here.
- Managers/admins approve an agreement with worker name, job stage, unit/daily/weekly/monthly basis, rate, start date and direct/pooled treatment. Unit agreements specify assigned units. Stage progress accrues the selected worker's unit wage automatically; already-completed unpaid work can be explicitly declared at approval, bounded by completed units. Fixed-period wages accrue once after the period ends, either at approval or through the due-period selector. Overlapping fixed periods for the same worker are rejected across jobs. Agreements, earnings and cash payment are distinct; this screen records no wage payment. Legacy rate/labor records remain available and retain their historical costs.
- Packaging has an explicit total cost per order, saved once and added to direct WIP. The cost tab is read-only and explains issued materials minus returns, earned direct wages, packaging, prior costs carried forward and pooled wages awaiting allocation. The unexplained actual-amount input has been removed. The legacy consumable endpoint remains available for non-stock items; stock-tracked items cannot bypass the issue ledger. These cost records create no cash/bank payment or supplier payable.

## WIP and completion boundary

The monthly view shows opening recorded direct costs, net additions in the selected month, closing recorded direct costs and separately captured pooled wages. Earlier direct costs are carried forward from source records once, not re-entered each month. This is explicitly **recorded direct cost before industrial allocation**, not final full WIP valuation or finished-product unit cost.

The separate **الفحص والقبول** tab selects a job and records an accepted or rejected quantity. Rejection requires a reason and keeps the quantity under production for repair. Reinspection is bounded by outstanding rejected units. Acceptance is capped by upstream completed work, records the QC decision and acceptance atomically, and remains `pending_costing`. A fully accepted batch becomes `ready_for_costing`; unfinished batches remain under production, even across month end. Neither action creates sellable finished-goods stock or falsely marks the accounting transfer complete.

Roadmap milestone 6 still owns three-plank monthly allocation, approved pool/denominator/rate/residual handling, formal WIP snapshots/closing, cost attribution to partial completions, finished-goods receipts and controlled cost corrections. There is intentionally no transfer-to-stock button until those accounting rules can be enforced. Posted production records are append-only through this API; correcting a rejected local draft is different from reversing an official posting. Reversal/adjustment workflows for already-posted stage/labor/waste mistakes remain a future controlled feature; records must not be edited directly to hide a mistake.

## Authority, periods and offline behavior

Production endpoints check current active database roles, including when retrying a command. `system_admin` and `production_manager` can configure rates; `production_operator` can view and post execution. The migration creates the two production roles without assigning them to users automatically.

`accounting_periods` creates each first-used month open. Official production actions and inventory receipts reject closed months, locking the period row for the duration of posting. Closing/reopening periods and full financial reconciliation are not delivered by this screen. This production safeguard is not a claim that a complete accountant-approved period-close module exists.

The desktop stores production drafts and outbox entries atomically in `production.sqlite` under the application's local data directory. It uses the operating system SQLite library (Windows: `winsqlite3.dll`; Linux/macOS: system SQLite). A durable operation UUID and exact payload survive restarts. Server idempotency uses a persisted hash and outcome under an advisory lock; identical replay returns the original result, while a changed payload with the same identity is rejected.

Cached jobs, routes, rates, master data and job details support offline preparation for previously loaded records. The screen states when it is showing local data and displays pending/rejected counts and last successful synchronization time. Pending is never treated as official stock issue or stage approval. New unacknowledged routes cannot authorize downstream production actions. The queue is scoped to the signed-in user and synchronizes in order every ten seconds while signed in, with increasing retry delay after transient errors. A blocked command stops dependent commands. An operator may retry a rejected command unchanged or abandon that rejected local draft and enter a corrected one. Abandoned/accepted history is retained; uncertain pending requests cannot be discarded through this UI.

Sync here covers the production module, not a retrofit of the earlier order/inventory-receipt forms. Local machine backup must include the production SQLite store; do not copy only its main file while the app is writing WAL data. Close the app first or use a SQLite-aware backup procedure.

## Installation

Close the desktop app, stop the API, and take a fresh PostgreSQL backup using the established procedure. Apply migration 011 if not already installed, then apply 012. Migration 012 requires 011 and includes its own transaction. Do not reset the volume. From the repository root:

```powershell
docker cp .\database\postgres\migrations\011_production_execution.sql wood-factory-erp-postgres:/tmp/011.sql
if ($LASTEXITCODE -ne 0) { throw "Could not copy migration 011." }
docker exec wood-factory-erp-postgres sh -lc 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /tmp/011.sql'
if ($LASTEXITCODE -ne 0) { throw "Migration 011 failed." }

docker cp .\database\postgres\migrations\012_production_workflow_usability.sql wood-factory-erp-postgres:/tmp/012.sql
if ($LASTEXITCODE -ne 0) { throw "Could not copy migration 012." }

docker exec wood-factory-erp-postgres sh -lc 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /tmp/012.sql'
if ($LASTEXITCODE -ne 0) { throw "Migration 012 failed." }

Push-Location apps/api
npm.cmd run build
if ($LASTEXITCODE -ne 0) { throw "API build failed." }
npm.cmd run start
# Keep this API window open; run the desktop in another PowerShell window.
```

In the second window:

```powershell
Set-Location "C:\Users\HP\source\wood-factory-erp\apps\desktop\WoodFactoryErp.Desktop"
dotnet run
```

## Verification

```powershell
node --test tests/complete-orders.test.cjs tests/order-job-sheets.test.cjs tests/production-execution.test.cjs
dotnet run --project tests/ProductionOutboxSmoke/ProductionOutboxSmoke.csproj

# Build the DLL used by the runtime form check, without replacing a running app.
dotnet build apps/desktop/WoodFactoryErp.Desktop/WoodFactoryErp.Desktop.csproj -o apps/desktop/WoodFactoryErp.Desktop/bin/ProductionUsabilityVerification
dotnet run --project tests/ProductionUiSmoke/ProductionUiSmoke.csproj
```

All 52 API regression tests pass using simulated PostgreSQL clients, including packaging, worker agreements/accruals and QC decisions. The real SQLite smoke test passes. The runtime form check passes without showing a window or connecting to the server: it constructs all tabs, checks searchable selectors and separate quantity/warehouse controls, switches wage basis, verifies retry controls and triggers inline validation feedback. API and isolated desktop builds pass. Migration execution, actual concurrent SQL posting, connected UI behavior and end-to-end inventory reconciliation still require local acceptance; these automated checks do not establish live database acceptance.

Manual acceptance:

1. Select a two-unit job; start with cutting, Ashareet, assembly and QC, omitting irrelevant operations. Verify **تحت الإنتاج** and route snapshots after refresh.
2. Complete one unit of cutting. Attempt two units of Ashareet (must reject); record one instead. Verify inline success/error feedback and partial progress through assembly and packaging. Record one accepted unit in the separate QC tab; reject another with a reason, then repair and reinspect it.
3. Receive 10 units at 100 and 10 at 200 for a test material. Issue 4: expect quantity 16, value 2,400, issue cost 600. Return 1: expect quantity 17, value 2,550 and net direct issue cost 450. Verify another warehouse cannot be oversubscribed.
4. Record actual waste against part of the remaining issued quantity; verify it creates no additional stock/cost charge and cannot be returned or observed twice beyond the original issue total.
5. Approve a unit wage for a named worker and record that worker's stage progress; verify earnings accrue once and cannot exceed assigned units. Approve daily, weekly and monthly agreements; verify displayed periods and one accrual after each period ends. Confirm pooled wages stay outside direct WIP. Save one total packaging cost and check that it appears once in WIP and history.
6. Pause with a reason; verify ordinary progress is blocked until resumed. Close a test month through the controlled database/test setup, verify both production and receipt postings are blocked, and verify replay of a previously acknowledged production operation still returns its original outcome.
7. Disconnect the API, prepare a permitted command, close/reopen the app, reconnect and synchronize. Verify one official event and stock/cost effect, stable operation identity, and visible rejection/correction flow.
8. Finish and accept the remaining unit. Verify **مقبول — بانتظار التكلفة والترحيل**, no automatic finished-stock receipt, and unchanged historical order/PDF functionality.
