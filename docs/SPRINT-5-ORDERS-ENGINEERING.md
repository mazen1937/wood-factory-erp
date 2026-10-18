# Sprint 5 — Customer Orders & Engineering Intake

**Status:** Implementation patch prepared for local build and acceptance testing. It has not yet been applied to the owner's working repository or verified against the owner's running API/database.

## Scope

- Create and list customer orders linked to an active customer party.
- Record dated orders and an optional requested delivery date.
- Append engineering-package revisions supplied by the external engineer. The engineer does not need an ERP account.
- Record structured BOM and cut-list lines, optional material references, quantities, units, dimensions and notes.
- Attach and download technical files. Allowed file extensions: PDF, common image formats, Excel/CSV, DWG/DXF, Word and ZIP. Maximum upload size: 20 MiB per file.
- Finalize a technical revision. Once finalized, its lines and attachments are read-only. Creating/finalizing a later revision marks the former finalized revision as superseded without changing its contents.
- Create a draft production-order reference only against a finalized technical revision belonging to the selected customer order.

## Explicitly not in this sprint

- Production execution, stock issue/return, labor, waste, WIP and production costing.
- BOM-based stock reservation or automatic conversion of an engineer's material references into inventory items.
- Sales, invoices, collections and costing rules.
- Offline create/edit and SQLite-outbox synchronization for these new screens. The current client pattern sends this feature to the authenticated API while connected; offline draft/sync behavior remains a cross-cutting implementation item and is not claimed as completed.
- Cloud/object-store deployment for engineering attachments. This MVP patch stores them on the API host in `ENGINEERING_STORAGE_DIR`; that directory must be included in backups. Choose persistent storage when moving the API to a container/production host.

## Database migration

`database/postgres/migrations/005_orders_engineering.sql` is additive and designed to be safe to re-run. An existing persistent PostgreSQL volume does not execute new files copied into `/docker-entrypoint-initdb.d`; apply migration 005 explicitly once after taking a database backup. The migration creates customer orders, engineering revisions, BOM/cut-list lines, engineering file metadata and draft production-order references. It does not alter inventory data.

## Acceptance checklist

1. An authenticated operator can create a customer order linked to an active customer or `both` party. Duplicate order number is rejected with a readable message.
2. An order can hold multiple engineering revisions with monotonically increasing revision numbers.
3. Each draft revision can store BOM and cut-list lines and one or more allowed file attachments.
4. Files download with the original display name; file metadata includes size and SHA-256 checksum.
5. A revision cannot be finalized without at least one BOM/cut-list line.
6. Finalizing a revision makes its content immutable. Finalizing a later revision supersedes the previous current revision but does not rewrite historical lines/files.
7. A draft production order cannot be created from a draft or superseded revision, or from a revision belonging to another order.
8. A valid draft production order records the exact finalized engineering-package ID and revision; no stock, labor, WIP or financial posting occurs.
9. API build and desktop build pass in the owner's Windows environment.
10. UI smoke test: sign in → create order → create revision → add BOM line → add cut-list line → upload/download a PDF → finalize revision → create draft production order → restart desktop and confirm server-backed records remain visible.

## Applying and running

From PowerShell at the repository root:

```powershell
# Back up the current database before applying a new schema migration.
docker exec wood-factory-erp-postgres sh -lc 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /tmp/wood-factory-before-sprint5.sql'
docker cp wood-factory-erp-postgres:/tmp/wood-factory-before-sprint5.sql "$env:TEMP\wood-factory-before-sprint5.sql"

# Copy the additive migration into the running container and apply it explicitly.
docker cp .\database\postgres\migrations\005_orders_engineering.sql wood-factory-erp-postgres:/tmp/005_orders_engineering.sql
docker exec wood-factory-erp-postgres sh -lc 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /tmp/005_orders_engineering.sql'

# Build the API. Run these commands from apps/api.
Set-Location .\apps\api
npm.cmd run build
npm.cmd run start:dev
```

In a second PowerShell window at the repository root:

```powershell
Set-Location .\apps\desktop\WoodFactoryErp.Desktop
dotnet build
# After the build passes:
dotnet run
```

Do not claim the milestone complete until these commands and the smoke test have been run against the actual local environment.
