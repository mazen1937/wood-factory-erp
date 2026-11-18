const {test}=require('node:test');const assert=require('node:assert/strict');const {randomUUID}=require('node:crypto');const fs=require('node:fs');const path=require('node:path');
const {Pool}=require('../apps/api/node_modules/pg');require('../apps/api/node_modules/reflect-metadata');
const {OrderAvailabilityService}=require('../apps/api/dist/inventory/order-availability.service');
const {InventoryService}=require('../apps/api/dist/inventory/inventory.service');const {ProductionService}=require('../apps/api/dist/production/production.service');const {OrdersService}=require('../apps/api/dist/orders/orders.service');
test('PostgreSQL: shared-order deficits, missing items, usable issues, returns, waste and replenishment',{skip:!process.env.ORDER_STOCK_TEST_DATABASE_URL,timeout:120000},async()=>{
 const schema='order_stock_test_'+randomUUID().replaceAll('-','');assert.match(schema,/^order_stock_test_[a-f0-9]{32}$/);
 const admin=new Pool({connectionString:process.env.ORDER_STOCK_TEST_DATABASE_URL,connectionTimeoutMillis:5000});let pool;
 try{
  await admin.query(`CREATE SCHEMA ${schema}`);pool=new Pool({connectionString:process.env.ORDER_STOCK_TEST_DATABASE_URL,options:`-c search_path=${schema},public`,connectionTimeoutMillis:5000});
  const client=await pool.connect();try{for(const f of fs.readdirSync(path.join(__dirname,'../database/postgres/migrations')).filter(f=>/^\d+.*\.sql$/.test(f)&&parseInt(f)<=14&&parseInt(f)!==13).sort())await client.query(fs.readFileSync(path.join(__dirname,'../database/postgres/migrations',f),'utf8'));}finally{client.release();}
  const availability=new OrderAvailabilityService();availability.pool=pool;const inventory=new InventoryService();inventory.pool=pool;const production=new ProductionService();production.pool=pool;const orders=new OrdersService(availability);orders.pool=pool;
  const actor=randomUUID(),customer=randomUUID(),unit=randomUUID(),warehouse=randomUUID(),fgWarehouse=randomUUID(),raw=randomUUID(),accessory=randomUUID(),missing=randomUUID(),nonstock=randomUUID(),a=randomUUID(),b=randomUUID(),job=randomUUID();
  await pool.query(`INSERT INTO users(id,username,password_hash,display_name) VALUES($1,'stock-test','test-only','Test')`,[actor]);
  await pool.query(`INSERT INTO parties(id,code,party_type,name_ar) VALUES($1,'C','customer','Customer')`,[customer]);
  await pool.query(`INSERT INTO units_of_measure(id,code,name_ar,name_en,precision_digits) VALUES($1,'PCS','Unit','Unit',4)`,[unit]);
  await pool.query(`INSERT INTO warehouses(id,code,name_ar,name_en,warehouse_type) VALUES($1,'RAW','Raw','Raw','raw_material'),($2,'FG','FG','FG','finished_goods')`,[warehouse,fgWarehouse]);
  for(const [id,code,type,tracked] of [[raw,'MDF','raw_material',true],[accessory,'HINGE','accessory',true],[missing,'PHS','raw_material',true],[nonstock,'NONSTOCK','consumable',false]])await pool.query(`INSERT INTO items(id,code,name_ar,item_type,stock_tracked,classification,base_unit_id) VALUES($1,$2,$2,$3,$4,'materials',$5)`,[id,code,type,tracked,unit]);
  for(const [id,code,date] of [[a,'SO-A','2026-01-10'],[b,'SO-B','2026-01-11']])await pool.query(`INSERT INTO customer_orders(id,order_no,order_name,customer_id,order_date,product_code,product_name,confirmed_at) VALUES($1,$2,$2,$3,$4,'P','Product',now())`,[id,code,customer,date]);
  await pool.query(`INSERT INTO customer_order_materials VALUES($1,$3,6),($2,$3,7)`,[a,b,raw]);await pool.query(`INSERT INTO customer_order_accessories VALUES($1,$3,5),($2,$3,2)`,[a,b,accessory]);
  const receive=async(item,qty,ref)=>inventory.receive({operationId:randomUUID(),itemId:item,warehouseId:warehouse,documentReference:ref,movementDate:'2026-01-15',quantity:qty,unitCost:1},actor);
  await receive(raw,10,'R-1');await receive(accessory,1,'R-2');
  assert.equal((await inventory.movements('MDF R-1')).length,1,'Movement search combines item and document fields');
  assert.equal((await inventory.movements('R-%')).length,0,'Search wildcard characters are literal');
  let report=await availability.report();let row=(order,item)=>report.items.find(i=>i.orderId===order&&i.itemId===item);
  assert.equal(Number(row(a,raw).shortageQuantity),0);assert.equal(Number(row(b,raw).shortageQuantity),3);assert.equal(Number(row(a,accessory).shortageQuantity),4);assert.equal(Number(row(b,accessory).shortageQuantity),2);
  const confirmed=await orders.confirmOrder({orderNo:'SO-C',orderName:'New PHS request',customerId:customer,orderDate:'2026-01-12',productCode:'PHS-P',productName:'PHS product',materials:[{itemId:missing,quantity:3},{itemId:nonstock,quantity:2}],accessories:[],attachments:[],wastePercentage:0},[],actor);
  assert.equal(confirmed.availability.hasShortages,true);assert.equal(Number(confirmed.availability.items.find(i=>i.itemId===missing).shortageQuantity),3);assert.equal(confirmed.availability.uncheckedItems,1);
  await pool.query(`INSERT INTO production_orders(id,order_no,job_name,production_mode,customer_order_id,product_code,product_name,planned_quantity,status,execution_started_date,production_date) VALUES($1,'JOB-A','Job A','customer_order',$2,'P','Product',1,'in_progress','2026-01-10','2026-01-10')`,[job,a]);
  const issue=await production.execute(job,{operationId:randomUUID(),action:'issue',workDate:'2026-01-15',itemId:raw,warehouseId:warehouse,quantity:2},actor);
  report=await availability.report();assert.equal(Number(row(a,raw).remainingQuantity),4);assert.equal(Number(row(b,raw).shortageQuantity),3,'An issue reduces both stock and the same order demand once');
  await production.execute(job,{operationId:randomUUID(),action:'return',workDate:'2026-01-15',originalIssueId:issue.materialPostingId,quantity:1,notes:'Unused'},actor);
  report=await availability.report();assert.equal(Number(row(a,raw).usableIssuedQuantity),1);assert.equal(Number(row(b,raw).shortageQuantity),3);
  await production.execute(job,{operationId:randomUUID(),action:'waste',workDate:'2026-01-15',originalIssueId:issue.materialPostingId,quantity:1,notes:'Actual waste'},actor);
  report=await availability.report();assert.equal(Number(row(a,raw).usableIssuedQuantity),0);assert.equal(Number(row(b,raw).shortageQuantity),4);
  await receive(raw,4,'R-3');await receive(missing,3,'R-4');report=await availability.report();assert.equal(Number(row(b,raw).shortageQuantity),0);assert.equal(Number(row(confirmed.id,missing).shortageQuantity),0);
  const before=(await pool.query('SELECT COUNT(*)::int AS count FROM inventory_movements')).rows[0].count;await availability.report();await availability.report();assert.equal((await pool.query('SELECT COUNT(*)::int AS count FROM inventory_movements')).rows[0].count,before,'Refresh creates no stock postings or duplicate alerts');
  await pool.query(`UPDATE customer_orders SET status='cancelled' WHERE id=$1`,[a]);report=await availability.report();assert.ok(!report.items.some(i=>i.orderId===a));
 }finally{if(pool)await pool.end();await admin.query(`DROP SCHEMA IF EXISTS ${schema} CASCADE`);await admin.end();}
});
