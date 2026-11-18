# Pricing, selling and delivery — 18 November 2026

The desktop module is now **البيع والحسابات**, with three normal tabs: **بيع وتسليم**, **دفتر الشهر**, **مصاريف ودفعات**. Advanced returns, invoices, treasury/configuration and other existing workflows remain available behind the advanced-procedures checkbox. Existing records and forms are preserved.

## Daily workflow

1. In production, record material consumption, actual machine/worker charges and packaging, then accept the produced quantity in QC.
2. Open **بيع وتسليم**. Select a customer, optionally their order, a stock-tracked finished-product item and finished-goods warehouse. The inline product dialog creates a master item only, never an opening balance.
3. If the goods came from this factory, expand **المنتج من تصنيعنا ولم يدخل مخزون التام بعد؟**, select its job, review recorded costs and matching item/quantity, explicitly approve completeness, and receive the accepted whole batch. Only a finance manager or administrator may approve this transfer.
4. Enter quantity, selling unit price before tax, total discount, an approved tax/exemption policy and any delivery charge. The preview separates customer total, actual inventory cost and gross profit. **حفظ سعر البيع** stores a reusable dated price without posting revenue, debt or cash.
5. **تسجيل البيع والتسليم المحدد** posts the sale; the checked delivery option records delivery in the same transaction. Uncheck it for a later delivery. Stock is issued only once, at sale. Later delivery does not issue stock again.
6. Select the posted sale and record only the money actually received, its treasury account and a unique reference. This settles the linked receivable without posting revenue again. New sale/payment buttons create new references deliberately.
7. Use **دفتر الشهر** for monthly source activity, product costs, tax, gross profit, industrial costs, office/administrative expenses, cash and debts, with order/source drill-down.

Saving an order, recording production cost, receiving finished goods, or saving a price does not create sales revenue. The sales screen explains a zero revenue balance when no sale has been posted. It does not populate invented balances.

## Costing and integrity boundaries

The new transfer supports one fully QC-accepted batch per job, at its reconciled positive recorded direct actual cost. Planned and accepted quantities must match. Sources must reconcile, have no later posting date than the receipt, and contain no outstanding legacy wage commitments. Approval attests that all job costs have been entered and the selected finished item matches the physical product. The server blocks transfer when pooled wages or industrial shared expenses remain in any of the job's cost months. It never guesses an allocation percentage or silently uses zero cost.

**Shared overhead allocation, partial-batch cost attribution, controlled post-completion cost adjustments and formal period closing remain unimplemented.** Such jobs remain blocked; there is no provisional-stock bypass. Later shared expenses still require formal allocation/closing; the monthly result remains explicitly provisional rather than certified net profit. Approved costs and source snapshots are immutable; further cost postings to a completed job are blocked.

The transfer moves WIP value to finished inventory without creating another expense or payable. On sale, inventory value becomes COGS; gross profit is net sale before tax minus COGS. Office expenses appear separately in the monthly result. Customer tax is separate from revenue/profit; no legal tax rate or profit distribution is invented. Cash collection is separate from sale. A saved selling price never changes actual inventory cost or historical sales.

Exact decimals, current database roles, operation/reference identities, global financial posting lock, job/stock locks, transaction rollback, immutable histories, deferred completion checks and source reconciliation protect posting. Duplicate retries return the original acknowledgment. The sale-and-delivery command has its own API command name so an old server cannot silently strip the delivery option. Capability diagnostics identify an old API build.

## Activation

Migration correction: the owner reported a syntax error in `require_production_completion_source` before COMMIT. Its function delimiters were accidentally reduced to single dollar signs; both are now restored to `$$`. With the displayed ON_ERROR_STOP invocation, disconnecting the failed transaction rolls back all earlier 017 statements. Rerun the corrected file once; no table deletion or manual partial rollback is needed. The added delimiter regression passes. Docker access is denied to the agent, so this correction has not been executed against PostgreSQL here.

Apply migration **017_sales_pricing_and_finished_goods.sql** once, after 016. This run did not apply any migration to the live database. From PowerShell at the repository root:

```powershell
cd C:\Users\HP\source\wood-factory-erp
docker compose exec postgres sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /docker-entrypoint-initdb.d/017_sales_pricing_and_finished_goods.sql'
```

Then rebuild the API with npm.cmd run build in apps/api, restart its existing process, close/rebuild/reopen the desktop. Do not rerun prior applied migrations. The desktop normal output was locked by the currently running application during verification, so verification used bin/SellingWorkflowVerification instead.

## Verification

API and isolated desktop builds pass (23 existing desktop warnings, zero errors). 160 Node regressions pass; five disposable PostgreSQL suites skip without their explicit test database URLs. Real Avalonia finance/monthly/draft/PDF and production controls pass, including the cost 2000 / net sale 2700 / tax 378 / customer total 3078 / gross profit 700 example. A queued date-change event that cleared an entered selling price was corrected and covered by the UI smoke check.

The new tests/selling-postgres.test.cjs suite uses SELLING_TEST_DATABASE_URL, creates an isolated schema, migrates all files, checks production acceptance through transfer, saved price, sale/delivery and cash, replays acknowledgments, checks one stock issue and frozen completed costs, then removes only that test schema. **It has not run against PostgreSQL in this session.** Live migration, database constraint/concurrency execution, financial reconciliation and operator acceptance remain pending; passing simulated tests does not establish live SQL correctness.
