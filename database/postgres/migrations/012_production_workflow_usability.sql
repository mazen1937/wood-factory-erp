BEGIN;
-- Owner-confirmed route. Historical stages/quantities/costs are retained.
UPDATE production_operation_catalog SET active=false WHERE code IN ('drilling','painting','upholstery','accessories');
UPDATE production_operation_catalog SET name_ar='التجميع وتركيب الإكسسوارات' WHERE code='assembly';
ALTER TABLE production_operations ADD COLUMN IF NOT EXISTS retired_from_route BOOLEAN NOT NULL DEFAULT false;
UPDATE production_operations SET retired_from_route=true WHERE operation_code IN ('drilling','painting','upholstery','accessories');

CREATE TABLE IF NOT EXISTS production_packaging_costs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(), production_order_id UUID NOT NULL UNIQUE REFERENCES production_orders(id),
  amount NUMERIC(18,4) NOT NULL CHECK(amount>0), posting_date DATE NOT NULL, description TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS production_wage_agreements (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(), production_order_id UUID NOT NULL REFERENCES production_orders(id),
  stage_id UUID NOT NULL REFERENCES production_operations(id), worker_name TEXT NOT NULL CHECK(length(btrim(worker_name))>0),
  basis TEXT NOT NULL CHECK(basis IN ('unit','daily','weekly','monthly')),
  rate NUMERIC(18,4) NOT NULL CHECK(rate>0), assigned_quantity NUMERIC(18,4),
  accrued_quantity NUMERIC(18,4) NOT NULL DEFAULT 0 CHECK(accrued_quantity>=0),
  period_start DATE NOT NULL, period_end DATE,
  treatment TEXT NOT NULL CHECK(treatment IN ('direct','pooled')),
  approved_by UUID NOT NULL REFERENCES users(id), created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  CHECK((basis='unit' AND assigned_quantity IS NOT NULL AND assigned_quantity>0 AND period_end IS NULL AND accrued_quantity<=assigned_quantity)
    OR (basis<>'unit' AND assigned_quantity IS NULL AND period_end IS NOT NULL AND period_end>=period_start AND accrued_quantity<=1))
);
CREATE INDEX IF NOT EXISTS ix_wage_agreements_job ON production_wage_agreements(production_order_id,stage_id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_wage_agreement_period ON production_wage_agreements
  (production_order_id,stage_id,lower(btrim(worker_name)),basis,period_start);
CREATE TABLE IF NOT EXISTS production_wage_accruals (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(), agreement_id UUID NOT NULL REFERENCES production_wage_agreements(id),
  operation_id UUID NOT NULL REFERENCES production_execution_events(operation_id) DEFERRABLE INITIALLY DEFERRED,
  quantity NUMERIC(18,4) NOT NULL CHECK(quantity>0), amount NUMERIC(18,4) NOT NULL CHECK(amount>=0),
  posting_date DATE NOT NULL, UNIQUE(agreement_id,operation_id)
);
CREATE TABLE IF NOT EXISTS production_quality_checks (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(), production_order_id UUID NOT NULL REFERENCES production_orders(id),
  operation_id UUID NOT NULL UNIQUE REFERENCES production_execution_events(operation_id) DEFERRABLE INITIALLY DEFERRED,
  quantity NUMERIC(18,4) NOT NULL CHECK(quantity>0), decision TEXT NOT NULL CHECK(decision IN ('accepted','rejected')),
  reinspection BOOLEAN NOT NULL DEFAULT false, reason TEXT, inspection_date DATE NOT NULL,
  CHECK(decision<>'rejected' OR length(btrim(COALESCE(reason,'')))>0)
);
ALTER TABLE production_cost_entries DROP CONSTRAINT IF EXISTS production_cost_entries_source_type_check;
ALTER TABLE production_cost_entries ADD CONSTRAINT production_cost_entries_source_type_check
  CHECK(source_type IN ('material_issue','material_return','labor','consumable','packaging','wage_accrual'));
COMMIT;
