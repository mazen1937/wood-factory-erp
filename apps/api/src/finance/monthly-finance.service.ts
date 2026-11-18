import {BadRequestException,ForbiddenException,Injectable,NotFoundException} from '@nestjs/common';
import {Pool,PoolClient} from 'pg';
import {ACTIVITY_CTE,ACTIVITY_SELECT,MONTH_SUMMARY_SQL,COST_RECONCILIATION_SQL} from './monthly-finance.sql';

export function monthStart(month:string):string{
 if(typeof month!=='string'||!/^(?:[2-9]\d{3})-(?:0[1-9]|1[0-2])$/.test(month))throw new BadRequestException('اختر الشهر بصيغة YYYY-MM.');
 return month+'-01';
}
@Injectable()
export class MonthlyFinanceService{
 private readonly pool=new Pool({connectionString:process.env.DATABASE_URL});
 private async snapshot<T>(user:string,read:(c:PoolClient)=>Promise<T>):Promise<T>{
  const c=await this.pool.connect();try{
   await c.query('BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY');
   const access=await c.query(`SELECT u.id FROM users u JOIN user_roles ur ON ur.user_id=u.id JOIN roles r ON r.id=ur.role_id
    WHERE u.id=$1 AND u.active AND r.code=ANY($2::text[])`,[user,['system_admin','finance_manager','finance_operator','finance_viewer']]);
   if(!access.rowCount)throw new ForbiddenException('لا توجد صلاحية لعرض المالية.');
   const result=await read(c);await c.query('COMMIT');return result;
  }catch(e){await c.query('ROLLBACK');throw e;}finally{c.release();}
 }
 async monthly(user:string,month:string){
  const start=monthStart(month);
  return this.snapshot(user,async c=>{
   const summary=(await c.query(MONTH_SUMMARY_SQL,[start])).rows[0];
   const activities=(await c.query(`${ACTIVITY_CTE} ${ACTIVITY_SELECT} WHERE day>=$1::date AND day<($1::date+interval '1 month') ORDER BY day,key`,[start])).rows;
   const exceptions=(await c.query(COST_RECONCILIATION_SQL,[start])).rows;
   const stockExceptions=(await c.query(`WITH ledger AS (
    SELECT item_id,warehouse_id,SUM(CASE WHEN movement_type IN ('production_issue','sale_issue') THEN -quantity ELSE quantity END) AS quantity
    FROM inventory_movements GROUP BY item_id,warehouse_id)
    SELECT 'stock_balance' AS kind,COALESCE(l.item_id,b.item_id)::text AS reference,COALESCE(l.quantity,0)::text AS expected,COALESCE(b.quantity_on_hand,0)::text AS actual
    FROM ledger l FULL JOIN inventory_balances b USING(item_id,warehouse_id) WHERE COALESCE(l.quantity,0)<>COALESCE(b.quantity_on_hand,0)
    UNION ALL SELECT 'stock_value',COALESCE(c.item_id,l.item_id)::text,COALESCE(l.value,0)::text,c.inventory_value::text FROM inventory_item_costs c FULL JOIN
    (SELECT item_id,SUM(CASE WHEN movement_type IN ('production_issue','sale_issue') THEN -total_cost ELSE total_cost END) AS value FROM inventory_movements GROUP BY item_id) l USING(item_id)
    WHERE c.item_id IS NULL OR c.inventory_value<>COALESCE(l.value,0)
    UNION ALL SELECT 'stock_valuation_quantity',c.item_id::text,COALESCE(b.quantity,0)::text,c.quantity_on_hand::text FROM inventory_item_costs c LEFT JOIN
    (SELECT item_id,SUM(quantity_on_hand) AS quantity FROM inventory_balances GROUP BY item_id) b USING(item_id) WHERE c.quantity_on_hand<>COALESCE(b.quantity,0)`)).rows;
   const snapshotAt=(await c.query('SELECT transaction_timestamp()::text AS at')).rows[0].at;
   return {month,snapshotAt,summary,activities,exceptions,stockExceptions,
    basis:'business_dates',profitStatus:'before_costing_and_period_close',activityAmountIsNotAdditive:true};
  });
 }
 async order(user:string,id:string){
  return this.snapshot(user,async c=>{
   const order=(await c.query(`SELECT o.id,o.order_no AS "orderNo",o.order_name AS "orderName",o.product_code AS "productCode",o.product_name AS "productName",o.order_date::text AS date,
    o.requested_delivery_date::text AS delivery,o.status,o.notes,p.name_ar AS customer FROM customer_orders o JOIN parties p ON p.id=o.customer_id WHERE o.id=$1`,[id])).rows[0];
   if(!order)throw new NotFoundException('الطلب غير موجود.');
   const jobs=(await c.query(`SELECT p.id,p.order_no AS "jobNo",p.job_name AS name,p.status,p.planned_quantity::text AS quantity,p.production_date::text AS date,
    p.planned_end_date::text AS delivery,COALESCE(SUM(c.amount) FILTER(WHERE c.treatment='direct'),0)::text AS "directCost",
    COALESCE(SUM(c.amount) FILTER(WHERE c.treatment='pooled'),0)::text AS "pooledCost"
    FROM production_orders p LEFT JOIN production_cost_entries c ON c.production_order_id=p.id WHERE p.customer_order_id=$1 GROUP BY p.id ORDER BY p.production_date,p.order_no`,[id])).rows;
   const requirements=(await c.query(`SELECT i.code,i.name_ar AS name,m.quantity::text,u.code AS unit,'material' AS kind FROM customer_order_materials m JOIN items i ON i.id=m.item_id JOIN units_of_measure u ON u.id=i.base_unit_id WHERE m.order_id=$1
    UNION ALL SELECT i.code,i.name_ar,a.quantity::text,u.code,'accessory' FROM customer_order_accessories a JOIN items i ON i.id=a.item_id JOIN units_of_measure u ON u.id=i.base_unit_id WHERE a.order_id=$1`,[id])).rows;
   const attachments=(await c.query('SELECT id,name,file_name AS "fileName",mime_type AS "mimeType",description,color FROM customer_order_attachments WHERE order_id=$1 ORDER BY id',[id])).rows;
   const activities=(await c.query(`${ACTIVITY_CTE} ${ACTIVITY_SELECT} WHERE $1::uuid=ANY(order_ids) ORDER BY day,key`,[id])).rows;
   return {order,jobs,requirements,attachments,activities,scope:'all_history'};
  });
 }
}
