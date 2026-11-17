BEGIN;
ALTER TABLE customer_orders ADD COLUMN IF NOT EXISTS order_name TEXT CHECK(order_name IS NULL OR length(btrim(order_name)) BETWEEN 1 AND 240);
UPDATE customer_orders SET order_name=COALESCE(NULLIF(btrim(product_name),''),order_no) WHERE order_name IS NULL;
CREATE TABLE IF NOT EXISTS customer_order_materials (
 order_id UUID NOT NULL REFERENCES customer_orders(id), item_id UUID NOT NULL REFERENCES items(id),
 quantity NUMERIC(18,4) NOT NULL CHECK(quantity>0), PRIMARY KEY(order_id,item_id)
);
-- Old untyped board totals remain historical evidence; they cannot be mapped to
-- a real material item without the owner identifying which item was intended.
ALTER TABLE production_orders ADD COLUMN IF NOT EXISTS job_name TEXT CHECK(job_name IS NULL OR length(btrim(job_name)) BETWEEN 1 AND 240);
ALTER TABLE production_orders ADD COLUMN IF NOT EXISTS planned_end_date DATE;
UPDATE production_orders SET job_name=order_no WHERE job_name IS NULL;
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conname='production_order_schedule_check' AND conrelid='production_orders'::regclass) THEN
  ALTER TABLE production_orders ADD CONSTRAINT production_order_schedule_check CHECK(planned_end_date IS NULL OR planned_end_date>=production_date);
 END IF;
END $$;
CREATE INDEX IF NOT EXISTS ix_production_orders_customer_order ON production_orders(customer_order_id);
COMMIT;
