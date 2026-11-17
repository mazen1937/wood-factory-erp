# Complete customer order workflow — 17 November 2026

The current order/job screen is described in `ORDER-WORKSPACE-PATCH.md` (migration 014). It replaces untyped board totals with material item lines, makes details opt-in and attachments clickable, and adds searchable named child jobs. The notes below describe the preceding version.

The order form now collects one product and one customer per order. Enter the order code/name, product code and name, order date, optional delivery date, and optional overall description.

Choose an existing customer or use **إضافة عميل**. The shortcut opens the customers section of master data. Saving the new customer returns to the order and selects that customer, preserving the draft fields, attachments, and accessories in memory.

For every attachment, enter a name, optional description, optional wood color, and a type (cut-list, product image, other). Add as many separate cut-lists as needed within the upload limits: 100 files, 20 MB per file, 100 MB total. Images, PDFs, spreadsheets, and design files are supported. A draft attachment can be removed before confirmation.

Enter the **total number of wooden boards for this order**, then choose accessories from active master-data accessory items and enter each item's **total quantity for this order**. Adding the same accessory again replaces its quantity; it does not duplicate the line. Lines can be removed before confirmation. Enter the engineer's waste percentage once for the entire order (0–100, up to two decimal places). Board count is a whole number, including zero. Waste is recorded separately and is not automatically added to the board count.

**تأكيد الطلب** sends the complete form and file contents to `POST /api/orders/confirm` as multipart data. Customer validation, accessory validation, order insertion, accessory insertion, and attachment metadata/content insertion occur in one PostgreSQL transaction. Failure rolls back all database changes. The form is cleared only after a successful response. Nothing from this draft is persisted before confirmation. Drafts do not survive closing the app.

Select a saved order to view product identity, customer, board total, waste, description, all accessories with their quantities, and all attachments together. Image files have previews; every attachment has a download button and its name, description, and optional color.

## Migration and verification

Back up the existing PostgreSQL database using the established project procedure, then apply `database/postgres/migrations/009_complete_customer_orders.sql` with `psql -X -v ON_ERROR_STOP=1`. Do not reset the volume. Migration 009 is transactional and additive; existing orders retain NULL product totals instead of invented historical values. It creates order-level accessory and attachment tables. Files are stored as PostgreSQL BYTEA so they commit or roll back with the order and are included in database backups.

The earlier uncommitted engineering redesign (migration 008) remains in the API for historical records, but its tab is removed from the visible desktop workflow. Migration 009 does not apply migration 008. Confirming an order does not create a production order. Product codes identify the product on an order; order codes remain unique. The same product code can be used for repeat customer orders.

## Job sheet and A3 PDF

Apply `010_order_based_job_sheets.sql` after backing up the database. It removes the old requirement that every customer production order have an engineering revision, while retaining customer-order references and historical engineering foreign keys. The API allows revision-free creation only for confirmed, open customer orders. Historical orders can automatically reuse their finalized revision. Cancelled orders cannot create new job sheets.

Keep using **الطلب المحدد** at the top. In **أمر الشغل والتجهيز**, product name/code are prefilled from the order. Enter the job number, date and planned finished-product quantity, then click **إنشاء أمر الشغل**. This loads the existing work-order checklist, quantity fields and custom rows. Check the required items, enter quantities, save, then use **تصدير أمر الشغل PDF A3**. The existing A3 PDF generator and layout are unchanged. When an order has no job sheet yet, an explanatory message replaces the empty editor. Switching customer orders clears the prior checklist and only shows job sheets belonging to the selected order.

Regression checks: `node --test tests/complete-orders.test.cjs tests/order-job-sheets.test.cjs` (15 passing tests with simulated database clients). The API build and isolated desktop build pass. The normal desktop output was locked by the running app; close the app before rebuilding it. Migration 010 and a live create/checklist/save/export flow remain to be verified manually.

Build and run regression checks from the repository root:

```powershell
Push-Location apps/api
npm.cmd run build
Pop-Location
node --test tests/complete-orders.test.cjs
dotnet build apps/desktop/WoodFactoryErp.Desktop/WoodFactoryErp.Desktop.csproj --no-restore
```

Manual acceptance after migration: create a customer through the shortcut, add two cut-lists (one without color) and a product image, add/update/remove accessories, enter board total/waste/description, confirm, refresh, and verify image previews/downloads and all totals. Try a duplicate order code and an inactive accessory to verify that no incomplete order is created and the draft is preserved on error.

Local API and desktop builds passed. Transaction/validation regression tests use a simulated database client; they do not establish that the live schema is migrated. Live migration and end-to-end verification could not be performed because both Docker access and the PostgreSQL connection were denied in this environment.
