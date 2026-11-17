# Project roadmap

Build real factory workflows incrementally. Each phase has an operational exit gate; do not jump ahead while the preceding workflow cannot be demonstrated.

1. **Foundation and identity** — NestJS API, Avalonia desktop, PostgreSQL schema, login, role checks, environment configuration and health endpoint. Existing code and user-reported local checks are the baseline; repeat builds when changing the source.
2. **Master data** — Arabic-first management of units, warehouses, customers/suppliers and items. Previously reported working; verify again on the current source when needed.
3. **Inventory ledger** — receipt, issue, return, moving-average cost and stock reconciliation. Receipt workflow is implemented; full inventory acceptance remains an incremental hardening target.
4. **Orders & engineering intake** — customer orders, external engineer's package revisions, BOM/cut-list lines, file attachments, immutable finalized revisions and draft production order referencing a specific revision. Exit when the Sprint 5 acceptance checklist passes.
5. **Production execution** — prepared in migrations 011–012: simplified stages, separate searchable wood/accessory issues and returns, actual waste, worker wage agreements and earned wages, per-order packaging cost, a dedicated QC accept/reject/reinspection section, explained WIP and a durable production outbox. Source builds and automated checks pass; live migration and operational acceptance remain required. Accepted quantities await the costing/finished-goods transfer milestone.
6. **Costing, WIP and period close** — configurable direct and pooled costs, three-plank allocation base, partial completion/WIP carry-forward and reconciliations.
7. **Sales and integrated finance** — local implementation prepared in migration 013: sale/COGS/finished-stock posting, internal invoices, advances/collections/allocations, source-linked payables/wages, expenses, treasury/transfers, deliveries/returns and reconciliation tables. Manufactured sales require the preceding approved costing/finished-stock transfer. Real PostgreSQL and connected acceptance remain release gates; statutory documents/tax, formal GL and period-close policies are not silently assumed.
8. **Migration, pilot and go-live** — reconcile Ultra opening balances and provenance, Arabic printouts, backups/restore drill, operator acceptance, cutover and stabilization.

## Cross-cutting backlog

- Complete local SQLite usage and durable outbox/sync behavior for new workflows; server remains authoritative for official posting.
- Automated test coverage for transaction acceptance scenarios.
- Production backup/restore, monitoring, secrets handling and attachment storage/deployment strategy.
- Resolve workbook/accounting questions marked OPEN in the Financial Rules Specification before implementing the affected financial behavior.
