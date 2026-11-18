import {BadRequestException,ConflictException} from '@nestjs/common';
import {PoolClient} from 'pg';
import {FinanceCommandDto} from './finance.dto';
import {units,decimal} from './decimal';
import {COST_SOURCES} from './monthly-finance.sql';

export const JOB_POOL_SQL=`WITH months AS (SELECT DISTINCT date_trunc('month',posting_date)::date AS month FROM production_cost_entries WHERE production_order_id=$1),
 pool AS (
 SELECT c.amount FROM production_cost_entries c WHERE c.treatment='pooled' AND date_trunc('month',c.posting_date)::date IN (SELECT month FROM months)
 UNION ALL SELECT CASE WHEN d.kind='expense' THEN d.net ELSE -d.net END FROM finance_documents d LEFT JOIN finance_documents original ON original.id=d.source_id
 WHERE date_trunc('month',d.posting_date)::date IN (SELECT month FROM months) AND ((d.kind='expense' AND d.category='industrial') OR(d.kind='reversal' AND original.kind='expense' AND original.category='industrial'))
 ) SELECT COALESCE(SUM(amount),0)::text AS amount FROM pool`;

export async function prepareCompletion(c:PoolClient,b:FinanceCommandDto){
 if(!b.productionOrderId||b.costApproved!==true||b.lines?.length!==1)throw new BadRequestException('اختر أمر الشغل والصنف النهائي والمخزن، ثم اعتمد اكتمال تسجيل التكاليف.');
 const job=(await c.query(`SELECT p.*,o.customer_id,o.status AS customer_status FROM production_orders p LEFT JOIN customer_orders o ON o.id=p.customer_order_id WHERE p.id=$1 FOR UPDATE OF p`,[b.productionOrderId])).rows[0];
 if(!job||job.status!=='ready_for_costing')throw new ConflictException('الاستلام متاح لأمر اجتاز الفحص بالكامل ولم يُستلم من قبل.');
 if(job.customer_order_id&&job.customer_status!=='open')throw new ConflictException('طلب العميل غير مفتوح.');
 const accepted=(await c.query(`SELECT COALESCE(SUM(quantity) FILTER(WHERE status='pending_costing'),0)::text AS quantity,MAX(accepted_date)::text AS latest FROM production_acceptances WHERE production_order_id=$1`,[job.id])).rows[0];
 const q=units(b.lines[0].quantity,true);if(q!==units(String(job.planned_quantity),true)||q!==units(accepted.quantity,true)||accepted.latest>b.date)throw new ConflictException('استلام الدفعة يتطلب قبول الكمية الكاملة وتاريخاً بعد آخر فحص.');
 const pending=await c.query(`SELECT id FROM production_wage_agreements WHERE production_order_id=$1 AND ((basis='unit' AND accrued_quantity<assigned_quantity) OR(basis<>'unit' AND accrued_quantity=0))`,[job.id]);
 if(pending.rowCount)throw new ConflictException('توجد أجور سابقة لم تُستحق؛ أكمل مراجعتها قبل اعتماد التكلفة.');
 const integrity=(await c.query(`WITH ${COST_SOURCES} SELECT
 EXISTS(SELECT 1 FROM cost_sources x FULL JOIN production_cost_entries c ON c.source_type=x.type AND c.source_id=x.id
  WHERE (x.job_id=$1 OR c.production_order_id=$1) AND (x.id IS NULL OR c.id IS NULL OR x.amount<>c.amount OR x.job_id<>c.production_order_id OR x.treatment<>c.treatment OR x.day<>c.posting_date OR x.item_id IS DISTINCT FROM c.item_id))
 OR EXISTS(SELECT 1 FROM production_material_postings p JOIN inventory_movements m ON m.id=p.inventory_movement_id WHERE p.production_order_id=$1
  AND(p.quantity<>m.quantity OR p.amount<>m.total_cost OR p.item_id<>m.item_id OR p.warehouse_id<>m.warehouse_id OR p.posting_date<>m.movement_date)) AS invalid`,[job.id])).rows[0];
 if(!integrity||integrity.invalid)throw new ConflictException('يوجد فرق بين مصادر التكاليف أو حركات المخزن وتسجيل أمر الشغل. راجع المطابقة قبل الاعتماد.');
 const pool=(await c.query(JOB_POOL_SQL,[job.id])).rows[0].amount;
 if(!/^-?0(?:\.0+)?$/.test(pool))throw new ConflictException('توجد مصاريف صناعية أو أجور مجمعة غير موزعة لأشهر هذا الأمر ('+pool+' ج.م). يجب اعتماد توزيعها قبل الاستلام؛ سعر البيع لا يحل محل تكلفة الإنتاج.');
 const costs=(await c.query(`SELECT COALESCE(SUM(amount) FILTER(WHERE treatment='direct'),0)::text AS total,MAX(posting_date)::text AS latest,
 jsonb_agg(jsonb_build_object('id',id,'sourceType',source_type,'sourceId',source_id,'amount',amount::text,'treatment',treatment,'date',posting_date::text) ORDER BY posting_date,id) AS snapshot
 FROM production_cost_entries WHERE production_order_id=$1`,[job.id])).rows[0];
 if(!costs||costs.latest>b.date)throw new ConflictException('تاريخ الاستلام يسبق آخر تكلفة.');
 const cost=units(costs.total,true);b.orderId=job.customer_order_id??undefined;b.partyId=job.customer_id??undefined;
 return {job,q,cost,snapshot:costs.snapshot,total:decimal(cost)};
}

export const READY_JOBS_SQL=`SELECT p.id,p.order_no AS "jobNo",p.job_name AS "jobName",p.product_code AS "productCode",p.product_name AS "productName",p.status,
 p.customer_order_id AS "orderId",o.order_no AS "orderNo",o.customer_id AS "customerId",p.planned_quantity::text AS quantity,
 COALESCE((SELECT SUM(quantity) FROM production_acceptances a WHERE a.production_order_id=p.id AND a.status='pending_costing'),0)::text AS accepted,
 COALESCE((SELECT SUM(amount) FROM production_cost_entries c WHERE c.production_order_id=p.id AND c.treatment='direct'),0)::text AS "directCost",
 EXISTS(SELECT 1 FROM production_completions c WHERE c.production_order_id=p.id) AS transferred
 FROM production_orders p LEFT JOIN customer_orders o ON o.id=p.customer_order_id WHERE p.status NOT IN ('cancelled','completed') ORDER BY p.created_at DESC`;
