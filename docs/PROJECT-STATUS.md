# Project status — 2026-10-18

## Source snapshot

The supplied audit ZIP reports branch `main` at commit `a110886` (`feat: add inventory receipt workflow`). This was a source snapshot, not a live repository connection. The ZIP did not include `.git`, dependencies, the actual `.env`, local SQLite files or the running PostgreSQL volume.

## Previously reported local state

- The user reported API build success after the inventory patch and desktop build success after the Avalonia ComboBox fix.
- PostgreSQL was reported healthy locally, and the inventory migration tables were present when queried.
- The supplied database inspection showed one item, one warehouse, two parties, one inventory cost row, one warehouse-balance row and two inventory movement rows. No `TEST-*` item was found.
- These are reported/observed baseline facts, not proof that the new Sprint 5 patch builds or has been applied.

## Sprint 5 patch prepared

Customer orders and engineering intake implementation is supplied in the accompanying patch ZIP. It adds customer-order CRUD-for-create/list, append-only engineering revisions, structured BOM/cut-list rows, technical file upload/download, revision finalization and draft production-order references tied to finalized technical revisions.

**Status: not yet applied, built or smoke-tested on the owner's Windows machine.** Apply migration `005_orders_engineering.sql` explicitly to the existing PostgreSQL volume after backing up the database; PostgreSQL's initialization directory does not apply new migration files to an already initialized volume.

## Sprint 5 exit gate

Follow `docs/SPRINT-5-ORDERS-ENGINEERING.md`. The gate is API and desktop builds passing, plus the end-to-end smoke flow passing with an engineering revision retained by ID through the draft production order.

## Known scope boundary

This sprint follows the current connected API interaction pattern. The new screens do not yet persist offline drafts to SQLite or use the local durable outbox. Do not treat offline synchronization as completed. Production execution, material consumption, labor, WIP and costing remain the next phase after order/engineering intake is accepted.
