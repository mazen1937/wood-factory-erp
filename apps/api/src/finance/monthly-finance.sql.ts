// Source projections only. Never sum the mixed activity-table Amount column:
// a stock issue, its cost attribution and its payable are different effects.
export const COST_SOURCES=`cost_sources AS (
 SELECT 'material_issue'::text AS type,m.id,m.production_order_id AS job_id,m.item_id,m.amount,'direct'::text AS treatment,m.posting_date AS day,
 jsonb_build_object('itemCode',i.code,'quantity',m.quantity::text,'unitCost',m.unit_cost::text,'warehouse',w.code,'movementId',m.inventory_movement_id) AS detail
 FROM production_material_postings m JOIN items i ON i.id=m.item_id JOIN warehouses w ON w.id=m.warehouse_id WHERE m.original_issue_id IS NULL
 UNION ALL SELECT 'material_return',m.id,m.production_order_id,m.item_id,-m.amount,'direct',m.posting_date,
 jsonb_build_object('itemCode',i.code,'quantity',m.quantity::text,'unitCost',m.unit_cost::text,'originalIssueId',m.original_issue_id,'movementId',m.inventory_movement_id)
 FROM production_material_postings m JOIN items i ON i.id=m.item_id WHERE m.original_issue_id IS NOT NULL
 UNION ALL SELECT 'labor',l.id,l.production_order_id,NULL,l.amount,l.treatment,l.posting_date,
 jsonb_build_object('workerName',l.worker_name,'quantity',l.quantity::text,'rate',l.rate_snapshot::text) FROM production_labor_entries l
 UNION ALL SELECT 'wage_accrual',a.id,w.production_order_id,NULL,a.amount,w.treatment,a.posting_date,
 jsonb_build_object('workerName',w.worker_name,'basis',w.basis,'quantity',a.quantity::text,'rate',w.rate::text,'agreementId',w.id)
 FROM production_wage_accruals a JOIN production_wage_agreements w ON w.id=a.agreement_id
 UNION ALL SELECT 'consumable',c.id,c.production_order_id,c.item_id,c.amount,'direct',c.posting_date,
 jsonb_build_object('reference',c.document_reference) FROM production_consumable_costs c
 UNION ALL SELECT 'packaging',p.id,p.production_order_id,NULL,p.amount,'direct',p.posting_date,
 jsonb_build_object('description',p.description) FROM production_packaging_costs p
 UNION ALL SELECT 'manufacturing_work',w.id,w.production_order_id,NULL,w.amount,'direct',w.posting_date,
 jsonb_build_object('workerId',w.worker_id,'workerName',w.worker_snapshot,'machine',w.machine_name,'workType',w.stage_code,'basis',w.basis,'quantity',w.quantity::text,'rate',w.rate::text,'reference',w.reference,'description',w.description)
 FROM production_work_costs w
)`;

