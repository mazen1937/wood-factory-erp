BEGIN;
CREATE TABLE IF NOT EXISTS official_operation_namespaces(operation_id UUID PRIMARY KEY,namespace TEXT NOT NULL);
-- A production issue intentionally shares its operation ID with its execution event.
-- Reusing that identity for an unrelated receipt or financial document is rejected.
INSERT INTO official_operation_namespaces(operation_id,namespace)
 SELECT operation_id,CASE WHEN movement_type IN ('production_issue','production_return') THEN 'production' ELSE 'inventory' END FROM inventory_movements
 ON CONFLICT DO NOTHING;
INSERT INTO official_operation_namespaces SELECT operation_id,'production' FROM production_execution_events ON CONFLICT DO NOTHING;
DO $$ BEGIN
 IF EXISTS(SELECT 1 FROM production_execution_events e JOIN official_operation_namespaces n USING(operation_id) WHERE n.namespace<>'production') THEN
  RAISE EXCEPTION 'Existing operation identity crosses domains; reconcile before installing finance';
 END IF;
END $$;
INSERT INTO roles(code,name) VALUES ('finance_viewer','Finance Viewer'),('finance_operator','Finance Posting'),('finance_manager','Finance Manager') ON CONFLICT(code) DO NOTHING;
CREATE TABLE IF NOT EXISTS finance_accounts (
 id UUID PRIMARY KEY DEFAULT gen_random_uuid(), code TEXT NOT NULL CHECK(length(btrim(code))>0), name_ar TEXT NOT NULL CHECK(length(btrim(name_ar))>0),
 account_type TEXT NOT NULL CHECK(account_type IN ('cash','bank')), currency TEXT NOT NULL DEFAULT 'EGP' CHECK(currency='EGP'),
 active BOOLEAN NOT NULL DEFAULT true, created_by UUID NOT NULL REFERENCES users(id), created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_finance_account_code ON finance_accounts(lower(btrim(code)));
CREATE TABLE IF NOT EXISTS finance_tax_policies (
 id UUID PRIMARY KEY DEFAULT gen_random_uuid(), code TEXT NOT NULL, rate NUMERIC(9,4) NOT NULL CHECK(rate BETWEEN 0 AND 100),
 effective_from DATE NOT NULL, basis TEXT NOT NULL CHECK(length(btrim(basis))>0),
 approved_by UUID NOT NULL REFERENCES users(id), created_at TIMESTAMPTZ NOT NULL DEFAULT now(), UNIQUE(code,effective_from)
);
CREATE TABLE IF NOT EXISTS finance_documents (
 id UUID PRIMARY KEY DEFAULT gen_random_uuid(), serial BIGINT GENERATED ALWAYS AS IDENTITY UNIQUE,
 operation_id UUID NOT NULL UNIQUE, payload_hash TEXT NOT NULL, actor_id UUID NOT NULL REFERENCES users(id),
 kind TEXT NOT NULL CHECK(kind IN ('sale','sales_return','invoice','delivery','customer_receipt','supplier_payment','expense','supplier_invoice','purchase_fg','wage_payable','allocation','transfer','opening_treasury','customer_refund','reversal')),
 reference TEXT NOT NULL CHECK(length(btrim(reference))>0), posting_date DATE NOT NULL,
 party_id UUID REFERENCES parties(id), customer_order_id UUID REFERENCES customer_orders(id),
 source_id UUID REFERENCES finance_documents(id), account_id UUID REFERENCES finance_accounts(id),
 subtotal NUMERIC(18,4) NOT NULL DEFAULT 0 CHECK(subtotal>=0), discount NUMERIC(18,4) NOT NULL DEFAULT 0 CHECK(discount>=0),
 net NUMERIC(18,4) NOT NULL DEFAULT 0 CHECK(net>=0), tax NUMERIC(18,4) NOT NULL DEFAULT 0 CHECK(tax>=0),
 delivery_charge NUMERIC(18,4) NOT NULL DEFAULT 0 CHECK(delivery_charge>=0),
 total NUMERIC(18,4) NOT NULL DEFAULT 0 CHECK(total>=0), cogs NUMERIC(18,4) NOT NULL DEFAULT 0 CHECK(cogs>=0),
 category TEXT, notes TEXT, party_name TEXT NOT NULL DEFAULT '', request_payload JSONB NOT NULL,
 result JSONB NOT NULL DEFAULT '{}', created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
 CHECK(total=net+tax+delivery_charge), CHECK(subtotal-discount=net)
);
-- Internal sequence is not a statutory invoice number; gaps are intentional on rollback.
CREATE UNIQUE INDEX IF NOT EXISTS ux_finance_reference ON finance_documents(kind,COALESCE(party_id,'00000000-0000-0000-0000-000000000000'::uuid),lower(btrim(reference)));
CREATE UNIQUE INDEX IF NOT EXISTS ux_finance_reversal ON finance_documents(source_id) WHERE kind='reversal';
CREATE UNIQUE INDEX IF NOT EXISTS ux_finance_payable_reference ON finance_documents(COALESCE(party_id,'00000000-0000-0000-0000-000000000000'::uuid),lower(btrim(reference))) WHERE kind IN ('expense','supplier_invoice','purchase_fg','wage_payable');
CREATE UNIQUE INDEX IF NOT EXISTS ux_finance_opening ON finance_documents(account_id) WHERE kind='opening_treasury';
CREATE UNIQUE INDEX IF NOT EXISTS ux_finance_payment_reference ON finance_documents(kind,lower(btrim(reference))) WHERE kind IN ('customer_receipt','supplier_payment','transfer','customer_refund');
CREATE INDEX IF NOT EXISTS ix_finance_documents_date ON finance_documents(posting_date,serial);
CREATE TABLE IF NOT EXISTS finance_lines (
 id UUID PRIMARY KEY REFERENCES inventory_movements(operation_id), document_id UUID NOT NULL REFERENCES finance_documents(id),
 item_id UUID NOT NULL REFERENCES items(id), warehouse_id UUID NOT NULL REFERENCES warehouses(id),
 source_line_id UUID REFERENCES finance_lines(id), quantity NUMERIC(18,4) NOT NULL CHECK(quantity>0),
 unit_price NUMERIC(18,4) NOT NULL CHECK(unit_price>=0), subtotal NUMERIC(18,4) NOT NULL CHECK(subtotal>=0),
 discount NUMERIC(18,4) NOT NULL CHECK(discount>=0), net NUMERIC(18,4) NOT NULL CHECK(net>=0),
 tax NUMERIC(18,4) NOT NULL CHECK(tax>=0), total NUMERIC(18,4) NOT NULL CHECK(total=net+tax),
 cogs NUMERIC(18,4) NOT NULL CHECK(cogs>=0), tax_policy_id UUID REFERENCES finance_tax_policies(id),
 item_code TEXT NOT NULL, item_name TEXT NOT NULL, unit_code TEXT NOT NULL,
 CHECK(subtotal-discount=net), UNIQUE(document_id,item_id,warehouse_id)
);
CREATE INDEX IF NOT EXISTS ix_finance_return_source ON finance_lines(source_line_id);
CREATE TABLE IF NOT EXISTS finance_obligations (
 id UUID PRIMARY KEY DEFAULT gen_random_uuid(), document_id UUID NOT NULL UNIQUE REFERENCES finance_documents(id),
 party_id UUID REFERENCES parties(id), worker_name TEXT,
 side TEXT NOT NULL CHECK(side IN ('ar','ap')), amount NUMERIC(18,4) NOT NULL CHECK(amount>0),
 CHECK(party_id IS NOT NULL OR (side='ap' AND worker_name IS NOT NULL AND length(btrim(worker_name))>0))
);
CREATE TABLE IF NOT EXISTS finance_settlements (
 id UUID PRIMARY KEY DEFAULT gen_random_uuid(), document_id UUID NOT NULL UNIQUE REFERENCES finance_documents(id),
 party_id UUID REFERENCES parties(id), worker_name TEXT, side TEXT NOT NULL CHECK(side IN ('ar','ap')),
 amount NUMERIC(18,4) NOT NULL CHECK(amount>0),
 CHECK(party_id IS NOT NULL OR (side='ap' AND worker_name IS NOT NULL AND length(btrim(worker_name))>0))
);
CREATE TABLE IF NOT EXISTS finance_allocations (
 id UUID PRIMARY KEY DEFAULT gen_random_uuid(), document_id UUID NOT NULL REFERENCES finance_documents(id),
 settlement_id UUID NOT NULL REFERENCES finance_settlements(id), obligation_id UUID NOT NULL REFERENCES finance_obligations(id),
 amount NUMERIC(18,4) NOT NULL CHECK(amount<>0), created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
 UNIQUE(document_id,settlement_id,obligation_id)
);
CREATE TABLE IF NOT EXISTS finance_credits (
 document_id UUID PRIMARY KEY REFERENCES finance_documents(id), obligation_id UUID NOT NULL REFERENCES finance_obligations(id),
 amount NUMERIC(18,4) NOT NULL CHECK(amount>0)
);
CREATE TABLE IF NOT EXISTS finance_treasury_movements (
 id UUID PRIMARY KEY DEFAULT gen_random_uuid(), document_id UUID NOT NULL REFERENCES finance_documents(id),
 account_id UUID NOT NULL REFERENCES finance_accounts(id), amount NUMERIC(18,4) NOT NULL CHECK(amount<>0),
 posting_date DATE NOT NULL, UNIQUE(document_id,account_id)
);
CREATE TABLE IF NOT EXISTS finance_invoice_sales (
 invoice_id UUID NOT NULL REFERENCES finance_documents(id), sale_id UUID NOT NULL UNIQUE REFERENCES finance_documents(id),
 PRIMARY KEY(invoice_id,sale_id)
);
CREATE TABLE IF NOT EXISTS finance_deliveries (
 document_id UUID NOT NULL REFERENCES finance_documents(id), sale_line_id UUID NOT NULL REFERENCES finance_lines(id),
 quantity NUMERIC(18,4) NOT NULL CHECK(quantity<>0), PRIMARY KEY(document_id,sale_line_id)
);
CREATE TABLE IF NOT EXISTS finance_source_links (
 source_type TEXT NOT NULL CHECK(source_type IN ('inventory_receipt','wage_accrual','legacy_labor')),
 source_id UUID NOT NULL, document_id UUID NOT NULL REFERENCES finance_documents(id), PRIMARY KEY(source_type,source_id)
);
CREATE TABLE IF NOT EXISTS finance_configuration_audit (
 id UUID PRIMARY KEY DEFAULT gen_random_uuid(), actor_id UUID NOT NULL REFERENCES users(id),
 action TEXT NOT NULL, record_id UUID NOT NULL, detail JSONB NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE OR REPLACE VIEW finance_obligation_balances AS
 SELECT o.*,d.reference,d.serial,d.posting_date,d.kind,
 COALESCE((SELECT SUM(amount) FROM finance_credits c WHERE c.obligation_id=o.id),0) AS credited,
 COALESCE((SELECT SUM(amount) FROM finance_allocations a WHERE a.obligation_id=o.id),0) AS allocated,
 o.amount-COALESCE((SELECT SUM(amount) FROM finance_credits c WHERE c.obligation_id=o.id),0)
 -COALESCE((SELECT SUM(amount) FROM finance_allocations a WHERE a.obligation_id=o.id),0) AS outstanding
 FROM finance_obligations o JOIN finance_documents d ON d.id=o.document_id;
CREATE OR REPLACE VIEW finance_settlement_balances AS
 SELECT s.*,d.reference,d.serial,d.posting_date,
 COALESCE((SELECT SUM(amount) FROM finance_allocations a WHERE a.settlement_id=s.id),0) AS allocated,
 s.amount-COALESCE((SELECT SUM(amount) FROM finance_allocations a WHERE a.settlement_id=s.id),0)
 -COALESCE((SELECT SUM(total) FROM finance_documents r WHERE r.source_id=d.id AND r.kind IN ('customer_refund','reversal')),0) AS available
 FROM finance_settlements s JOIN finance_documents d ON d.id=s.document_id;
ALTER TABLE inventory_movements DROP CONSTRAINT IF EXISTS inventory_movements_movement_type_check;
ALTER TABLE inventory_movements ADD CONSTRAINT inventory_movements_movement_type_check
 CHECK(movement_type IN ('receipt','production_issue','production_return','finished_purchase','sale_issue','sales_return'));
-- Financial history is append-only. No UI/API performs destructive posted edits.
CREATE OR REPLACE FUNCTION finance_immutable_history() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Posted financial records are immutable; use a linked adjustment' USING ERRCODE='23514'; END $$;
DO $$ DECLARE tab TEXT; BEGIN
 FOREACH tab IN ARRAY ARRAY['finance_documents','finance_lines','finance_obligations','finance_settlements','finance_allocations','finance_credits','finance_treasury_movements','finance_invoice_sales','finance_deliveries','finance_source_links','finance_tax_policies','finance_configuration_audit'] LOOP
  IF NOT EXISTS(SELECT 1 FROM pg_trigger WHERE tgname=tab||'_immutable' AND tgrelid=tab::regclass) THEN
   EXECUTE format('CREATE TRIGGER %I BEFORE UPDATE OR DELETE ON %I FOR EACH ROW EXECUTE FUNCTION finance_immutable_history()',tab||'_immutable',tab);
  END IF;
 END LOOP;
END $$;
-- Deferred checks see the entire transaction, including rows inserted after the
-- document header. They are a second line of defence beyond the service checks.
CREATE OR REPLACE FUNCTION finance_validate_commit() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE d finance_documents%ROWTYPE; n NUMERIC; t NUMERIC; co NUMERIC; cnt INTEGER; ob NUMERIC;
BEGIN
 SELECT * INTO d FROM finance_documents WHERE id=NEW.id;
 IF d.kind IN ('sale','sales_return','purchase_fg') THEN
  SELECT COALESCE(SUM(net),0),COALESCE(SUM(tax),0),COALESCE(SUM(cogs),0),COUNT(*) INTO n,t,co,cnt FROM finance_lines WHERE document_id=d.id;
  IF cnt=0 OR n<>d.net OR t<>d.tax OR (d.kind<>'purchase_fg' AND co<>d.cogs) THEN
   RAISE EXCEPTION 'Document/line totals do not reconcile' USING ERRCODE='23514';
  END IF;
  IF EXISTS(SELECT 1 FROM finance_lines l JOIN inventory_movements m ON m.operation_id=l.id WHERE l.document_id=d.id
   AND (m.item_id<>l.item_id OR m.warehouse_id<>l.warehouse_id OR m.quantity<>l.quantity OR m.total_cost<>l.cogs
    OR m.movement_type<>CASE d.kind WHEN 'sale' THEN 'sale_issue' WHEN 'purchase_fg' THEN 'finished_purchase' ELSE 'sales_return' END)) THEN
   RAISE EXCEPTION 'Stock/cost link mismatch' USING ERRCODE='23514';
  END IF;
 END IF;
 IF d.kind IN ('sale','purchase_fg','expense','supplier_invoice','wage_payable') THEN
  SELECT COALESCE(SUM(amount),0) INTO ob FROM finance_obligations WHERE document_id=d.id;
  IF ob<>d.total THEN RAISE EXCEPTION 'Obligation mismatch' USING ERRCODE='23514'; END IF;
 END IF;
 IF d.kind IN ('transfer','customer_receipt','supplier_payment','customer_refund','opening_treasury') THEN
  SELECT COALESCE(SUM(amount),0),COUNT(*) INTO n,cnt FROM finance_treasury_movements WHERE document_id=d.id;
  IF (d.kind='transfer' AND (cnt<>2 OR n<>0 OR EXISTS(SELECT 1 FROM finance_treasury_movements WHERE document_id=d.id AND abs(amount)<>d.total)))
   OR (d.kind IN ('customer_receipt','opening_treasury') AND (cnt<>1 OR n<>d.total))
   OR (d.kind IN ('supplier_payment','customer_refund') AND (cnt<>1 OR n<>-d.total)) THEN
   RAISE EXCEPTION 'Treasury legs do not reconcile' USING ERRCODE='23514';
  END IF;
 END IF;
 IF d.kind IN ('customer_receipt','supplier_payment') THEN
  SELECT COALESCE(SUM(amount),0) INTO n FROM finance_settlements WHERE document_id=d.id;
  IF n<>d.total THEN RAISE EXCEPTION 'Settlement mismatch' USING ERRCODE='23514'; END IF;
 END IF;
 IF EXISTS(SELECT 1 FROM finance_obligation_balances WHERE outstanding<0 OR allocated<0 OR credited>amount)
  OR EXISTS(SELECT 1 FROM finance_settlement_balances WHERE available<0 OR allocated<0) THEN
  RAISE EXCEPTION 'Invalid obligation/settlement balance' USING ERRCODE='23514';
 END IF;
 IF EXISTS(SELECT 1 FROM finance_allocations a JOIN finance_obligations o ON o.id=a.obligation_id JOIN finance_settlements s ON s.id=a.settlement_id
  WHERE s.side<>o.side OR s.party_id IS DISTINCT FROM o.party_id OR s.worker_name IS DISTINCT FROM o.worker_name) THEN
  RAISE EXCEPTION 'Allocation counterparty mismatch' USING ERRCODE='23514';
 END IF;
 IF EXISTS(SELECT 1 FROM finance_credits c JOIN finance_obligations o ON o.id=c.obligation_id JOIN finance_documents r ON r.id=c.document_id
  WHERE r.kind NOT IN ('sales_return','reversal') OR r.source_id IS DISTINCT FROM o.document_id) THEN
  RAISE EXCEPTION 'Credit source mismatch' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $$;
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM pg_trigger WHERE tgname='finance_commit_reconciliation' AND tgrelid='finance_documents'::regclass) THEN
  CREATE CONSTRAINT TRIGGER finance_commit_reconciliation AFTER INSERT ON finance_documents DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION finance_validate_commit();
 END IF;
END $$;
CREATE OR REPLACE FUNCTION register_official_operation() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE op UUID; domain TEXT;
BEGIN
 IF TG_TABLE_NAME='finance_configuration_audit' THEN op:=NEW.id; domain:='finance_config';
 ELSIF TG_TABLE_NAME='finance_documents' THEN op:=NEW.operation_id; domain:='finance';
 ELSIF TG_TABLE_NAME='production_execution_events' THEN op:=NEW.operation_id; domain:='production';
 ELSE op:=NEW.operation_id; domain:=CASE WHEN NEW.movement_type IN ('production_issue','production_return') THEN 'production'
  WHEN NEW.movement_type IN ('sale_issue','sales_return','finished_purchase') THEN 'finance_stock' ELSE 'inventory' END;
 END IF;
 INSERT INTO official_operation_namespaces VALUES(op,domain) ON CONFLICT DO NOTHING;
 IF NOT EXISTS(SELECT 1 FROM official_operation_namespaces WHERE operation_id=op AND namespace=domain) THEN
  RAISE EXCEPTION 'Operation identity already used by another domain' USING ERRCODE='23505';
 END IF;
 RETURN NEW;
END $$;
DO $$ DECLARE tab TEXT; BEGIN
 FOREACH tab IN ARRAY ARRAY['inventory_movements','production_execution_events','finance_documents','finance_configuration_audit'] LOOP
  IF NOT EXISTS(SELECT 1 FROM pg_trigger WHERE tgname=tab||'_operation_namespace' AND tgrelid=tab::regclass) THEN
   EXECUTE format('CREATE TRIGGER %I BEFORE INSERT ON %I FOR EACH ROW EXECUTE FUNCTION register_official_operation()',tab||'_operation_namespace',tab);
  END IF;
 END LOOP;
END $$;
COMMIT;
