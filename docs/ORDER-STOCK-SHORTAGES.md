# Order stock availability — 18 November 2026

Confirming a customer order now returns the saved material and accessory requirements with available and missing quantities. A shortage does not reject the order. The desktop displays the result immediately after confirmation and the inventory screen lists required replenishment totals and their source order code, ID, customer and item. Refreshing reads current data; it does not insert alerts, purchase orders, reservations or inventory movements.

The material and accessory pickers each offer an inline item-creation dialog. It uses the existing master-data endpoint and permissions, collects code, Arabic/English names, item type, unit, classification and stock tracking, then selects the saved item without clearing the order draft. Order requirements use the materials classification. A newly created stock item with no receipts has zero available stock.

## Calculation

- Sum item requirements once per customer order across material/accessory lines.
- Deduct usable issues across that order's linked, non-cancelled production jobs: original issues minus returns and recorded actual waste. Subtract this from the required quantity, with a minimum of zero.
- Read physical stock from active raw-material warehouses. A missing balance is zero.
- Allocate that stock as a planning projection across open orders by order date, creation time and ID. Deduct earlier outstanding demand before calculating each order's shortage. Quantities use PostgreSQL NUMERIC; API quantities are decimal strings and desktop totals use decimal.
- Serialize order confirmations with a transaction advisory lock before inserting requirements so simultaneous confirmations see preceding committed demand. The result is a point-in-time forecast; subsequent backdated orders, receipts, issues, returns or waste can change it.
- Non-stock/non-material items are explicitly marked as unchecked. Historical board totals without item identities are flagged for review rather than treated as covered. The entered quantities are whole-order requirements; the waste percentage is not silently added again.

Inventory shortages refresh when inventory is loaded, after receiving stock, by the refresh button and every ten seconds while the inventory panel is visible. This is a procurement planning list; purchasing and physical stock movements still use their existing workflows.

## Inventory usability patch

Per-item shortage totals remain visible. Each order's detailed requirements, unchecked-item notes and historical untyped-board details start collapsed behind an Arabic show/hide button. Expansion choices survive automatic refresh and search changes during the current window session.

A single compact search row filters inventory balances, item identities (including English names and items without balances), shortages and linked orders. Multiple words can match different fields. Clear restores the complete summary. Search does not change any quantities or write data. For movements, the authenticated API searches the complete ledger before returning the newest 250 matching records; the UI states this limit. It includes item identity/type, warehouse, supplier, reference, date, notes and quantity/cost fields. Literal `%`, `_` and backslash characters are escaped and all search values are bound parameters. Typing is debounced; stale responses cannot replace a newer query's results.

The inventory search patch adds no migration. Rebuild/restart both API and desktop. Both builds pass (desktop: 23 existing warnings, zero errors); 24 focused search/order/availability regressions and all three extended `OrderWorkspaceSmoke` runtime checks pass. Use `DesktopVerificationOutput=InventorySearchVerification` for the latest desktop verification output. Connected PostgreSQL search acceptance remains pending.

## Activation and verification

No new migration is added. Existing migrations through 014 are required for the order material tables. The owner has already applied finance migration 013; do not rerun it for this patch. Rebuild and restart the API and desktop to use the updated code.

Verified locally: API build, desktop build (23 existing warnings, zero errors), 30 focused Node regression tests, and both Avalonia runtime workspace checks pass. The opt-in PostgreSQL integration suite is skipped without `ORDER_STOCK_TEST_DATABASE_URL`. Docker/database access was unavailable here, so SQL execution and concurrent connected acceptance remain unverified.

Commands from the repository root:

```powershell
npm.cmd --prefix apps/api run build
node --test tests/complete-orders.test.cjs tests/order-job-sheets.test.cjs tests/order-stock-availability.test.cjs tests/order-stock-postgres.test.cjs
dotnet build apps/desktop/WoodFactoryErp.Desktop/WoodFactoryErp.Desktop.csproj --no-restore -o apps/desktop/WoodFactoryErp.Desktop/bin/OrderStockVerification
dotnet run --project tests/OrderWorkspaceSmoke/OrderWorkspaceSmoke.csproj --no-restore -p:DesktopVerificationOutput=OrderStockVerification
```

For real SQL acceptance, set `ORDER_STOCK_TEST_DATABASE_URL` to a disposable test database only, then run the Node command. The test creates and drops its own schema and covers shared stock, accessories, no-balance items, non-stock warnings, material issues, returns, waste, replenishment and repeated read-only refreshes.

Connected UI acceptance still required: create a missing item in the popup; confirm an order with that item and another partially stocked item; verify exact deficits against the same order code in inventory; receive stock and refresh; verify deficits decrease; confirm competing orders and verify stock is not projected twice. Check that popup cancellation and failed item saves preserve the order form.
