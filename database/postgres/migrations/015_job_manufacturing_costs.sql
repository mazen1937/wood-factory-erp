BEGIN;
CREATE TABLE production_workers (
 id UUID PRIMARY KEY, code TEXT NOT NULL CHECK(length(btrim(code)) BETWEEN 1 AND 60),
 name_ar TEXT NOT NULL CHECK(length(btrim(name_ar)) BETWEEN 1 AND 160), name_en TEXT,
 active BOOLEAN NOT NULL DEFAULT true, created_by UUID NOT NULL REFERENCES users(id), created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX ux_production_workers_code ON production_workers(lower(btrim(code)));
ALTER TABLE production_orders ADD COLUMN cost_focused BOOLEAN NOT NULL DEFAULT false;
CREATE TABLE production_work_costs (
 id UUID PRIMARY KEY DEFAULT gen_random_uuid(), operation_id UUID NOT NULL UNIQUE REFERENCES production_execution_events(operation_id) DEFERRABLE INITIALLY DEFERRED,
 production_order_id UUID NOT NULL REFERENCES production_orders(id),
 stage_code TEXT NOT NULL CHECK(stage_code IN ('cutting','ashareet','assembly')),
 worker_id UUID REFERENCES production_workers(id), worker_snapshot TEXT, machine_name TEXT,
 basis TEXT NOT NULL CHECK(basis IN ('unit','minute','hour','daily','weekly','monthly')),
 quantity NUMERIC(18,4) NOT NULL CHECK(quantity>0), rate NUMERIC(18,4) NOT NULL CHECK(rate>0),
 amount NUMERIC(18,4) GENERATED ALWAYS AS (ROUND(quantity*rate,4)) STORED CHECK(amount>0),
 reference TEXT NOT NULL CHECK(length(btrim(reference)) BETWEEN 1 AND 120),
 posting_date DATE NOT NULL, description TEXT, actor_id UUID NOT NULL REFERENCES users(id),
 created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
 CHECK((stage_code='assembly' AND worker_id IS NOT NULL AND worker_snapshot IS NOT NULL AND machine_name IS NULL)
 OR(stage_code IN ('cutting','ashareet') AND worker_id IS NULL AND worker_snapshot IS NULL AND machine_name IS NOT NULL AND length(btrim(machine_name))>0)),
 CHECK(stage_code='assembly' OR basis IN ('unit','minute','hour'))
);
CREATE UNIQUE INDEX ux_production_work_reference ON production_work_costs(lower(btrim(reference)));
CREATE INDEX ix_production_work_order ON production_work_costs(production_order_id,posting_date);
ALTER TABLE production_cost_entries DROP CONSTRAINT production_cost_entries_source_type_check;
ALTER TABLE production_cost_entries ADD CONSTRAINT production_cost_entries_source_type_check
 CHECK(source_type IN ('material_issue','material_return','labor','consumable','packaging','wage_accrual','manufacturing_work'));
ALTER TABLE finance_source_links DROP CONSTRAINT finance_source_links_source_type_check;
ALTER TABLE finance_source_links ADD CONSTRAINT finance_source_links_source_type_check
 CHECK(source_type IN ('inventory_receipt','wage_accrual','legacy_labor','job_work'));
COMMIT;
