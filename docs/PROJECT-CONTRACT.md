# Wood Factory ERP Project Contract

## Purpose and scope

The ERP replaces the factory's operational Excel workflow. It must link customer orders and external engineering packages to purchasing, inventory, production, costing, WIP, finished goods, sales, invoices, collections, supplier payments, treasury, reporting, and audit history. MVP completion means operational parity and reconciliation, not a collection of screens.

The external engineer does not require an ERP account. Engineering packages, drawings, BOMs, cut lists, instructions, and revisions must remain traceable to the production order that used them.

## Approved architecture

| Layer | Approved responsibility |
| --- | --- |
| Avalonia C# desktop | Arabic-first RTL user interface, English support, local workflow, cache, drafts, convenience validation, visible sync state. |
| SQLite on each device | Local working copy, durable drafts/outbox, acknowledgments, pull cursor and diagnostics. Never authoritative for official posting. |
| NestJS modular monolith | Authentication, permissions, business rules, central validation, API, sync, audit, posting orchestration. |
| PostgreSQL | Authoritative master data and all officially posted inventory, production, costing, sales, treasury and financial records. |
| Docker and DigitalOcean | Development/deployment direction. Production requires selected topology, backups, monitoring, secrets, and tested restore. |

Critical transactions may be prepared offline, but are official only after server validation and a successful PostgreSQL commit. The sync target is every 10 seconds while connected, plus startup/reconnect/new-operation triggers. It is not a delivery guarantee.

## Data and audit rules

- Use UUID internal IDs separate from human-readable business codes.
- Store UTC timestamps and explicit business-effective dates/accounting periods.
- Use exact decimal/numeric storage for quantities, money, costs and rates; never binary floating point.
- Use explicit statuses and lifecycle transitions.
- Use foreign keys, unique/check constraints, transactions and concurrency controls for integrity-critical rules.
- Master data is soft-deactivated when history references it. Posted records are append-only in business meaning; correct via linked reversal or adjustment, never destructive edits.
- Store actor, device, reason where needed, correlation ID, source document/reference and historical cost/configuration snapshots.
- Arabic/English labels belong in localization resources. Persist stable language-neutral identifiers.
- Effective-dated settings/rates/classifications apply prospectively. They do not rewrite history.

## Identity and authority

Users, roles, permissions, user-role grants, devices, audit events and server idempotency records are first-class platform data. Passwords are hashed; authorization is enforced by the backend, not by UI visibility. Separate permissions exist for entry, posting, configuration, period close, and closed-period correction.

The initial deployment may have one device and one operator holding multiple roles, but the data model/protocol must remain multi-device ready.

## Sync and idempotency contract

Each client write has one immutable operation ID, device ID, user identity, operation type, payload hash, expected version/precondition, and dependency IDs where necessary.

1. Save the local business record and its outbox row in the same SQLite transaction.
2. Send the operation to the API; a timeout leaves the outcome unknown.
3. The API authenticates, authorizes, validates current PostgreSQL state, checks idempotency, and performs all linked effects in one PostgreSQL transaction.
4. Only after commit, return a durable acknowledgment with operation ID, outcome and central record IDs/versions.
5. The client verifies and stores the acknowledgment, then marks the outbox entry acknowledged. It retains the business record.
6. Retrying after uncertainty always uses the same operation ID and identical logical payload. A reused ID with a different payload is rejected.

Outcomes are `POSTED`, `REJECTED`, `RETRYABLE_OR_UNKNOWN`, or `CONFLICT_NEEDS_RESOLUTION`. Permanent rejections stay visible and do not blindly retry. Use bounded exponential backoff for transient failure. Never delete pending data before an acknowledgment is recorded. Sync is not a backup.

## Core master data and domains

Maintain items, Arabic/English units, warehouses, parties, products/specifications, labor rates, allocation policies, financial periods, controlled document sequences, attachments and engineering revisions.

Item type, stock tracking, direct-cost behavior, allocation eligibility and classification are separate concepts. Users can classify a transaction/item as `خامات` or `مصاريف صناعية`; classification changes are audited and future-facing. Start with raw-material and finished-goods warehouses.

A customer order is not a production order. A production order can be customer-linked or stock production. A BOM describes required components; a cut list describes pieces/dimensions; a work order provides executable factory instructions. Product specification/revision changes must not silently affect in-progress or historic production.

## Inventory and purchasing

The inventory movement ledger is the traceable source of stock movement. A read-optimized balance/valuation state is allowed only when transactionally consistent and reconciled to the ledger.

