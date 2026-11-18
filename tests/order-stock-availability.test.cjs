const {test}=require('node:test');const assert=require('node:assert/strict');
require('../apps/api/node_modules/reflect-metadata');
const {OrderAvailabilityService,ORDER_AVAILABILITY_SQL}=require('../apps/api/dist/inventory/order-availability.service');
const {InventoryController}=require('../apps/api/dist/inventory/inventory.controller');
test('one database projection covers materials and accessories, deducts usable issues and shares stock across orders',()=>{
 assert.match(ORDER_AVAILABILITY_SQL,/customer_order_materials[\s\S]+UNION ALL SELECT order_id,item_id,quantity FROM customer_order_accessories/);
 assert.match(ORDER_AVAILABILITY_SQL,/m.quantity-COALESCE\(w.quantity,0\)/);assert.match(ORDER_AVAILABILITY_SQL,/ELSE -m.quantity/);
 assert.match(ORDER_AVAILABILITY_SQL,/PARTITION BY item_id ORDER BY order_date,created_at,order_id/);
 assert.ok(ORDER_AVAILABILITY_SQL.indexOf('WHERE ($1::uuid')>ORDER_AVAILABILITY_SQL.indexOf('ROWS BETWEEN UNBOUNDED PRECEDING'));
 assert.match(ORDER_AVAILABILITY_SQL,/COALESCE\(s.quantity,0\)/);assert.match(ORDER_AVAILABILITY_SQL,/warehouse_type='raw_material'/);
 assert.ok(!/\bINSERT\b|\bUPDATE\b|\bDELETE\b/i.test(ORDER_AVAILABILITY_SQL));
});
test('for-order snapshot retains exact quantity strings and warns for non-stock items',async()=>{
 const service=new OrderAvailabilityService();let query;
 const result=await service.forOrder({query:async(sql,params)=>{query={sql,params};return {rows:[{shortageQuantity:'0.0001',insufficient:true,checkable:true},{shortageQuantity:null,insufficient:false,checkable:false}]};}},'order');
 assert.deepEqual(query.params,['order']);assert.equal(result.items[0].shortageQuantity,'0.0001');assert.equal(result.hasShortages,true);assert.equal(result.uncheckedItems,1);
});
test('inventory report is one consistent read-only snapshot and includes unmapped legacy requests',async()=>{
 const service=new OrderAvailabilityService(),calls=[];let released=false;service.pool={connect:async()=>({query:async(sql)=>{calls.push(sql);return {rows:sql.includes('order-stock-availability')?[{insufficient:true,shortageQuantity:'5'}]:sql.includes('legacyBoards')?[{orderNo:'OLD',legacyBoards:7}]:[]};},release(){released=true;}})};
 const report=await service.report();assert.match(calls[0],/REPEATABLE READ READ ONLY/);assert.equal(calls.at(-1),'COMMIT');assert.equal(report.legacyOrders[0].legacyBoards,7);assert.ok(released);
});
test('unauthenticated inventory requests cannot read customer shortage data',async()=>{
 let reads=0;const controller=new InventoryController({me:async()=>({})},{},{report:async()=>{reads++;}});await assert.rejects(controller.orderShortages(''));assert.equal(reads,0);
});
