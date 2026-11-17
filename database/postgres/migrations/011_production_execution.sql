BEGIN;
INSERT INTO roles(code,name) VALUES('production_manager','Production Manager'),('production_operator','Production Operator') ON CONFLICT(code) DO NOTHING;
ALTER TABLE production_orders DROP CONSTRAINT IF EXISTS production_orders_status_check;
ALTER TABLE production_orders ADD CONSTRAINT production_orders_status_check
  CHECK (status IN ('draft','open','in_progress','on_hold','ready_for_costing','completed','cancelled'));
ALTER TABLE production_orders ADD COLUMN IF NOT EXISTS execution_started_date DATE;

CREATE TABLE IF NOT EXISTS accounting_periods (
  month DATE PRIMARY KEY CHECK (month = date_trunc('month', month)::date),
  status TEXT NOT NULL DEFAULT 'open' CHECK (status IN ('open','closed'))
);
CREATE TABLE IF NOT EXISTS production_operation_catalog (
  code TEXT PRIMARY KEY, name_ar TEXT NOT NULL, sequence INTEGER NOT NULL, active BOOLEAN NOT NULL DEFAULT true
);
INSERT INTO production_operation_catalog(code,name_ar,sequence) VALUES
  ('cutting','التقطيع',10),('ashareet','الشريط / أشريطة',20),('drilling','التخريم',30),
  ('assembly','التجميع',40),('accessories','تركيب الإكسسوارات',50),('painting','الدهان والتشطيب',60),
  ('upholstery','التنجيد',70),('packaging','التعبئة والتغليف',80),('quality_control','الفحص والقبول',90)
