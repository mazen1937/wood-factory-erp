import { Injectable } from '@nestjs/common';
import { Pool, PoolClient } from 'pg';

// Planning projections only. The inventory ledger remains the authority for
// physical issues. No reservations, purchase documents or stock writes here.
export const ORDER_AVAILABILITY_SQL=`/* order-stock-availability */
WITH requirements AS (
 SELECT order_id,item_id,quantity FROM customer_order_materials
 UNION ALL SELECT order_id,item_id,quantity FROM customer_order_accessories
), wanted AS (
 SELECT order_id,item_id,SUM(quantity) AS required FROM requirements GROUP BY order_id,item_id
), actual_waste AS (
 SELECT original_issue_id,SUM(quantity) AS quantity FROM production_material_waste GROUP BY original_issue_id
), issued AS (
 SELECT po.customer_order_id AS order_id,m.item_id,
  SUM(CASE WHEN m.original_issue_id IS NULL THEN m.quantity-COALESCE(w.quantity,0) ELSE -m.quantity END) AS quantity
 FROM production_material_postings m JOIN production_orders po ON po.id=m.production_order_id
 LEFT JOIN actual_waste w ON w.original_issue_id=m.id
 WHERE po.customer_order_id IS NOT NULL AND po.status<>'cancelled'
 GROUP BY po.customer_order_id,m.item_id
), stock AS (
 SELECT b.item_id,SUM(b.quantity_on_hand) AS quantity FROM inventory_balances b
 JOIN warehouses w ON w.id=b.warehouse_id WHERE w.active AND w.warehouse_type='raw_material' GROUP BY b.item_id
), remaining AS (
 SELECT o.id AS order_id,o.order_no,COALESCE(o.order_name,o.product_name,o.order_no) AS order_name,
  o.order_date,o.created_at,p.name_ar AS customer,i.id AS item_id,i.code AS item_code,i.name_ar AS item_name,
  i.item_type,u.code AS unit_code,i.stock_tracked AND i.classification='materials' AS checkable,
  r.required,GREATEST(COALESCE(used.quantity,0),0) AS issued,
  GREATEST(r.required-GREATEST(COALESCE(used.quantity,0),0),0) AS remaining,COALESCE(s.quantity,0) AS on_hand
 FROM wanted r JOIN customer_orders o ON o.id=r.order_id JOIN parties p ON p.id=o.customer_id
 JOIN items i ON i.id=r.item_id JOIN units_of_measure u ON u.id=i.base_unit_id
 LEFT JOIN issued used ON used.order_id=o.id AND used.item_id=i.id LEFT JOIN stock s ON s.item_id=i.id
 WHERE o.status='open'
), queued AS (
 SELECT *,COALESCE(SUM(remaining) FILTER(WHERE checkable) OVER(
  PARTITION BY item_id ORDER BY order_date,created_at,order_id ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING),0) AS earlier_demand
 FROM remaining
), projected AS (
 SELECT *,CASE WHEN checkable THEN GREATEST(on_hand-earlier_demand,0) ELSE NULL END AS available,
  CASE WHEN checkable THEN GREATEST(remaining-GREATEST(on_hand-earlier_demand,0),0) ELSE NULL END AS shortage
 FROM queued
)
SELECT order_id AS "orderId",order_no AS "orderNo",order_name AS "orderName",order_date::text AS "orderDate",
 customer AS "customerName",item_id AS "itemId",item_code AS "itemCode",item_name AS "itemName",
 item_type AS "itemType",unit_code AS "unitCode",checkable,
 required::text AS "requiredQuantity",issued::text AS "usableIssuedQuantity",remaining::text AS "remainingQuantity",
 on_hand::text AS "onHandQuantity",available::text AS "availableQuantity",shortage::text AS "shortageQuantity",
 COALESCE(shortage>0,false) AS insufficient
FROM projected WHERE ($1::uuid IS NULL OR order_id=$1::uuid) ORDER BY order_date,created_at,order_id,item_code`;

@Injectable()
export class OrderAvailabilityService {
 private readonly pool=new Pool({connectionString:process.env.DATABASE_URL});
 async forOrder(client:Pick<PoolClient,'query'>,orderId:string){
  const result=await client.query(ORDER_AVAILABILITY_SQL,[orderId]);
  return {items:result.rows,hasShortages:result.rows.some(r=>r.insufficient),uncheckedItems:result.rows.filter(r=>!r.checkable).length};
 }
 async report(){
  const c=await this.pool.connect();try{
   await c.query('BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY');
   const items=(await c.query(ORDER_AVAILABILITY_SQL,[null])).rows;
   const legacyOrders=(await c.query(`SELECT o.id AS "orderId",o.order_no AS "orderNo",COALESCE(o.order_name,o.product_name,o.order_no) AS "orderName",o.total_boards AS "legacyBoards"
    FROM customer_orders o WHERE o.status='open' AND o.total_boards>0
    AND NOT EXISTS(SELECT 1 FROM customer_order_materials m WHERE m.order_id=o.id) ORDER BY o.order_date,o.id`)).rows;
   await c.query('COMMIT');return {items,legacyOrders};
  }catch(e){await c.query('ROLLBACK');throw e;}finally{c.release();}
 }
}