- Moving-average valuation is the default. Posted receipts update it; issues use the applicable cost at posting.
- Block negative stock by default. Any future exception needs explicit authorization/audit.
- Material returns reference the original issue and use original issue cost by default.
- Countable material issue cost equals quantity multiplied by applicable unit cost.
- Direct actual-cost consumables can record actual cost without mandatory quantity multiplication; stock effects depend on configuration.
- A purchase/receipt, inventory movement, valuation update, payable effect where applicable, audit event and idempotency outcome post atomically.
- A supplier payment is separate from the purchase/expense. It settles a payable and affects selected cash/bank only once.

## Production, costing, WIP and finished goods

Production records actual consumption, operations, labor, direct costs, approved allocations, WIP and completion. The operation `Ashareet` may be configured where applicable.

A material issue requires an eligible production order, valid item/unit, authority, open-period policy and centrally available stock. It atomically creates the stock movement and linked production-cost attribution; neither may exist without the other.

Labor uses the effective configured rate/basis and records whether it is direct, pooled, or otherwise treated. A cost source cannot be counted twice.

The monthly industrial allocation denominator is the recorded acquisition/material value of the three eligible main wooden-plank types produced in that month, not selling price. Eligible types are configurable. Accessories, paint, wood outside the configured plank types and other materials are excluded unless explicitly configured. The 500-plank figure is planning/capacity only, not actual production.

Allocation rate = eligible industrial-expense pool / eligible denominator, only when the denominator is positive. Persist pool composition, denominator, exclusions, rate, allocations, rounding/residual handling and policy version. When the denominator is zero, flag/carry the pool for authorized review; do not invent a rate.

Closing WIP = opening WIP + current eligible production costs - cost transferred to completed finished goods - approved WIP removals. A month may close with open production. Opening WIP carries forward and is never re-added as a new cost.

Only accepted completed goods enter finished-goods stock. Internally produced finished goods use recorded actual production cost; externally purchased finished goods use acquisition cost. Identical batch units share batch cost across accepted identical units; different specifications use distinct product codes.

## Sales, receivables, payables and treasury

Sale, delivery, invoice, customer advance, collection and receipt are distinct but linked records. Sale posting validates stock, product cost, quantity, price, discount, tax/document configuration, period, authority and idempotency; it atomically posts stock deduction and COGS exactly once.

Net sales = selling amount minus discounts, excluding separately accounted tax and approved adjustments. Gross profit = net sales - COGS. Margin = gross profit / net sales × 100 only when net sales is positive. Preserve price, discount, tax and cost snapshots.

Returns/cancellations are linked reversals/credits; do not delete original sales. Customer outstanding derives from posted obligations minus allocations/credits. Supplier outstanding derives from posted obligations minus allocations/credits. Partial payments are supported.

Cash and bank are separate treasury accounts. Combined treasury is their sum. A transfer posts paired legs atomically and creates neither income nor expense.

## Period close, reports and migration

Ordinary posting/backdating into a closed period is blocked. Privileged correction/reopen requires permission, reason, actor, timestamp and linked adjustment. Reports must drill down to posted source records and adjustment history.

Required pilot reports include inventory ledger/balances; allocation pool/denominator/rate; production cost/WIP; finished goods; sales/discount/tax/COGS/margin; customer and supplier balances; cash/bank/combined treasury; and period-close reconciliation/audit.

Ultra is current workbook evidence; MHM is historical evidence. Preserve read-only originals, profile rows and formulas, map legacy codes separately, reconcile approved opening balances, run a trial migration with exception reports, and set a controlled cutover date. Do not operate Excel and ERP as parallel sources of truth.

## Mandatory acceptance examples

- Receive 10 units at EGP 100 and 10 at EGP 200: 20 units, EGP 3,000 value, EGP 150 average. Issuing 4 costs EGP 600 and leaves 16 units/Egp 2,400.
- Eligible value EGP 5,000 with pool EGP 1,000 yields a 20% allocation rate; EGP 100 eligible value receives EGP 20.
- A zero allocation denominator creates an unresolved/unallocated state, never a divide-by-zero result.
- Opening WIP EGP 1,000 + additions EGP 500 - transfer EGP 900 = closing WIP EGP 600; it carries forward once.
- Cost EGP 2,000, price EGP 3,000, discount EGP 300 and separate tax yields net sales EGP 2,700, gross profit EGP 700 and margin about 25.93%.
- Replaying a committed operation ID never creates a second stock, COGS, payment, allocation or treasury effect.

## Delivery order

1. Identity, roles, devices, audit and idempotency/synchronization foundations.
2. Master data and effective-dated configuration.
3. Inventory ledger, moving-average valuation, receipts/issues/returns and concurrency tests.
4. Customer orders, engineering packages, BOM/cut lists and production orders.
5. Production consumption, labor, WIP, allocation and completed finished goods.
6. Sales, invoices, collections, supplier payments, expenses, treasury and period close.
7. Arabic-first workflows, reports, migration/reconciliation, backup/restore, end-to-end tests and factory pilot.

Implementation begins with schema migrations and domain services, not dashboards.
