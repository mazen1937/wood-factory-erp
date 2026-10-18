-- Sprint 5: customer orders, immutable engineering revisions, cut-list/BOM rows,
-- attachment metadata, and draft production orders linked to a stable revision.
-- Safe to re-run; this migration does not alter existing inventory records.

CREATE TABLE IF NOT EXISTS customer_orders (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  order_no TEXT NOT NULL UNIQUE CHECK (length(btrim(order_no)) BETWEEN 1 AND 60),
  customer_id UUID NOT NULL REFERENCES parties(id),
  order_date DATE NOT NULL DEFAULT CURRENT_DATE,
  requested_delivery_date DATE,
  status TEXT NOT NULL DEFAULT 'open' CHECK (status IN ('open', 'cancelled')),
  notes TEXT,
  created_by UUID REFERENCES users(id),
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  CHECK (requested_delivery_date IS NULL OR requested_delivery_date >= order_date)
);
CREATE INDEX IF NOT EXISTS ix_customer_orders_customer_date
  ON customer_orders(customer_id, order_date DESC);

CREATE TABLE IF NOT EXISTS engineering_packages (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  customer_order_id UUID NOT NULL REFERENCES customer_orders(id),
  revision INTEGER NOT NULL CHECK (revision > 0),
  technical_reference TEXT NOT NULL CHECK (length(btrim(technical_reference)) BETWEEN 1 AND 160),
  notes TEXT,
  status TEXT NOT NULL DEFAULT 'draft' CHECK (status IN ('draft', 'finalized', 'superseded')),
  created_by UUID REFERENCES users(id),
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  finalized_at TIMESTAMPTZ,
  UNIQUE(customer_order_id, revision),
  UNIQUE(id, customer_order_id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_engineering_packages_one_finalized_per_order
  ON engineering_packages(customer_order_id) WHERE status = 'finalized';
CREATE INDEX IF NOT EXISTS ix_engineering_packages_order_revision
  ON engineering_packages(customer_order_id, revision DESC);

CREATE TABLE IF NOT EXISTS engineering_material_lines (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  package_id UUID NOT NULL REFERENCES engineering_packages(id),
  line_no INTEGER NOT NULL CHECK (line_no > 0),
  line_type TEXT NOT NULL CHECK (line_type IN ('bom', 'cut_list')),
  description TEXT NOT NULL CHECK (length(btrim(description)) BETWEEN 1 AND 240),
  material_reference TEXT,
  quantity NUMERIC(18,4) NOT NULL CHECK (quantity > 0),
  unit_code TEXT NOT NULL CHECK (length(btrim(unit_code)) BETWEEN 1 AND 40),
  dimensions TEXT,
  notes TEXT,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE(package_id, line_no)
);
CREATE INDEX IF NOT EXISTS ix_engineering_material_lines_package
  ON engineering_material_lines(package_id, line_no);

CREATE TABLE IF NOT EXISTS engineering_package_files (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  package_id UUID NOT NULL REFERENCES engineering_packages(id),
  file_name TEXT NOT NULL CHECK (length(btrim(file_name)) BETWEEN 1 AND 255),
  mime_type TEXT NOT NULL,
  size_bytes BIGINT NOT NULL CHECK (size_bytes > 0),
  storage_key TEXT NOT NULL UNIQUE,
  checksum_sha256 TEXT NOT NULL CHECK (length(checksum_sha256) = 64),
  uploaded_by UUID REFERENCES users(id),
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_engineering_package_files_package
  ON engineering_package_files(package_id, created_at);

CREATE TABLE IF NOT EXISTS production_orders (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  order_no TEXT NOT NULL UNIQUE CHECK (length(btrim(order_no)) BETWEEN 1 AND 60),
  production_mode TEXT NOT NULL CHECK (production_mode IN ('customer_order', 'stock')),
  customer_order_id UUID REFERENCES customer_orders(id),
  engineering_package_id UUID,
  product_code TEXT NOT NULL CHECK (length(btrim(product_code)) BETWEEN 1 AND 80),
  planned_quantity NUMERIC(18,4) NOT NULL CHECK (planned_quantity > 0),
  status TEXT NOT NULL DEFAULT 'draft' CHECK (status IN ('draft', 'open', 'in_progress', 'completed', 'cancelled')),
  created_by UUID REFERENCES users(id),
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  FOREIGN KEY (engineering_package_id, customer_order_id)
    REFERENCES engineering_packages(id, customer_order_id),
  CHECK (
    (production_mode = 'customer_order' AND customer_order_id IS NOT NULL AND engineering_package_id IS NOT NULL)
    OR (production_mode = 'stock' AND customer_order_id IS NULL)
  )
);
CREATE INDEX IF NOT EXISTS ix_production_orders_customer_order
  ON production_orders(customer_order_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_production_orders_engineering_package
  ON production_orders(engineering_package_id);