ON CONFLICT(code) DO NOTHING;
CREATE TABLE IF NOT EXISTS production_operations (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(), production_order_id UUID NOT NULL REFERENCES production_orders(id),
  operation_code TEXT NOT NULL REFERENCES production_operation_catalog(code), name_ar TEXT NOT NULL,
  sequence INTEGER NOT NULL, completed_quantity NUMERIC(18,4) NOT NULL DEFAULT 0 CHECK(completed_quantity>=0),
  UNIQUE(production_order_id,operation_code), UNIQUE(production_order_id,sequence), UNIQUE(id,production_order_id)
);
CREATE TABLE IF NOT EXISTS production_execution_events (
  operation_id UUID PRIMARY KEY, production_order_id UUID NOT NULL REFERENCES production_orders(id),
  event_type TEXT NOT NULL, payload JSONB NOT NULL, payload_hash TEXT NOT NULL, result JSONB NOT NULL,
  actor_id UUID NOT NULL REFERENCES users(id), work_date DATE NOT NULL, notes TEXT,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_execution_events_order ON production_execution_events(production_order_id,created_at);
CREATE TABLE IF NOT EXISTS production_acceptances (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(), production_order_id UUID NOT NULL REFERENCES production_orders(id),
  operation_id UUID NOT NULL UNIQUE REFERENCES production_execution_events(operation_id) DEFERRABLE INITIALLY DEFERRED,
  quantity NUMERIC(18,4) NOT NULL CHECK(quantity>0), accepted_date DATE NOT NULL,
  status TEXT NOT NULL DEFAULT 'pending_costing' CHECK(status IN ('pending_costing','transferred'))
);
CREATE TABLE IF NOT EXISTS production_material_postings (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(), production_order_id UUID NOT NULL REFERENCES production_orders(id),
  stage_id UUID REFERENCES production_operations(id), inventory_movement_id UUID NOT NULL UNIQUE REFERENCES inventory_movements(id),
  original_issue_id UUID REFERENCES production_material_postings(id),
  item_id UUID NOT NULL REFERENCES items(id), warehouse_id UUID NOT NULL REFERENCES warehouses(id),
  quantity NUMERIC(18,4) NOT NULL CHECK(quantity>0), unit_cost NUMERIC(18,4) NOT NULL CHECK(unit_cost>=0),
  amount NUMERIC(18,4) NOT NULL CHECK(amount>=0), posting_date DATE NOT NULL,
  CHECK(original_issue_id IS NULL OR original_issue_id <> id)
);
CREATE INDEX IF NOT EXISTS ix_production_material_order ON production_material_postings(production_order_id);
-- Observed waste belongs to an existing issue. Its cost is already in that
-- issue; this observation must not create another stock or cost charge.
CREATE TABLE IF NOT EXISTS production_material_waste (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(), production_order_id UUID NOT NULL REFERENCES production_orders(id),
  original_issue_id UUID NOT NULL REFERENCES production_material_postings(id),
  quantity NUMERIC(18,4) NOT NULL CHECK(quantity>0), reason TEXT NOT NULL,
  observation_date DATE NOT NULL
);
CREATE TABLE IF NOT EXISTS production_labor_rates (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(), code TEXT NOT NULL, stage_code TEXT NOT NULL REFERENCES production_operation_catalog(code),
  name_ar TEXT NOT NULL, rate NUMERIC(18,4) NOT NULL CHECK(rate>=0),
  treatment TEXT NOT NULL CHECK(treatment IN ('direct','pooled')),
  effective_from DATE NOT NULL, effective_to DATE, approved_by UUID NOT NULL REFERENCES users(id),
  CHECK(effective_to IS NULL OR effective_to>=effective_from), UNIQUE(code,effective_from)
);
CREATE TABLE IF NOT EXISTS production_labor_entries (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(), production_order_id UUID NOT NULL REFERENCES production_orders(id),
  stage_id UUID NOT NULL REFERENCES production_operations(id), rate_id UUID NOT NULL REFERENCES production_labor_rates(id),
  rate_code TEXT NOT NULL, worker_name TEXT NOT NULL, quantity NUMERIC(18,4) NOT NULL CHECK(quantity>0),
  rate_snapshot NUMERIC(18,4) NOT NULL, treatment TEXT NOT NULL CHECK(treatment IN ('direct','pooled')),
  amount NUMERIC(18,4) NOT NULL CHECK(amount>=0), posting_date DATE NOT NULL
);
CREATE TABLE IF NOT EXISTS production_labor_rate_audit (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(), rate_id UUID NOT NULL REFERENCES production_labor_rates(id),
  actor_id UUID NOT NULL REFERENCES users(id), details JSONB NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TABLE IF NOT EXISTS production_cost_entries (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(), production_order_id UUID NOT NULL REFERENCES production_orders(id),
  source_type TEXT NOT NULL CHECK(source_type IN ('material_issue','material_return','labor','consumable')),
  source_id UUID NOT NULL, item_id UUID REFERENCES items(id), amount NUMERIC(18,4) NOT NULL,
  treatment TEXT NOT NULL CHECK(treatment IN ('direct','pooled')), posting_date DATE NOT NULL, description TEXT,
  UNIQUE(source_type,source_id)
);
CREATE INDEX IF NOT EXISTS ix_production_cost_date ON production_cost_entries(production_order_id,posting_date);
CREATE TABLE IF NOT EXISTS production_consumable_costs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(), production_order_id UUID NOT NULL REFERENCES production_orders(id),
  item_id UUID NOT NULL REFERENCES items(id), document_reference TEXT NOT NULL, amount NUMERIC(18,4) NOT NULL CHECK(amount>0),
  posting_date DATE NOT NULL, UNIQUE(production_order_id,item_id,document_reference)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_production_consumable_source ON production_consumable_costs
  (production_order_id,item_id,lower(btrim(document_reference)));
ALTER TABLE inventory_movements DROP CONSTRAINT IF EXISTS inventory_movements_movement_type_check;
ALTER TABLE inventory_movements ADD CONSTRAINT inventory_movements_movement_type_check
  CHECK(movement_type IN ('receipt','production_issue','production_return'));
DROP INDEX IF EXISTS ux_inventory_receipt_reference_supplier;
CREATE UNIQUE INDEX ux_inventory_receipt_reference_supplier ON inventory_movements
  (COALESCE(party_id,'00000000-0000-0000-0000-000000000000'::uuid),lower(btrim(document_reference)),item_id)
  WHERE movement_type='receipt';
COMMIT;
