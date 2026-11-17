# Order workspace patch — 17 November 2026

## Delivered behavior

- Two visible tabs: **إنشاء وعرض الطلبات** and **أوامر الشغل وملف PDF**, organized into bordered cards matching production.
- Synchronized parent-order searches by code, name or customer, both above the tabs and inside the job tab.
- Several named/coded job orders can belong to one customer order and the same product. Only the selected parent's jobs appear in the searchable job selector.
- New jobs inherit product identity on the server. The create-job form asks for the job's own code/name, quantity, start and optional end date. Start/end default from the parent order/delivery date and can be adjusted.
- Creation inputs and saved-job checklist/PDF inputs are separate, so selecting an existing job does not overwrite a new-job draft. Each saved job retains its own checklist and the existing A3 PDF layout.
- Order details appear only after **عرض التفاصيل**. Hide them with **إخفاء التفاصيل**; selecting another parent also hides previous details. Stale detail responses are ignored.
- Details show the customer, identity, schedule, materials/accessories, waste, description, related-job links and clickable attachments. No thumbnails or file-content downloads occur while browsing details. Clicking a file opens it in the associated computer viewer. The same immutable cached file can reopen without overwriting a viewer-locked copy.
- The generic board-total input is replaced by searchable raw-material/consumable lines, each with quantity and base unit. Accessories are separate and searchable. Repeated item entry updates the existing line; draft lines can be removed. Unit precision and strict decimal-point input are validated.
- Order quantities are planned requirements. Confirmation does not issue stock or create production costs.

The creation screen uses **اسم الطلب / المنتج** as its order/product name. The API stores `order_name` separately and can accept a different explicit name. Existing names fall back to product name or code. Historical untyped board totals remain labeled historical/unmapped; no material type is guessed.

## Database and compatibility

Migration **014_order_workspace.sql** adds `customer_orders.order_name`, `customer_order_materials`, `production_orders.job_name` and `production_orders.planned_end_date`. It preserves existing IDs, parent links, attachments, checklists, costs and inventory. Existing job codes become their initial names. A finish date cannot precede its start.

Legacy clients may still send the optional `totalBoards`; new material lines cannot accompany a positive untyped total. Confirmed parent identity overrides conflicting client product fields. Historical orders may derive identity from a finalized engineering revision.

014 needs the existing master-data/order migrations through 010. **Finance migration 013 is not required for this patch** and can remain deferred. Do not reset the database or replay financial postings.

## Activation

Close the desktop, stop the API and take a fresh database backup. Run from the repository root:

```powershell
Set-Location "C:\Users\HP\source\wood-factory-erp"
docker cp .\database\postgres\migrations\014_order_workspace.sql wood-factory-erp-postgres:/tmp/014.sql
if ($LASTEXITCODE -ne 0) { throw "Could not copy migration 014." }
docker exec wood-factory-erp-postgres sh -lc 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /tmp/014.sql'
if ($LASTEXITCODE -ne 0) { throw "Migration 014 failed. Stop and inspect the error." }
```

Build/start the API in one window:

```powershell
Set-Location "C:\Users\HP\source\wood-factory-erp\apps\api"
npm.cmd run build
if ($LASTEXITCODE -ne 0) { throw "API build failed." }
npm.cmd run start
```

In another window:

```powershell
Set-Location "C:\Users\HP\source\wood-factory-erp\apps\desktop\WoodFactoryErp.Desktop"
dotnet run
```

014 has not been applied by the agent. Connected saving, external viewer associations and live A3 download still require local acceptance.

## Verification and acceptance

The API and desktop builds pass. Order/job tests cover atomic material saving/rollback, duplicate items, unit precision, inherited identity, multiple child jobs and schedule validation. The runtime Avalonia check constructs the actual window without a server/session and verifies both visible tabs, cards, search labels, hidden details, inherited schedule, separate creation inputs, material updates and decimal-comma rejection.

```powershell
node --test tests/complete-orders.test.cjs tests/order-job-sheets.test.cjs
dotnet build apps/desktop/WoodFactoryErp.Desktop/WoodFactoryErp.Desktop.csproj -o apps/desktop/WoodFactoryErp.Desktop/bin/OrderWorkspaceVerification
dotnet run --project tests/OrderWorkspaceSmoke/OrderWorkspaceSmoke.csproj -p:NuGetAudit=false
```

Connected checks:

1. Create an order with two material types, accessories, image/PDF attachments and a delivery date. Refresh and locate it by either code or name.
2. Confirm details remain hidden until requested. Show them, open attachments, hide them, and change orders; previous files must disappear.
3. Select the parent in the job tab. Check inherited identity/dates; create two differently named/coded jobs for the same product.
4. Search/select each child, save different checklist quantities and export each A3 PDF. Switching children must restore the correct checklist without changing the new-job draft.
5. Change parent: the previous parent's jobs must not appear. A related-job link in shown details must open the correct job editor.
6. Invalid materials, duplicate job codes, incompatible unit precision and finish-before-start schedules must reject without partial records.
