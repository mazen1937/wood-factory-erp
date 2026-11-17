-- Confirmed customer orders can directly supply a job sheet without an
-- engineering revision. Historical revision links and their FK remain intact.
BEGIN;
DO $$
DECLARE existing_constraint RECORD;
BEGIN
  FOR existing_constraint IN
    SELECT conname FROM pg_constraint
    WHERE conrelid = 'production_orders'::regclass AND contype = 'c'
      AND pg_get_constraintdef(oid) LIKE '%production_mode%'
      AND pg_get_constraintdef(oid) LIKE '%engineering_package_id%'
  LOOP
    EXECUTE format('ALTER TABLE production_orders DROP CONSTRAINT %I', existing_constraint.conname);
  END LOOP;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'production_orders'::regclass AND conname = 'ck_production_orders_customer_source') THEN
    ALTER TABLE production_orders ADD CONSTRAINT ck_production_orders_customer_source CHECK (
      (production_mode = 'customer_order' AND customer_order_id IS NOT NULL)
      OR (production_mode = 'stock' AND customer_order_id IS NULL)
    );
  END IF;
END $$;
COMMIT;