export const ACTIVITY_CTE=`WITH RECURSIVE ${COST_SOURCES},
 seed_orders AS (
 SELECT id AS document_id,customer_order_id AS order_id FROM finance_documents WHERE customer_order_id IS NOT NULL
 UNION SELECT l.document_id,p.customer_order_id FROM finance_source_links l JOIN cost_sources x ON x.id=l.source_id
 AND x.type=CASE l.source_type WHEN 'job_work' THEN 'manufacturing_work' WHEN 'legacy_labor' THEN 'labor' ELSE l.source_type END
 JOIN production_orders p ON p.id=x.job_id WHERE p.customer_order_id IS NOT NULL
 ), doc_edges AS (
 SELECT id AS child,source_id AS parent FROM finance_documents WHERE source_id IS NOT NULL
 UNION SELECT invoice_id,sale_id FROM finance_invoice_sales
 UNION SELECT a.document_id,o.document_id FROM finance_allocations a JOIN finance_obligations o ON o.id=a.obligation_id
 UNION SELECT s.document_id,o.document_id FROM finance_allocations a JOIN finance_obligations o ON o.id=a.obligation_id JOIN finance_settlements s ON s.id=a.settlement_id
 ), doc_orders(document_id,order_id) AS (
 SELECT document_id,order_id FROM seed_orders
 UNION SELECT e.child,o.order_id FROM doc_edges e JOIN doc_orders o ON o.document_id=e.parent
 ), doc_map AS (
 SELECT d.document_id,array_agg(DISTINCT d.order_id) AS order_ids,string_agg(DISTINCT o.order_no,', ' ORDER BY o.order_no) AS order_no
 FROM doc_orders d JOIN customer_orders o ON o.id=d.order_id GROUP BY d.document_id
 ), cash AS (SELECT document_id,SUM(amount) AS amount FROM finance_treasury_movements GROUP BY document_id),
 activity AS (
 SELECT 'order:'||o.id AS key,o.order_date AS day,'orders'::text AS domain,'customer_order'::text AS kind,o.order_no AS reference,
 ARRAY[o.id] AS order_ids,o.order_no,NULL::uuid AS job_id,NULL::text AS job_no,
 concat_ws(' | ',o.order_name,o.product_name,p.name_ar) AS title,NULL::text AS quantity,NULL::text AS unit,NULL::numeric AS amount,0::numeric AS cash,
 jsonb_build_object('orderId',o.id,'orderName',o.order_name,'productCode',o.product_code,'productName',o.product_name,'status',o.status,'notes',o.notes) AS detail
 FROM customer_orders o JOIN parties p ON p.id=o.customer_id
 UNION ALL SELECT 'job:'||p.id,p.production_date,'production','job_order',p.order_no,
 CASE WHEN p.customer_order_id IS NULL THEN ARRAY[]::uuid[] ELSE ARRAY[p.customer_order_id] END,o.order_no,p.id,p.order_no,p.product_name,p.planned_quantity::text,'منتج',NULL,0,
 jsonb_build_object('jobName',p.job_name,'status',p.status,'plannedQuantity',p.planned_quantity::text,'plannedEnd',p.planned_end_date)
 FROM production_orders p LEFT JOIN customer_orders o ON o.id=p.customer_order_id
 UNION ALL SELECT 'stock:'||m.id,m.movement_date,'inventory',m.movement_type,m.document_reference,
 COALESCE(CASE WHEN p.customer_order_id IS NOT NULL THEN ARRAY[p.customer_order_id] END,d.order_ids,ARRAY[]::uuid[]),COALESCE(o.order_no,d.order_no),p.id,p.order_no,
 concat_ws(' | ',i.code,i.name_ar,w.code,party.name_ar),
 (CASE WHEN m.movement_type IN ('production_issue','sale_issue') THEN -m.quantity ELSE m.quantity END)::text,u.code,
 CASE WHEN m.movement_type IN ('production_issue','sale_issue') THEN -m.total_cost ELSE m.total_cost END,0,
 jsonb_build_object('itemId',m.item_id,'itemCode',i.code,'warehouse',w.code,'unitCost',m.unit_cost::text,'itemBalanceAfter',m.quantity_after::text,'itemValueAfter',m.value_after::text,'notes',m.notes,'operationId',m.operation_id)
 FROM inventory_movements m JOIN items i ON i.id=m.item_id JOIN units_of_measure u ON u.id=i.base_unit_id JOIN warehouses w ON w.id=m.warehouse_id
 LEFT JOIN parties party ON party.id=m.party_id LEFT JOIN production_material_postings pm ON pm.inventory_movement_id=m.id
 LEFT JOIN production_completions pc ON pc.line_id=m.operation_id LEFT JOIN production_orders p ON p.id=COALESCE(pm.production_order_id,pc.production_order_id) LEFT JOIN customer_orders o ON o.id=p.customer_order_id
 LEFT JOIN finance_lines fl ON fl.id=m.operation_id LEFT JOIN doc_map d ON d.document_id=fl.document_id
 UNION ALL SELECT 'cost:'||c.id,c.posting_date,'costs',c.source_type,COALESCE(x.detail->>'reference',p.order_no),
 CASE WHEN p.customer_order_id IS NULL THEN ARRAY[]::uuid[] ELSE ARRAY[p.customer_order_id] END,o.order_no,p.id,p.order_no,
    concat_ws(' | ',c.description,i.code,i.name_ar),x.detail->>'quantity',COALESCE(u.code,x.detail->>'basis'),c.amount,0,
 COALESCE(x.detail,'{}'::jsonb)||jsonb_build_object('sourceId',c.source_id,'treatment',c.treatment,'costType',c.source_type)
 FROM production_cost_entries c JOIN production_orders p ON p.id=c.production_order_id LEFT JOIN customer_orders o ON o.id=p.customer_order_id
 LEFT JOIN cost_sources x ON x.type=c.source_type AND x.id=c.source_id LEFT JOIN items i ON i.id=c.item_id LEFT JOIN units_of_measure u ON u.id=i.base_unit_id
 UNION ALL SELECT 'event:'||e.operation_id,e.work_date,'production',e.event_type,p.order_no,
 CASE WHEN p.customer_order_id IS NULL THEN ARRAY[]::uuid[] ELSE ARRAY[p.customer_order_id] END,o.order_no,p.id,p.order_no,
 concat_ws(' | ',e.notes,u.display_name),e.payload->>'quantity',NULL,NULL,0,
 jsonb_build_object('notes',e.notes,'actor',u.display_name,'operationId',e.operation_id,'decision',e.payload->>'decision')
 FROM production_execution_events e JOIN production_orders p ON p.id=e.production_order_id LEFT JOIN customer_orders o ON o.id=p.customer_order_id JOIN users u ON u.id=e.actor_id
 WHERE e.event_type IN ('start','hold','resume','progress','quality_check','accept','waste','wage_agreement','cost_mode')
 UNION ALL SELECT 'finance:'||f.id,f.posting_date,'finance',f.kind,f.reference,COALESCE(d.order_ids,ARRAY[]::uuid[]),d.order_no,NULL,NULL,
 concat_ws(' | ',f.serial::text,f.party_name,f.notes),NULL,NULL,f.total,COALESCE(c.amount,0),
 jsonb_build_object('documentId',f.id,'serial',f.serial::text,'subtotal',f.subtotal::text,'discount',f.discount::text,'net',f.net::text,'tax',f.tax::text,'delivery',f.delivery_charge::text,'cogs',f.cogs::text,'category',f.category,'sourceId',f.source_id,'notes',f.notes)
 FROM finance_documents f LEFT JOIN doc_map d ON d.document_id=f.id LEFT JOIN cash c ON c.document_id=f.id
 UNION ALL SELECT 'worker:'||w.id,w.created_at::date,'master','worker_created',w.code,ARRAY[]::uuid[],NULL,NULL,NULL,w.name_ar,NULL,NULL,NULL,0,
 jsonb_build_object('workerId',w.id,'nameEn',w.name_en) FROM production_workers w
 UNION ALL SELECT 'price:'||s.id,s.effective_date,'finance','selling_price',i.code,CASE WHEN s.customer_order_id IS NULL THEN ARRAY[]::uuid[] ELSE ARRAY[s.customer_order_id] END,o.order_no,NULL,NULL,i.name_ar,NULL,NULL,NULL,0,jsonb_build_object('unitPrice',s.unit_price::text,'notes',s.notes,'itemId',s.item_id) FROM product_selling_prices s JOIN items i ON i.id=s.item_id LEFT JOIN customer_orders o ON o.id=s.customer_order_id
 UNION ALL SELECT 'config:'||c.id,c.created_at::date,'configuration',c.action,c.record_id::text,ARRAY[]::uuid[],NULL,NULL,NULL,'إعداد مالي',NULL,NULL,NULL,0,
 jsonb_build_object('recordId',c.record_id,'actorId',c.actor_id) FROM finance_configuration_audit c
 )`;

