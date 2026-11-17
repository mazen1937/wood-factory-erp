# Financial integrity review — 17 November 2026

Reviewed the reference financial/database contracts, all current domain schema migrations, the new finance posting/report paths, stock receipts and production material/labor cost paths, and the order inputs feeding production. This is a bounded source/calculation review with automated checks; real PostgreSQL/connected acceptance remains outstanding. It does not establish that the whole ERP is free of defects.

| Finding | Change / evidence |
| --- | --- |
| Raw receipts used JS Number multiplication/toFixed for money | Cost, balance and weighted-average calculations now use PostgreSQL NUMERIC. Receipt operation lock protects identical concurrent retries. Backdated valuation and future receipts are rejected. |
| Rounded displayed average could overstate a tiny-value material issue | Production issues use recorded inventory value/quantity at full internal precision; depletion consumes the residual. |
| Repeated partial returns could accumulate rounding excess | Production returns and financial sale credits use cumulative original-value entitlement minus prior credited value. Full returns reconcile to the original amount. |
| Legacy labor entry could overlap new unit-wage agreements | Legacy labor posting is blocked on stages using the new unit-agreement workflow. New wage payable links earned source records without charging production cost again. |
| Old signed admin roles could survive revocation | Authenticated requests now refresh active user/current roles from the database. Malformed signatures and expired/invalid signed sessions reject cleanly. Finance also validates live roles inside posting. |
| Reusing an operation ID across unrelated modules | Migration 013 installs a shared namespace registry and triggers, retaining intentional production-event/material-movement grouping. Existing conflicts block installation for reconciliation. |
| Payment could be counted as another sale/expense | Distinct obligations, settlements, allocations and treasury ledgers. Document generation and allocation have no second stock/revenue/cost effect. |
| Duplicate references or timeout retries | Unique internal serials/business/source references, durable client operation IDs, payload hashes, atomic acknowledgments, and database constraints. Exact request replay returns its committed result. |
| Concurrent overselling/over-allocation | Serialized finance transactions plus item/balance/account locks, central availability checks and commit reconciliation. Requires the supplied real PostgreSQL concurrency test before acceptance. |
| Return of already-paid goods | Original debt credit and release of excess allocations into customer credit. Refund is a separate bounded treasury outflow. |
| Historical tax/product/party values could drift | Immutable effective tax policies, line snapshots, stored document party names, request/actor/date/source history; normal posted edits/deletes are rejected. |
| Partial multi-ledger writes | One transaction for all linked effects, deferred header/line/stock/debt/treasury checks and precommit balance validation. Forced-failure service tests verify rollback calls; real SQL test covers atomic execution when run. |
| Misleading stale/local state | Pending saves never count as official balances. Stale cache is labeled; a successful master-data fetch cannot relabel cached finance as fresh. Sign-out clears financial forms/tables, and saved drafts are scoped to the user. |
| Inventory screen labeled new stock movements as receipts | Sale issues, returns and purchased finished-goods receipts now have distinct labels. |

## Explicit limits and unresolved dependencies

- Manufacturing allocation, completion cost transfer and formal WIP/period close are not implemented by this sales sprint. QC-accepted work cannot be sold without finished stock. No zero-cost manufactured receipt is invented.
- The four-decimal policy is an internal accounting precision, not statutory invoice/currency compliance. Tax exemptions/rates need an explicit recorded basis of approval. Internal invoice PDFs/serials are not legal/e-invoice compliance.
- Formal double-entry GL/chart of accounts, bank reconciliation imports, foreign currencies, statutory tax reports, approved opening-data migration and full period-close/reopen remain outside this delivery.
- Reversals cover collections, payments, expenses and transfers; source purchase/stock-value corrections and wage agreement corrections require additional controlled workflows. Do not edit posted tables directly.
- Direct packaging/consumable costs already in production should not be re-entered as industrial expenses. Their supplier-liability matching needs an explicit source-linked extension. The current wage and raw-receipt source-link paths do not duplicate their costs.
- Report projections require source-ledger reconciliation and approved opening balances before pilot use. Missing supplier links are shown explicitly; they are not silently counted as both a receipt and a new purchase.
- Current financial posting uses a single advisory lock for correctness and modest factory throughput. Large-scale concurrency/long report history needs measured pagination/performance work.
- UI form validation, arithmetic, source wiring, replay/rollback branches, SQLite and PDF generation have automated checks. The PostgreSQL migration, deferred constraints, real isolation/concurrency and connected UI remain unverified in this environment. `tests/finance-postgres.test.cjs` is provided and explicitly skips without a disposable database URL.

No production rollout or financial accuracy guarantee is implied by passing simulated-client tests. Required release evidence is a successful real database acceptance run, source/treasury/debt/stock reconciliation, approved accounting settings and the connected operator checklist in `SPRINT-9-SALES-FINANCE.md`.
