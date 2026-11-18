BEGIN;
-- Preserve all posted rows and references. Extend only the work-cost categories.
DO $$ DECLARE r RECORD; BEGIN
 FOR r IN SELECT conname FROM pg_constraint WHERE conrelid='production_work_costs'::regclass
   AND contype='c' AND pg_get_constraintdef(oid) LIKE '%stage_code%'
 LOOP EXECUTE format('ALTER TABLE production_work_costs DROP CONSTRAINT %I',r.conname); END LOOP;
END $$;
ALTER TABLE production_work_costs ADD CONSTRAINT work_cost_stage_check
 CHECK(stage_code IN ('cutting','ashareet','assembly','general_labor'));
ALTER TABLE production_work_costs ADD CONSTRAINT work_cost_identity_check
 CHECK((stage_code IN ('assembly','general_labor') AND worker_id IS NOT NULL AND worker_snapshot IS NOT NULL AND machine_name IS NULL)
 OR(stage_code IN ('cutting','ashareet') AND worker_id IS NULL AND worker_snapshot IS NULL AND machine_name IS NOT NULL AND length(btrim(machine_name))>0));
ALTER TABLE production_work_costs ADD CONSTRAINT work_cost_basis_check
 CHECK(stage_code IN ('assembly','general_labor') OR basis IN ('unit','minute','hour'));
ALTER TABLE production_work_costs ADD CONSTRAINT general_wage_description_check
 CHECK(stage_code<>'general_labor' OR (description IS NOT NULL AND length(btrim(description))>0));
COMMIT;
