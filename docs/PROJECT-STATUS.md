# Project status — 2026-11-18

## Current handoff

Pricing/selling/delivery update: **البيع والحسابات** now defaults to three tabs: guided sale/delivery, monthly book and expenses/payments. Dated saved selling prices are separate from revenue; sale/delivery deducts stock once and creates the receivable; cash collection settles it separately. A manager-approved, fully accepted whole-batch production receipt moves reconciled direct actual cost from WIP to finished stock. Shared pool, partial-batch allocation and formal close remain gated, and the monthly result stays provisional. Apply migration **017** once after 016, rebuild/restart both apps; see `SELLING-PRICING-WORKFLOW.md`. Live PostgreSQL verification is pending.

Current verification: API and isolated desktop builds pass; 160 Node regressions pass and five PostgreSQL suites skip without disposable URLs. Finance/monthly/drafts/PDF and production runtime UI checks pass.

Monthly finance workspace: **دفتر الشهر** is the default finance tab with source-driven monthly totals, a full-month searchable/paged activity table, order/job/source trees, CSV export and stock-to-job/cost-source exceptions. Entry forms use compact expand/collapse sections. Backend corrections enforce raw stock unit precision and original-order inheritance for source-based finance documents. That earlier monthly-only patch added no migration beyond 016; the pricing update above adds 017. Rebuild/restart both apps; see `FINANCE-MONTHLY-WORKSPACE.md`. Approved manufacturing/FG/WIP closing remains gated; real PostgreSQL month-end acceptance is pending.

Monthly-view verification: both builds, 144 Node regressions, the built API's authenticated monthly routes and actual finance/monthly UI/draft/PDF checks pass. Four disposable PostgreSQL suites are skipped without test connection URLs. The actual user's database was not inspected or reconciled by this run.

Wage-entry restoration: wages/workers now contains an independent registered-worker wage form for other described work, while assembly retains its wage form. Both use the same manufacturing source/costing/finance path and reference deduplication. Migration **016_general_worker_wages.sql** extends the work-cost category checks; apply once after 015 and rebuild/restart both apps. Previously posted wages are preserved.

Worker endpoint/job-picker correction: the screenshot's missing POST route requires restarting the API from its updated build. Fixed a desktop dependency bug where a failed worker read prevented binding successfully fetched job choices. Job selection now loads independently, worker/API errors remain visible, and unsupported manufacturing actions are gated. Desktop build and built-API worker-route HTTP regression pass; no database migration is added by this correction.

Production cost-workspace follow-up: migration **015_job_manufacturing_costs.sql** and cost-focused cutting/banding/assembly/packaging sections are prepared. Includes unique worker identities, exact source-linked machine/worker charges, per-section/job totals, phase-free quality acceptance and finance manufacturing reports/wage sources without duplicate expense posting. Existing historical wage commitments are preserved and can block conversion pending reconciliation. Owner must apply 015 after 014 and restart the updated apps. See `MANUFACTURING-COST-WORKSPACE.md`; real PostgreSQL acceptance remains pending.

Local verification for this follow-up: both builds pass (desktop 23 existing warnings, zero errors), 132 Node regressions pass, and production plus finance runtime/draft/PDF checks pass. Three opt-in PostgreSQL suites are skipped without disposable test database URLs; no live migration or production costing/financial posting was performed here.

Inventory usability follow-up: shortage totals stay visible with per-order details collapsed by default and persistent show/hide choices during refresh. A compact search covers stock items, balances, shortage orders and full-history movement matches (latest 250 matching movements displayed). No migration added; rebuild/restart API and desktop. See `ORDER-STOCK-SHORTAGES.md`.

18 November: implemented order-confirmation stock checks, order-linked inventory replenishment tables and inline master-item creation. No new migration; this uses the order tables from 014. API/desktop builds, 30 focused regression tests and the Avalonia runtime checks pass. Real PostgreSQL integration is explicitly skipped without a disposable database connection; connected calculation/concurrency acceptance remains pending. See `ORDER-STOCK-SHORTAGES.md` for calculation rules, activation and checks.

The owner reports that finance migration **013 is already applied**. Do not ask them to rerun it. Finance workflow/concurrency acceptance remains pending. The latest order patch requires **014** unless the owner subsequently confirms applying it. Latest focused order verification: both builds, 24 order/job regression tests and the actual Avalonia workspace smoke check pass. Preserve local database backups; they are intentionally excluded from Git.