export const ACTIVITY_SELECT=`SELECT key,day::text AS date,domain,kind,reference,order_ids AS "orderIds",order_no AS "orderNo",job_id AS "jobId",job_no AS "jobNo",title,quantity,unit,amount::text,cash::text,detail FROM activity`;

export const MONTH_SUMMARY_SQL=`WITH ${COST_SOURCES}, bounds AS (SELECT $1::date AS start,($1::date+interval '1 month')::date AS finish),
 documents AS (SELECT d.*,s.kind AS source_kind,s.category AS source_category FROM finance_documents d LEFT JOIN finance_documents s ON s.id=d.source_id,bounds b WHERE d.posting_date>=b.start AND d.posting_date<b.finish),
 sales AS (SELECT COALESCE(SUM(CASE kind WHEN 'sale' THEN subtotal ELSE -subtotal END),0) AS gross,
 COALESCE(SUM(CASE kind WHEN 'sale' THEN discount ELSE -discount END),0) AS discount,
 COALESCE(SUM(CASE kind WHEN 'sale' THEN net ELSE -net END),0) AS net,
 COALESCE(SUM(CASE kind WHEN 'sale' THEN tax ELSE -tax END),0) AS tax,
 COALESCE(SUM(CASE kind WHEN 'sale' THEN delivery_charge ELSE -delivery_charge END),0) AS delivery,
 COALESCE(SUM(CASE kind WHEN 'sale' THEN cogs ELSE -cogs END),0) AS cogs FROM documents WHERE kind IN ('sale','sales_return')),
 expenses AS (SELECT COALESCE(SUM(CASE WHEN kind='expense' THEN net WHEN kind='reversal' AND source_kind='expense' THEN -net ELSE 0 END),0) AS total,
 COALESCE(SUM(CASE WHEN kind='expense' AND category='industrial' THEN net WHEN kind='reversal' AND source_kind='expense' AND source_category='industrial' THEN -net ELSE 0 END),0) AS industrial FROM documents),
 costs AS (SELECT COALESCE(SUM(amount) FILTER(WHERE treatment='direct' AND source_type IN ('manufacturing_work','packaging','labor','wage_accrual')),0) AS manufacturing,
 COALESCE(SUM(amount) FILTER(WHERE treatment='pooled'),0) AS pooled,
 COALESCE(SUM(amount) FILTER(WHERE source_type IN ('material_issue','material_return','consumable')),0) AS materials,
 COALESCE(SUM(amount) FILTER(WHERE treatment='direct'),0) AS additions FROM production_cost_entries,bounds b WHERE posting_date>=b.start AND posting_date<b.finish),
 cashflow AS (SELECT COALESCE(SUM(t.amount) FILTER(WHERE t.amount>0 AND d.kind NOT IN ('transfer','opening_treasury') AND NOT(d.kind='reversal' AND s.kind='transfer')),0) AS inflow,
 COALESCE(SUM(-t.amount) FILTER(WHERE t.amount<0 AND d.kind NOT IN ('transfer','opening_treasury') AND NOT(d.kind='reversal' AND s.kind='transfer')),0) AS outflow,
 COALESCE(SUM(t.amount),0) AS change FROM finance_treasury_movements t JOIN finance_documents d ON d.id=t.document_id LEFT JOIN finance_documents s ON s.id=d.source_id,bounds b WHERE t.posting_date>=b.start AND t.posting_date<b.finish)
 , allocations_asof AS (SELECT a.obligation_id,a.settlement_id,SUM(a.amount) AS amount FROM finance_allocations a JOIN finance_documents d ON d.id=a.document_id,bounds b WHERE d.posting_date<b.finish GROUP BY a.obligation_id,a.settlement_id),
 credits_asof AS (SELECT c.obligation_id,SUM(c.amount) AS amount FROM finance_credits c JOIN finance_documents d ON d.id=c.document_id,bounds b WHERE d.posting_date<b.finish GROUP BY c.obligation_id),
 obligation_balances AS (SELECT o.side,o.amount-COALESCE(c.amount,0)-COALESCE(a.amount,0) AS amount FROM finance_obligations o JOIN finance_documents d ON d.id=o.document_id
 LEFT JOIN credits_asof c ON c.obligation_id=o.id LEFT JOIN (SELECT obligation_id,SUM(amount) AS amount FROM allocations_asof GROUP BY obligation_id) a ON a.obligation_id=o.id,bounds b WHERE d.posting_date<b.finish),
 refunds_asof AS (SELECT r.source_id,SUM(r.total) AS amount FROM finance_documents r,bounds b WHERE r.kind IN ('customer_refund','reversal') AND r.posting_date<b.finish GROUP BY r.source_id),
 advances AS (SELECT s.side,s.amount-COALESCE(a.amount,0)-COALESCE(r.amount,0) AS amount FROM finance_settlements s JOIN finance_documents d ON d.id=s.document_id
 LEFT JOIN (SELECT settlement_id,SUM(amount) AS amount FROM allocations_asof GROUP BY settlement_id) a ON a.settlement_id=s.id LEFT JOIN refunds_asof r ON r.source_id=d.id,bounds b WHERE d.posting_date<b.finish)
 SELECT sales.gross::text AS "grossSales",sales.discount::text AS discounts,sales.net::text AS "netSales",sales.tax::text AS "salesTax",sales.delivery::text AS "deliveryCharges",sales.cogs::text AS cogs,
 (sales.net-sales.cogs)::text AS "grossProfit",CASE WHEN sales.net>0 THEN ROUND((sales.net-sales.cogs)/sales.net*100,4)::text ELSE NULL END AS "grossMargin",
 expenses.total::text AS "periodExpenses",expenses.industrial::text AS "industrialExpenses",costs.manufacturing::text AS "directManufacturing",costs.pooled::text AS "pooledWages",
 (expenses.industrial+costs.manufacturing+costs.pooled)::text AS "manufacturingIncurred",costs.materials::text AS "materialConsumption",costs.additions::text AS "directProductionAdditions",
 (sales.net-sales.cogs+sales.delivery-expenses.total)::text AS "resultBeforeClosing",cashflow.inflow::text AS "cashIn",cashflow.outflow::text AS "cashOut",cashflow.change::text AS "treasuryChange",
 (SELECT COALESCE(SUM(amount),0)::text FROM finance_treasury_movements,bounds b WHERE posting_date<b.start) AS "openingTreasury",
 (SELECT COALESCE(SUM(amount),0)::text FROM finance_treasury_movements,bounds b WHERE posting_date<b.finish) AS "closingTreasury",
 (SELECT COALESCE(SUM(t.amount),0)::text FROM finance_treasury_movements t JOIN finance_documents d ON d.id=t.document_id,bounds b WHERE d.kind='opening_treasury' AND t.posting_date>=b.start AND t.posting_date<b.finish) AS "openingEntries",
 (SELECT COALESCE(SUM(net),0)::text FROM documents WHERE kind='purchase_fg') AS "finishedPurchases",
 (SELECT COALESCE(SUM(m.total_cost),0)::text FROM inventory_movements m,bounds b WHERE m.movement_type='receipt' AND m.movement_date>=b.start AND m.movement_date<b.finish) AS "rawReceipts",
 (SELECT COALESCE(SUM(m.total_cost),0)::text FROM inventory_movements m,bounds b WHERE m.movement_type='receipt' AND m.movement_date>=b.start AND m.movement_date<b.finish
  AND NOT EXISTS(SELECT 1 FROM finance_source_links l JOIN finance_documents d ON d.id=l.document_id WHERE l.source_type='inventory_receipt' AND l.source_id=m.id AND d.posting_date<b.finish)) AS "unbilledRawReceipts",
 (SELECT COALESCE(SUM(x.amount),0)::text FROM cost_sources x,bounds b WHERE x.day>=b.start AND x.day<b.finish AND
  (x.type IN ('labor','wage_accrual') OR x.type='manufacturing_work' AND x.detail->>'workerId' IS NOT NULL)
  AND NOT EXISTS(SELECT 1 FROM finance_source_links l JOIN finance_documents d ON d.id=l.document_id WHERE l.source_id=x.id
   AND l.source_type=CASE x.type WHEN 'labor' THEN 'legacy_labor' WHEN 'manufacturing_work' THEN 'job_work' ELSE x.type END AND d.posting_date<b.finish)) AS "unpostedWagePayables",
 (SELECT COALESCE(SUM(amount),0)::text FROM obligation_balances WHERE side='ar') AS "receivablesAtEnd",
 (SELECT COALESCE(SUM(amount),0)::text FROM obligation_balances WHERE side='ap') AS "payablesAtEnd",
 (SELECT COALESCE(SUM(amount),0)::text FROM advances WHERE side='ar') AS "customerAdvancesAtEnd",
 (SELECT COALESCE(SUM(amount),0)::text FROM advances WHERE side='ap') AS "supplierAdvancesAtEnd",
 (SELECT COALESCE(SUM(cost),0)::text FROM production_completions,bounds b WHERE posting_date>=b.start AND posting_date<b.finish) AS "completedProductionCost",
 (SELECT COALESCE(SUM(c.amount),0)-(SELECT COALESCE(SUM(cost),0) FROM production_completions WHERE posting_date<(SELECT start FROM bounds)) FROM production_cost_entries c,bounds b WHERE c.treatment='direct' AND c.posting_date<b.start)::text AS "openingDirectWip",
 (SELECT COALESCE(SUM(c.amount),0)-(SELECT COALESCE(SUM(cost),0) FROM production_completions WHERE posting_date<(SELECT finish FROM bounds)) FROM production_cost_entries c,bounds b WHERE c.treatment='direct' AND c.posting_date<b.finish)::text AS "closingDirectWip",
 (SELECT COUNT(*)::int FROM production_orders WHERE status='ready_for_costing') AS "pendingCostingJobs"
 FROM sales,expenses,costs,cashflow`;

