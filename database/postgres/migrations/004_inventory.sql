-- Inventory receipt slice. PostgreSQL remains the only authority for posted stock.
CREATE TABLE IF NOT EXISTS inventory_item_costs (
  item_id UUID PRIMARY KEY REFERENCES items(id),
  quantity_on_hand NUMERIC(18,4) NOT NULL DEFAULT 0 CHECK (quantity_on_hand >= 0),
  inventory_value NUMERIC(18,4) NOT NULL DEFAULT 0 CHECK (inventory_value >= 0),
  average_unit_cost NUMERIC(18,4) NOT NULL DEFAULT 0 CHECK (average_unit_cost >= 0),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS inventory_balances (
  item_id UUID NOT NULL REFERENCES items(id),
  warehouse_id UUID NOT NULL REFERENCES warehouses(id),
  quantity_on_hand NUMERIC(18,4) NOT NULL DEFAULT 0 CHECK (quantity_on_hand >= 0),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  PRIMARY KEY (item_id, warehouse_id)
);

CREATE TABLE IF NOT EXISTS inventory_movements (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  operation_id UUID NOT NULL UNIQUE,
  movement_type TEXT NOT NULL CHECK (movement_type IN ('receipt')),
  item_id UUID NOT NULL REFERENCES items(id),
  warehouse_id UUID NOT NULL REFERENCES warehouses(id),
  party_id UUID REFERENCES parties(id),
  document_reference TEXT NOT NULL,
  movement_date DATE NOT NULL,
  quantity NUMERIC(18,4) NOT NULL CHECK (quantity > 0),
  unit_cost NUMERIC(18,4) NOT NULL CHECK (unit_cost >= 0),
  total_cost NUMERIC(18,4) NOT NULL CHECK (total_cost >= 0),
  average_cost_after NUMERIC(18,4) NOT NULL CHECK (average_cost_after >= 0),
  quantity_after NUMERIC(18,4) NOT NULL CHECK (quantity_after >= 0),
  value_after NUMERIC(18,4) NOT NULL CHECK (value_after >= 0),
  notes TEXT,
  posted_by UUID,
  posted_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_inventory_movements_item_date ON inventory_movements(item_id, movement_date, posted_at);
CREATE INDEX IF NOT EXISTS ix_inventory_movements_warehouse_date ON inventory_movements(warehouse_id, movement_date, posted_at);

-- Avoid accidentally posting the same supplier document twice.
CREATE UNIQUE INDEX IF NOT EXISTS ux_inventory_receipt_reference_supplier
  ON inventory_movements (COALESCE(party_id, '00000000-0000-0000-0000-000000000000'::uuid), lower(btrim(document_reference)), item_id);