## Order workspace patch — 17 November 2026

Prepared migration **014_order_workspace.sql** and the revised order/job screen: synchronized code/name searches, several named child jobs per order, inherited product identity/schedule, independent creation/checklist controls, explicit show/hide details, related-job links, clickable attachments and item-based material quantities separate from accessories. Visible sections use production-style cards; A3 generation is retained. 014 can be applied independently of deferred finance migration 013. See `ORDER-WORKSPACE-PATCH.md` for activation and connected checks; live migration/saving/viewer acceptance remains pending. This supersedes the earlier order-preview/board-total screen descriptions below.

## Sales and finance implementation — 17 November 2026

Prepared migration **013_sales_finance.sql**, a financial posting/report API and the **المبيعات والمالية** desktop module. Delivered sale/COGS/stock posting, customer/supplier/worker obligations and payments, advances/allocations/refunds, source-linked supplier receipts and earned wages, unpaid expenses, treasury/transfers, internal grouped invoices, partial deliveries, linked sale returns/cancellations and supported financial reversals. Tables include reconciliation exceptions, CSV and Arabic internal PDF export. Financial drafts/retries use a separate user-scoped SQLite queue.

Uses exact decimal strings/BigInt and NUMERIC, unique serial/reference/source constraints, immutable history, current database permissions, atomic posting, deferred reconciliation and a cross-domain operation namespace registry. Review also corrected raw-receipt floating-point arithmetic and material issue/return rounding. See `SPRINT-9-SALES-FINANCE.md` and `FINANCIAL-INTEGRITY-REVIEW.md`.

**Release remains gated:** the owner reports applying migration 013; real PostgreSQL/concurrency/connected UI acceptance has not been independently run here. Manufacturing costing-to-finished-stock, formal period close, statutory invoices/tax and formal GL remain separate unresolved/dependent work; the UI does not bypass them.

Local verification: API and desktop builds pass (23 existing desktop warnings, no errors), 97 regression/calculation/permission tests pass, and real SQLite/draft/PDF runtime smoke checks pass. One real PostgreSQL suite is explicitly skipped without a disposable test database URL. No database migration or live posting was performed by the agent.

## Production execution milestone

### Production usability update — 17 November 2026

Migration **012_production_workflow_usability.sql** and the redesigned desktop/API are prepared. Stages are cutting, Ashareet, assembly including accessories, packaging, and QC; retired historical stages retain their records. Separate searchable wood/accessory issue and return forms have local quantities and original-warehouse return details. Each action shows inline feedback. Worker agreements support unit/daily/weekly/monthly wages, automatic unit accrual with progress, and once-per-period fixed wage accrual. Packaging records one total per job. QC has a separate accept/reject/reinspection tab. WIP is an explained read-only report; history and empty states are explicit.

Verification: 52 simulated-database regression tests, real SQLite persistence checks and the Avalonia runtime form check pass. Migration 012 and connected database/UI acceptance remain pending. Apply 012 after 011, then rebuild/restart the API and desktop. See `SPRINT-8-PRODUCTION-EXECUTION.md` for commands and acceptance checks. The earlier production scope below describes the initial implementation; this usability update supersedes its stage and wage UI descriptions.

Prepared the **الإنتاج / تحت الإنتاج** main-menu module: selected per-job stage routes (cutting, Ashareet, drilling, assembly, accessories, paint, upholstery, packaging and QC), incremental/partial progress, pause/resume reasons, stock issues and original-cost returns, actual material waste observations, dated direct/pooled labor, non-stock consumable costs, direct-cost carry-forward views and partial QC acceptance. Successful postings include actor/date/source audit and persisted server idempotency. Pending/blocked production drafts and cached data use SQLite with an ordered background outbox and visible synchronization state.

Accepted batches stop at **مقبول — بانتظار التكلفة والترحيل**. Monthly industrial allocation, approved full WIP/period-close reconciliation, partial-completion cost attribution and actual finished-goods stock receipts remain the next costing milestone. No provisional stage status creates sellable stock.

