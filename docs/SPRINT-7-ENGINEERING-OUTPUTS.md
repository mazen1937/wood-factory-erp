# Sprint 7 — Product reference, color cut-lists, and product recipe

## Purpose

This redesign replaces the earlier piece-dimension entry workflow. Staff should record the information the external engineer actually provides and needs the factory to use: the product reference, a set of color-specific cut-list files, the number of boards per color, and the engineer's waste percentage. The ERP should not ask operators to re-enter every cut piece's dimensions.

The existing production job-order form and A3 PDF generator are out of scope and must remain unchanged.

## New product revision workflow

1. Create an engineering revision under the selected customer order, entering one product code, product description, notes, and whether upholstery is required.
2. Upload the product reference image as `product_reference`; record an optional description.
3. Upload any other relevant engineer documents by selecting the attachment type and adding an optional description.
4. For each color-specific cut-list file, select the board item from active stock-tracked raw-material items in master data, enter its color, the engineer's board count per finished unit, and the engineer's waste percentage from 0 to 100. The product code is inherited from the revision, so all cut-lists within that revision share the same product identity.
5. Record the shared product recipe: add one line at a time using a master-data drop-down and quantity per finished product unit. Categories are accessories, raw materials, and paints. Lines can be deleted while the revision remains a draft.
6. Finalize the revision only after a product-reference attachment and at least one cut-list attachment exist. A finalized revision remains immutable; changes require a new revision.
7. The existing production-order and work-order/PDF screens continue to work separately as before.

## Data/costing interpretation

- **Product code and description** live on the engineering revision. All attached files and color cut-lists inherit that product identity; code is not repeatedly typed per attachment.
- **Board requirements** are per color-specific cut-list: board item, color, boards per finished unit, and waste percent.
- **Accessories, other raw materials, and paint requirements** are product-wide recipe lines. They are stored once per product revision rather than repeated on every color cut-list, preventing duplicate counting when a product uses multiple colors.
- **Recipe quantities are per one finished product unit.** A future production estimate can multiply them by the planned production quantity.
- The engineer-provided board quantity and waste percentage are stored independently. The workflow does not automatically add waste percentage to board quantity: the supplied board count may already reflect the nesting/yield calculation, and adding the percentage again could double-count waste.
- These records supply engineering/planned material inputs; they do not yet implement a complete production-cost engine. Actual consumption, moving-average material cost, paint actual-cost treatment where applicable, wages, industrial-expense allocation, WIP, and finished-goods cost remain governed by the financial specification and later production/costing milestones.
- The system does not run OCR or redo the engineer's cutting/nesting optimization.

## Database migration

Migration `007_engineering_outputs.sql` belongs to the earlier implementation and has already been verified in the development database. **Do not rerun it just to apply this redesign.** Apply the additive migration `008_product_recipe_engineering.sql` after taking and verifying a PostgreSQL backup. It adds product identity and upholstery fields, links a cut-list board to an item in master data, records waste percentage, relaxes the earlier requirement for piece/board dimensions, and adds product-wide material recipe lines. Existing cut-list piece rows and attachment records are preserved; the redesigned screen no longer asks users to enter new piece-dimension rows.

Example from PowerShell at the repository root:

```powershell
# Back up to a file inside the PostgreSQL container, then copy it to the host.
docker exec wood-factory-erp-postgres sh -lc 'pg_dump -Fc -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /tmp/wood-factory-before-sprint7-redesign.dump'
if ($LASTEXITCODE -ne 0) { throw "PostgreSQL backup failed." }
docker cp wood-factory-erp-postgres:/tmp/wood-factory-before-sprint7-redesign.dump "$env:USERPROFILE\Desktop\wood-factory-before-sprint7-redesign.dump"
if ($LASTEXITCODE -ne 0) { throw "Could not copy PostgreSQL backup to Desktop." }
if ((Get-Item "$env:USERPROFILE\Desktop\wood-factory-before-sprint7-redesign.dump").Length -le 0) { throw "Backup file is empty." }

# Copy and apply only migration 008.
docker cp .\database\postgres\migrations\008_product_recipe_engineering.sql wood-factory-erp-postgres:/tmp/008_product_recipe_engineering.sql
if ($LASTEXITCODE -ne 0) { throw "Could not copy migration 008." }
docker exec wood-factory-erp-postgres sh -lc 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /tmp/008_product_recipe_engineering.sql'
if ($LASTEXITCODE -ne 0) { throw "Migration 008 failed. Stop and inspect the error." }
```

Do not reset the database volume and do not rerun migrations 005, 006, or 007.

## Build and acceptance

```powershell
Set-Location "C:\Users\HP\source\wood-factory-erp\apps\api"
npm.cmd run build
if ($LASTEXITCODE -ne 0) { throw "API build failed." }

Set-Location "C:\Users\HP\source\wood-factory-erp\apps\desktop\WoodFactoryErp.Desktop"
dotnet build
if ($LASTEXITCODE -ne 0) { throw "Desktop build failed." }
```

Manual acceptance:

1. Create a test engineering revision with product code, description and upholstery yes/no.
2. Upload a product reference image and optional description.
3. Add at least two cut-list files for different colors. Select valid board items from master data and enter board count and waste percent for each.
4. Verify every attachment appears under the same product code and each cut-list keeps its own board item, color, count, and waste percent.
5. Add at least two accessories, a raw material, and a paint requirement using the master-data drop-down. Verify quantities are labelled per finished unit and that a line can be removed while the revision is a draft.
6. Verify finalization fails if there is no reference image or no cut-list attachment, and succeeds once both exist.
7. Verify finalized revision changes are rejected and new changes require a new revision.
8. Regression-test the existing production-order/job-order workflow and A3 PDF. `ProductionWorkOrderPdf.cs` is not modified by this redesign.

## Known boundary

The current data model allows one finalized engineering revision per customer order, consistent with the existing order design. If a single customer order must contain multiple different product codes, the order model will need explicit product/order lines in a separate planned change rather than quietly mixing products in one revision.
