# Sprint 7 — Engineering outputs, board/color metadata, and cut-list pieces

## Purpose

Update the existing **النسخ الفنية وقائمة الخامات / القص** workflow to record the real engineer deliverables without changing the production work-order screen or PDF generator.

## What changes

- Engineering attachments are categorized as product reference image, dimensioned drawing, cut-list layout, BOM export, or other.
- A cut-list layout stores its material/board code, color/finish, board thickness, board length and width, and the number of boards shown by the engineer's cut-list output.
- Piece-level cut-list records can be entered against a specific uploaded cut-list file: part name, length, width, optional piece thickness, quantity, and notes.
- BOM/material entries continue to use the existing engineering line workflow.
- Finalization accepts either existing BOM/material rows or a dimensioned drawing/cut-list output. A product-reference image by itself is not enough to finalize a technical revision.
- Existing attachments are kept and default to type `other`. Existing BOM rows, production orders, work-order records, and PDF generation are not rewritten.

The ERP records the dimensions and board quantities supplied by the engineer/operator. **This sprint does not attempt to parse dimensions from images or re-optimize nesting/cutting.** The user records the values shown in the engineer's output.

## Apply the database migration

1. Back up PostgreSQL using the same backup procedure already used for Sprint 5 and Sprint 6.
2. From the repository root, confirm `database/postgres/migrations/007_engineering_outputs.sql` exists.
3. Apply migration 007 once with the project's PostgreSQL container. The migration is additive and is designed to be safe to rerun.
4. Do not rerun migrations 005 or 006 and do not reset the database volume.

Example, from PowerShell in the repository root:

```powershell
docker cp .\database\postgres\migrations\007_engineering_outputs.sql wood-factory-erp-postgres:/tmp/007_engineering_outputs.sql
if ($LASTEXITCODE -ne 0) { throw "Could not copy migration 007." }
docker exec wood-factory-erp-postgres sh -lc 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /tmp/007_engineering_outputs.sql'
if ($LASTEXITCODE -ne 0) { throw "Migration 007 failed. Stop and inspect the error." }
```

## Build

```powershell
Set-Location "C:\Users\HP\source\wood-factory-erp\apps\api"
npm.cmd run build

Set-Location "C:\Users\HP\source\wood-factory-erp\apps\desktop\WoodFactoryErp.Desktop"
dotnet build
```

Check `$LASTEXITCODE` immediately after each build. Do not print a success message unless the build exit code is zero.

## Manual acceptance checklist

1. Open an existing customer order and create a draft engineering revision.
2. Upload the finished-product image as **صورة المنتج المرجعية**; verify it appears in the attachments list and downloads correctly.
3. Upload the dimensioned SketchUp drawing as **الرسم الهندسي بالأبعاد**.
4. Upload one cut-list image as **مخطط الكاتلست**, entering board code/type, color, thickness, board length, board width and board count exactly as shown in the cut-list output.
5. Upload another cut-list with a different color/material specification and verify it remains a separate entry.
6. Select a cut-list layout and add two or more piece rows, testing length, width, optional thickness, quantity and notes.
7. Verify the displayed parts list identifies the source cut-list, color/material, board size, board count, dimensions, and quantity.
8. Add BOM/accessory lines using the existing material-line form.
9. Finalize the revision. Confirm that adding files or parts to this finalized revision is blocked; create a new revision for further changes.
10. Open the existing job-order tab and verify the work-order save/PDF flow still works. This patch does not modify the work-order PDF generator.

## Scope boundaries

- Attachment and cut-list data belongs to its engineering revision; finalized revisions remain immutable.
- Board/piece quantities are recorded as supplied. No automatic calculation of board yield, sheet optimization, OCR, or dimension extraction is included.
- The existing work-order form/PDF is intentionally unchanged in this patch.
