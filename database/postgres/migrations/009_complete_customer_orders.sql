-- Complete order confirmation. Existing orders remain valid with NULL product totals.
BEGIN;
ALTER TABLE customer_orders
  ADD COLUMN IF NOT EXISTS product_code TEXT CHECK (product_code IS NULL OR length(btrim(product_code)) BETWEEN 1 AND 80),
  ADD COLUMN IF NOT EXISTS product_name TEXT CHECK (product_name IS NULL OR length(btrim(product_name)) BETWEEN 1 AND 240),
  ADD COLUMN IF NOT EXISTS total_boards INTEGER CHECK (total_boards >= 0),
  ADD COLUMN IF NOT EXISTS waste_percentage NUMERIC(5,2) CHECK (waste_percentage BETWEEN 0 AND 100),
  ADD COLUMN IF NOT EXISTS confirmed_at TIMESTAMPTZ;

CREATE TABLE IF NOT EXISTS customer_order_accessories (
  order_id UUID NOT NULL REFERENCES customer_orders(id),
  item_id UUID NOT NULL REFERENCES items(id),
  quantity NUMERIC(18,4) NOT NULL CHECK (quantity > 0),
  PRIMARY KEY (order_id, item_id)
);

-- Content and metadata commit/rollback together with the order.
CREATE TABLE IF NOT EXISTS customer_order_attachments (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  order_id UUID NOT NULL REFERENCES customer_orders(id),
  attachment_type TEXT NOT NULL CHECK (attachment_type IN ('cut_list', 'product_reference', 'other')),
  name TEXT NOT NULL CHECK (length(btrim(name)) BETWEEN 1 AND 240),
  description TEXT,
  color TEXT,
  file_name TEXT NOT NULL,
  mime_type TEXT NOT NULL,
  content BYTEA NOT NULL CHECK (octet_length(content) BETWEEN 1 AND 20971520),
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_customer_order_attachments_order ON customer_order_attachments(order_id, created_at);
COMMIT;