Migration **011_production_execution.sql** is prepared and must be applied after the preceding migrations. It also adds closed-period checking to inventory receipts. The agent has not applied it to the live database. API build and isolated desktop build pass (desktop: 23 existing warnings, zero errors). The production regression tests use simulated PostgreSQL clients; a separate real SQLite smoke test passes for durability, atomic save, retry identity, account isolation, Arabic and cache/rejection handling. Live migration, SQL concurrency and UI acceptance remain pending. See `SPRINT-8-PRODUCTION-EXECUTION.md` for the complete scope, installation and acceptance checklist.

## Complete customer order form

The owner reports that the complete order form is working locally after the manual update. The engineering tab has now been removed from the visible desktop workflow, and new job sheets can use confirmed orders directly. The selected-order dropdown is retained. Job creation prefills product identity, loads the existing quantity/checklist editor, and uses the unchanged A3 PDF generator. Historical job sheets retain their engineering links.

Latest verification: API build passed; desktop verification build passed in `bin/JobSheetVerification` (23 warnings, zero errors). The running desktop executable prevented replacement of the usual output, so the app must be closed and rebuilt/restarted. All 15 simulated database transaction/validation/job-sheet tests passed. New migration `010_order_based_job_sheets.sql` is pending manual application before creating job sheets without engineering revisions. Live database/PDF acceptance for this update remains pending.

The new order form collects customer, order code, product code/name, multiple named attachments with descriptions and optional colors, total boards per order, master-data accessories with total quantities, engineer waste percentage, and an optional overall description. Confirmation saves the complete order and attachment content in one PostgreSQL transaction. The customer shortcut preserves the order draft and returns with the newly saved customer selected. Saved order details show image previews and file downloads.

API build and desktop build passed locally (desktop: 29 warnings, zero errors). All nine transaction/validation regression tests passed using a simulated database client. See `COMPLETE-ORDER-WORKFLOW.md` and `tests/complete-orders.test.cjs`.

The owner reports successful use of the order form after the manual migration steps. Docker access and the direct PostgreSQL connection were denied in this execution environment, so the agent did not apply or independently inspect the live migrations. Migration 008 below belongs to the earlier engineering redesign.

## Earlier engineering redesign status

## Verified baseline reported by the owner

- Uploaded source snapshot: branch `main`, latest commit `324c016` (`feat: add production work order and engineering workflow`), with no tracked local modifications reported. Untracked `before-inventory-backup.sql` and `erp-auth-review.zip` were intentionally excluded from the review archive; preserve them for later inspection.
- The owner ran `npm.cmd run build` for the API and confirmed success.
- The owner ran `dotnet build` for the desktop and confirmed success with 41 warnings, no build errors. Warnings include Avalonia analyzer/compiler version mismatch, Windows-only ProtectedData platform warnings, and obsolete `TextBox.Watermark` properties.
- The owner verified the Sprint 7 schema: `public.engineering_cutlist_parts` exists and all eight expected metadata columns were found on `engineering_package_files`. Thus migration 007 is present in the development database; do not rerun it.

These are owner-reported local checks, not checks executed in this review environment.

## Sprint 7 redesign prepared in source

The product-reference + color-specific cut-list + product-wide recipe redesign is prepared in the source snapshot:

- Product code, product description and upholstery requirement on each engineering revision.
- Each color cut-list references an active stock-tracked raw-material item and captures color, board count per finished unit, and waste percentage. Thickness/length/width entry is no longer part of the new UI.
- Product-wide accessory/raw-material/paint recipe lines use master-data items and quantity per finished unit.
- Finalization requires a product reference and at least one cut-list.
- New additive migration: `008_product_recipe_engineering.sql`.
- The existing job-order form and `ProductionWorkOrderPdf.cs` are not changed.

**Status: patch prepared for review; migration 008 has not been applied, and the redesigned API/desktop builds and runtime workflow have not yet been verified after these source edits.**

## Next steps

1. Review/apply the patch while preserving user files, then back up PostgreSQL and apply migration 008 exactly once.
2. Build API and desktop and address any compiler errors before running.
3. Test reference upload, two color-specific cut-lists, master-data recipe dropdowns, draft line deletion, required finalization conditions, immutable revisions, and job-order PDF regression.
4. Only after this workflow is accepted, proceed to production execution and subsequent costing/WIP work. Full offline SQLite/outbox sync, comprehensive automated tests, module-wide permissions, financial postings, and integrated costing remain incomplete.
