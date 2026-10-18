# Sprint 6 — Production Work Order (`أمر شغل إنتاج`)

## Goal

Replace the handwritten/partly filled factory job-order form with a persisted ERP work order and a downloadable, populated, two-page A3 PDF. The paper PDF supplied by the project owner is the source for the form's fields, standard items, custom rows, and signature areas.

## Workflow

1. In **Orders**, select a customer order and a finalized engineering-package revision.
2. Create a production-order draft using a unique order number, product code, product name, production date (`YYYY-MM-DD`), and planned quantity.
3. In the **أمر شغل وتجهيز إنتاج (Job Order)** tab, select the production order. The system loads the fixed form rows and two custom-item rows.
4. Tick the standard items that apply; change each required specification/measurement and enter quantity. For either custom row, enter item name, specification, unit, and quantity.
5. Click **حفظ أمر الشغل**. The draft work-order header and all rows are saved to PostgreSQL.
6. Click **تحميل أمر الشغل PDF**. The app saves the draft data first, fetches it back from the API, generates the PDF from the saved data, and opens a Save dialog.

The work-order editor is intended for draft production orders. When production has started or the order is otherwise no longer a draft, editing is blocked; PDF download remains available.

## Form coverage

The migration seeds 24 standard rows based on the source form: MDF, CNT, HDF, PVC 22/PVC 40, hinge variants, drawer runners, handles, sliding-door hardware, hanger pipe/holder, bed-assembly set, shelf supports, floor levelers, silicone assembly item, metal L-angles, and wardrobe connector clips. It also includes two user-defined addition rows. The PDF carries order number, date, product code/name and planned quantity, then the materials/accessories table, extra rows, and signature boxes for cutting/edge-banding, assembly and quality.

The generated PDF reproduces the form structure and uses Arabic RTL text and A3 page size. It is a programmatically rendered document rather than an overlay on the original PDF background, so check the first printout for visual fit before the factory uses it regularly.

## Apply database migration 006

The source patch installer does not touch the database. After it succeeds, open PowerShell and run the following from any directory. It makes a fresh backup before applying the additive migration.

```powershell
$ErrorActionPreference = "Stop"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backup = Join-Path $env:TEMP "wood-factory-before-sprint6-$stamp.sql"

# Backup the current central database inside the running PostgreSQL container.
docker exec wood-factory-erp-postgres sh -lc 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /tmp/wood-factory-before-sprint6.sql'
if ($LASTEXITCODE -ne 0) { throw "Database backup failed; migration not applied." }

docker cp wood-factory-erp-postgres:/tmp/wood-factory-before-sprint6.sql $backup
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $backup) -or (Get-Item $backup).Length -eq 0) {
    throw "Could not verify the backup; migration not applied."
}
Write-Host "Backup verified: $backup" -ForegroundColor Green

Set-Location "C:\Users\HP\source\wood-factory-erp"
$migration = ".\database\postgres\migrations\006_production_work_order.sql"
if (-not (Test-Path $migration)) { throw "Migration 006 not found." }

docker cp $migration wood-factory-erp-postgres:/tmp/006_production_work_order.sql
if ($LASTEXITCODE -ne 0) { throw "Could not copy migration 006." }

docker exec wood-factory-erp-postgres sh -lc 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /tmp/006_production_work_order.sql'
if ($LASTEXITCODE -ne 0) { throw "Migration 006 failed; stop and inspect the error before retrying." }
Write-Host "Sprint 6 migration applied." -ForegroundColor Green
```

Migration 006 is additive: it adds product name/date columns, seeds the fixed catalog, and adds saved work-order lines. It does not delete inventory records. Do not reset the database.

## Build

### API

```powershell
Set-Location "C:\Users\HP\source\wood-factory-erp\apps\api"
npm.cmd run build
```

### Desktop

```powershell
Set-Location "C:\Users\HP\source\wood-factory-erp\apps\desktop\WoodFactoryErp.Desktop"
dotnet build
```

Then run the API in a separate terminal:

```powershell
Set-Location "C:\Users\HP\source\wood-factory-erp\apps\api"
npm.cmd run start:dev
```

Run the desktop in another terminal:

```powershell
Set-Location "C:\Users\HP\source\wood-factory-erp\apps\desktop\WoodFactoryErp.Desktop"
dotnet run
```

## Acceptance checklist

- [ ] Existing sign-in works and the Orders tab loads.
- [ ] Create a customer order and a technical package, add its engineering lines/attachments, then finalize the package.
- [ ] Create a production-order draft with an unused order number. Product code, name, date and planned quantity are saved.
- [ ] Newly created order is selected and the 24 predefined item rows plus two custom rows load.
- [ ] Select a few standard items and enter realistic quantities/specifications; add one custom item.
- [ ] Save, switch away and back to the production order, and verify the entered data remains.
- [ ] Download the PDF and confirm it is exactly two pages, A3, opens in a PDF reader, and has the expected order fields, item rows, selected checkboxes, custom row, Arabic labels, and three signature spaces.
- [ ] Print one test copy and review RTL alignment/fit before giving the form to factory staff.
- [ ] Confirm QuestPDF Community license eligibility for the company's commercial use before production deployment.

If production-order creation still fails, keep the error text shown in the UI and the API terminal output from the same attempt. Do not repeatedly retry with the same order number if the result is uncertain; check whether the order appeared in the list first.

## Scope boundary

This sprint implements the work-order form and PDF. It does not yet implement production operation postings, actual material issues/returns, labor costing, scrap accounting, WIP, or finished-goods completion. Those remain the next production-execution work items, using the saved production order and its finalized engineering revision as references.

## Verification notes

Static checks on the prepared patch: TypeScript syntax transpilation passed for `orders.service.ts` and `orders.controller.ts`; the XAML parses as XML; all new XAML control names match their `FindControl` references; and the catalog migration has 24 seeded standard items. Full API and desktop builds plus in-app and PDF visual acceptance are still to be run on the project's Windows machine.