export const COST_RECONCILIATION_SQL=`WITH ${COST_SOURCES}
 SELECT 'production_cost'::text AS kind,COALESCE(x.id,c.source_id)::text AS reference,COALESCE(x.job_id,c.production_order_id)::text AS "jobId",
 x.amount::text AS expected,c.amount::text AS actual
 FROM cost_sources x FULL JOIN production_cost_entries c ON c.source_type=x.type AND c.source_id=x.id
 WHERE (x.id IS NULL OR c.id IS NULL OR x.amount<>c.amount OR x.job_id<>c.production_order_id OR x.treatment<>c.treatment OR x.day<>c.posting_date OR x.item_id IS DISTINCT FROM c.item_id)
 AND ((x.day>=$1::date AND x.day<($1::date+interval '1 month')) OR (c.posting_date>=$1::date AND c.posting_date<($1::date+interval '1 month')))
 UNION ALL SELECT 'stock_to_job',p.id::text,p.production_order_id::text,p.amount::text,m.total_cost::text
 FROM production_material_postings p JOIN inventory_movements m ON m.id=p.inventory_movement_id
 WHERE p.posting_date>=$1::date AND p.posting_date<($1::date+interval '1 month') AND
 (p.quantity<>m.quantity OR p.amount<>m.total_cost OR p.item_id<>m.item_id OR p.warehouse_id<>m.warehouse_id OR p.posting_date<>m.movement_date OR
 m.movement_type<>CASE WHEN p.original_issue_id IS NULL THEN 'production_issue' ELSE 'production_return' END)
 UNION ALL SELECT 'missing_job_posting',m.id::text,NULL,m.total_cost::text,NULL FROM inventory_movements m
 WHERE m.movement_type IN ('production_issue','production_return') AND m.movement_date>=$1::date AND m.movement_date<($1::date+interval '1 month')
 AND NOT EXISTS(SELECT 1 FROM production_material_postings p WHERE p.inventory_movement_id=m.id)`;
