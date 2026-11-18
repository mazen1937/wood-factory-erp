BEGIN;
ALTER TABLE finance_documents DROP CONSTRAINT finance_documents_kind_check;
ALTER TABLE finance_documents ADD CONSTRAINT finance_documents_kind_check CHECK(kind IN
 ('sale','sales_return','invoice','delivery','customer_receipt','supplier_payment','expense','supplier_invoice','purchase_fg','wage_payable','allocation','transfer','opening_treasury','customer_refund','reversal','production_completion'));
ALTER TABLE inventory_movements DROP CONSTRAINT inventory_movements_movement_type_check;
ALTER TABLE inventory_movements ADD CONSTRAINT inventory_movements_movement_type_check CHECK(movement_type IN
 ('receipt','production_issue','production_return','finished_purchase','sale_issue','sales_return','production_completion'));
CREATE TABLE product_selling_prices (
 id UUID PRIMARY KEY DEFAULT gen_random_uuid(),operation_id UUID NOT NULL UNIQUE,payload_hash TEXT NOT NULL,
 item_id UUID NOT NULL REFERENCES items(id),customer_order_id UUID REFERENCES customer_orders(id),
 unit_price NUMERIC(18,4) NOT NULL CHECK(unit_price>=0),effective_date DATE NOT NULL,notes TEXT,
 actor_id UUID NOT NULL REFERENCES users(id),created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX ix_selling_prices_lookup ON product_selling_prices(item_id,customer_order_id,effective_date DESC,created_at DESC);
CREATE TRIGGER product_selling_prices_immutable BEFORE UPDATE OR DELETE ON product_selling_prices FOR EACH ROW EXECUTE FUNCTION finance_immutable_history();
CREATE TABLE production_completions (
 id UUID PRIMARY KEY DEFAULT gen_random_uuid(),production_order_id UUID NOT NULL UNIQUE REFERENCES production_orders(id),
 document_id UUID NOT NULL UNIQUE REFERENCES finance_documents(id),line_id UUID NOT NULL UNIQUE REFERENCES finance_lines(id) DEFERRABLE INITIALLY DEFERRED,
 quantity NUMERIC(18,4) NOT NULL CHECK(quantity>0),cost NUMERIC(18,4) NOT NULL CHECK(cost>0),posting_date DATE NOT NULL,
 approved_by UUID NOT NULL REFERENCES users(id),source_snapshot JSONB NOT NULL,
 created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TRIGGER production_completions_immutable BEFORE UPDATE OR DELETE ON production_completions FOR EACH ROW EXECUTE FUNCTION finance_immutable_history();
CREATE OR REPLACE FUNCTION validate_production_completion() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE d finance_documents%ROWTYPE; l finance_lines%ROWTYPE; m inventory_movements%ROWTYPE; p production_orders%ROWTYPE; actual NUMERIC;
BEGIN
 SELECT * INTO d FROM finance_documents WHERE id=NEW.document_id;
 SELECT * INTO l FROM finance_lines WHERE id=NEW.line_id;
 SELECT * INTO m FROM inventory_movements WHERE operation_id=NEW.line_id;
 SELECT * INTO p FROM production_orders WHERE id=NEW.production_order_id;
 SELECT COALESCE(SUM(amount),0) INTO actual FROM production_cost_entries WHERE production_order_id=p.id AND treatment='direct';
 IF d.kind<>'production_completion' OR l.id IS NULL OR m.id IS NULL OR l.document_id<>d.id OR m.movement_type<>'production_completion'
 OR m.item_id<>l.item_id OR m.warehouse_id<>l.warehouse_id OR m.quantity<>NEW.quantity OR l.quantity<>NEW.quantity
 OR m.total_cost<>NEW.cost OR l.cogs<>NEW.cost OR d.total<>NEW.cost OR actual<>NEW.cost OR p.status<>'completed'
 OR d.cogs<>0 OR d.tax<>0 OR l.net<>NEW.cost OR l.tax<>0 OR (SELECT COUNT(*) FROM finance_lines WHERE document_id=d.id)<>1
 OR p.planned_quantity<>NEW.quantity OR NEW.quantity<>(SELECT COALESCE(SUM(quantity),0) FROM production_acceptances WHERE production_order_id=p.id AND status='transferred')
 THEN RAISE EXCEPTION 'Production completion sources do not reconcile' USING ERRCODE='23514'; END IF;
 RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER completion_commit_check AFTER INSERT ON production_completions DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION validate_production_completion();
CREATE OR REPLACE FUNCTION require_production_completion_source() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NEW.kind='production_completion' AND NOT EXISTS(SELECT 1 FROM production_completions WHERE document_id=NEW.id) THEN
  RAISE EXCEPTION 'Finished-goods receipt requires its accepted production source' USING ERRCODE='23514';
 END IF;
 RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER completion_document_source_check AFTER INSERT ON finance_documents DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_production_completion_source();
CREATE OR REPLACE FUNCTION protect_completed_job_costs() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE job UUID;
BEGIN
 job:=CASE WHEN TG_OP='DELETE' THEN OLD.production_order_id ELSE NEW.production_order_id END;
 IF EXISTS(SELECT 1 FROM production_completions WHERE production_order_id=job)
 OR (TG_OP='UPDATE' AND EXISTS(SELECT 1 FROM production_completions WHERE production_order_id=OLD.production_order_id)) THEN
  RAISE EXCEPTION 'Completed job costs require a controlled adjustment' USING ERRCODE='23514';
 END IF;
 IF TG_OP='DELETE' THEN RETURN OLD; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER completed_job_costs_guard BEFORE INSERT OR UPDATE OR DELETE ON production_cost_entries FOR EACH ROW EXECUTE FUNCTION protect_completed_job_costs();
CREATE OR REPLACE FUNCTION register_official_operation() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE op UUID; domain TEXT;
BEGIN
 IF TG_TABLE_NAME='finance_configuration_audit' THEN op:=NEW.id;domain:='finance_config';
 ELSIF TG_TABLE_NAME='finance_documents' THEN op:=NEW.operation_id;domain:='finance';
 ELSIF TG_TABLE_NAME='production_execution_events' THEN op:=NEW.operation_id;domain:='production';
 ELSIF TG_TABLE_NAME='product_selling_prices' THEN op:=NEW.operation_id;domain:='selling_price';
 ELSE op:=NEW.operation_id;domain:=CASE WHEN NEW.movement_type IN ('production_issue','production_return') THEN 'production'
  WHEN NEW.movement_type IN ('sale_issue','sales_return','finished_purchase','production_completion') THEN 'finance_stock' ELSE 'inventory' END;
 END IF;
 INSERT INTO official_operation_namespaces VALUES(op,domain) ON CONFLICT DO NOTHING;
 IF NOT EXISTS(SELECT 1 FROM official_operation_namespaces WHERE operation_id=op AND namespace=domain) THEN RAISE EXCEPTION 'Operation identity already used by another domain' USING ERRCODE='23505'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER selling_price_namespace BEFORE INSERT ON product_selling_prices FOR EACH ROW EXECUTE FUNCTION register_official_operation();
COMMIT;
