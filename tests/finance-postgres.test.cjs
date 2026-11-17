// Opt-in real PostgreSQL acceptance. Uses an isolated random schema, never .env.
// FINANCE_TEST_DATABASE_URL must name a disposable development/test database.
const {test}=require('node:test');
const assert=require('node:assert/strict');
const {randomUUID}=require('node:crypto');
const fs=require('node:fs');
const path=require('node:path');
const {Pool}=require('../apps/api/node_modules/pg');
require('../apps/api/node_modules/reflect-metadata');
const {FinanceService}=require('../apps/api/dist/finance/finance.service');
const {InventoryService}=require('../apps/api/dist/inventory/inventory.service');
test('real PostgreSQL: migrations, sale/return, AR/AP, treasury, immutable history, retries and concurrent stock use', {skip:!process.env.FINANCE_TEST_DATABASE_URL,timeout:120000},async t=>{
 const schema='finance_test_'+randomUUID().replaceAll('-','');
 assert.match(schema,/^finance_test_[a-f0-9]{32}$/);
 const admin=new Pool({connectionString:process.env.FINANCE_TEST_DATABASE_URL,connectionTimeoutMillis:5000});
 let pool;
 try{
  await admin.query(`CREATE SCHEMA ${schema}`);
  pool=new Pool({connectionString:process.env.FINANCE_TEST_DATABASE_URL,options:`-c search_path=${schema},public`,connectionTimeoutMillis:5000,max:6});
  const migration=await pool.connect();try{for(const file of fs.readdirSync(path.join(__dirname,'../database/postgres/migrations')).filter(f=>/^\d+.*\.sql$/.test(f)).sort())await migration.query(fs.readFileSync(path.join(__dirname,'../database/postgres/migrations',file),'utf8'));}finally{migration.release();}
  const service=new FinanceService();service.pool=pool;const inventory=new InventoryService();inventory.pool=pool;
  const actor=randomUUID(),customer=randomUUID(),supplier=randomUUID(),item=randomUUID(),raw=randomUUID(),warehouse=randomUUID(),rawWarehouse=randomUUID(),unit=randomUUID();
  await pool.query(`INSERT INTO users(id,username,password_hash,display_name) VALUES($1,'finance-test','test-only','اختبار المالية')`,[actor]);
  await pool.query(`INSERT INTO roles(code,name) VALUES('system_admin','System Admin') ON CONFLICT DO NOTHING`);
  await pool.query(`INSERT INTO user_roles SELECT $1,id FROM roles WHERE code='system_admin'`,[actor]);
  await pool.query(`INSERT INTO parties(id,code,party_type,name_ar) VALUES($1,'C','customer','عميل'),($2,'S','supplier','مورد')`,[customer,supplier]);
  await pool.query(`INSERT INTO units_of_measure(id,code,name_ar,name_en,precision_digits) VALUES($1,'PCS','وحدة','Unit',0)`,[unit]);
  await pool.query(`INSERT INTO warehouses(id,code,name_ar,name_en,warehouse_type) VALUES($1,'FG','منتجات','FG','finished_goods'),($2,'RM','خامات','RM','raw_material')`,[warehouse,rawWarehouse]);
  await pool.query(`INSERT INTO items(id,code,name_ar,item_type,base_unit_id,classification) VALUES($1,'FG-1','منتج','finished_good',$3,'materials'),($2,'RM-1','خشب','raw_material',$3,'materials')`,[item,raw,unit]);
  const day='2026-01-15';let sequence=0;
  const cmd=(kind,extra={})=>({operationId:randomUUID(),kind,reference:'TEST-'+(++sequence),date:day,...extra});
  const cash=(await service.configure({operationId:randomUUID(),kind:'account',code:'CASH',name:'نقدية',accountType:'cash'},actor)).id;
  const bank=(await service.configure({operationId:randomUUID(),kind:'account',code:'BANK',name:'بنك',accountType:'bank'},actor)).id;
  const tax=(await service.configure({operationId:randomUUID(),kind:'tax',code:'TEST-14',name:'Test-only policy; not tax advice',rate:'14',effectiveFrom:'2026-01-01'},actor)).id;
  const sellLine={itemId:item,warehouseId:warehouse,quantity:'1',unitPrice:'3000',discount:'300',taxPolicyId:tax};
  let sale,receipt,obligation;
  await t.test('opening balances and transfers preserve cash+bank total',async()=>{
   await service.execute(cmd('opening_treasury',{accountId:cash,amount:'10000',notes:'Approved test opening'}),actor);
   await service.execute(cmd('transfer',{accountId:cash,targetAccountId:bank,amount:'500'}),actor);
   const report=await service.report(actor);assert.equal(report.summary.treasury,'10000.0000');assert.deepEqual(report.accounts.map(a=>a.balance).sort(),['500.0000','9500.0000']);
  });
  await t.test('purchase and sale use exact cost; invoice creates no second obligation',async()=>{
   await service.execute(cmd('purchase_fg',{partyId:supplier,lines:[{itemId:item,warehouseId:warehouse,quantity:'3',unitPrice:'2000'}]}),actor);
   const b=cmd('sale',{partyId:customer,lines:[sellLine]});sale=await service.execute(b,actor);assert.equal(sale.total,'3078.0000');assert.equal(sale.cogs,'2000.0000');
   assert.equal((await service.execute(b,actor)).alreadyPosted,true);
   await service.execute(cmd('invoice',{partyId:customer,saleIds:[sale.id]}),actor);
   await assert.rejects(service.execute(cmd('invoice',{partyId:customer,saleIds:[sale.id]}),actor));
   const report=await service.report(actor);assert.equal(report.summary.net,'2700.0000');assert.equal(report.summary.profit,'700.0000');obligation=report.obligations.find(o=>o.document_id===sale.id).id;
  });
  await t.test('advance allocation settles debt; return releases reusable credit at original COGS',async()=>{
   receipt=await service.execute(cmd('customer_receipt',{partyId:customer,accountId:cash,amount:'4000'}),actor);
   let r=await service.report(actor);const settlement=r.settlements.find(s=>s.document_id===receipt.id).id;
   await service.execute(cmd('allocation',{allocations:[{settlementId:settlement,obligationId:obligation,amount:'3078'}]}),actor);
   const original=r.lines.find(l=>l.document_id===sale.id);
   await service.execute(cmd('delivery',{sourceId:sale.id,lines:[{itemId:item,warehouseId:warehouse,sourceLineId:original.id,quantity:'1'}]}),actor);
   await service.execute(cmd('sales_return',{sourceId:sale.id,returnType:'return',lines:[{itemId:item,warehouseId:warehouse,sourceLineId:original.id,quantity:'1'}]}),actor);
   r=await service.report(actor);assert.equal(r.summary.net,'0.0000');assert.equal(r.summary.cogs,'0.0000');assert.equal(r.summary.advances,'4000.0000');assert.equal(r.summary.receivables,'0.0000');
   await service.execute(cmd('customer_refund',{sourceId:receipt.id,accountId:cash,amount:'4000'}),actor);assert.equal((await service.report(actor)).summary.advances,'0.0000');
  });
  await t.test('supplier installment settles payable without a second purchase',async()=>{
   const r=await service.report(actor),payable=r.obligations.find(o=>o.side==='ap');
   const paid=await service.execute(cmd('supplier_payment',{partyId:supplier,accountId:cash,amount:'4000'}),actor);
   const s=(await service.report(actor)).settlements.find(s=>s.document_id===paid.id);
   await service.execute(cmd('allocation',{allocations:[{settlementId:s.id,obligationId:payable.id,amount:'4000'}]}),actor);
   assert.equal((await service.report(actor)).summary.payables,'2000.0000');
  });
  await t.test('concurrent receipts use one operation and exact moving average',async()=>{
   const b={operationId:randomUUID(),itemId:raw,warehouseId:rawWarehouse,supplierId:supplier,documentReference:'RAW-A',movementDate:day,quantity:10,unitCost:100};
   await Promise.all([inventory.receive(b,actor),inventory.receive(b,actor)]);
   await inventory.receive({...b,operationId:randomUUID(),documentReference:'RAW-B',unitCost:200},actor);
   const c=(await pool.query('SELECT * FROM inventory_item_costs WHERE item_id=$1',[raw])).rows[0];assert.equal(c.quantity_on_hand,'20.0000');assert.equal(c.inventory_value,'3000.0000');assert.equal(c.average_unit_cost,'150.0000');
  });
  await t.test('only one simultaneous attempt can consume the final available units',async()=>{
   const results=await Promise.allSettled([service.execute(cmd('sale',{partyId:customer,lines:[{...sellLine,quantity:'3'}]}),actor),service.execute(cmd('sale',{partyId:customer,lines:[{...sellLine,quantity:'3'}]}),actor)]);
   assert.equal(results.filter(x=>x.status==='fulfilled').length,1);assert.equal(results.filter(x=>x.status==='rejected').length,1);
  });
  await t.test('same payment reference under another operation cannot duplicate treasury',async()=>{
   const b=cmd('customer_receipt',{partyId:customer,accountId:cash,amount:'100'});await service.execute(b,actor);const before=(await service.report(actor)).summary.treasury;
   await assert.rejects(service.execute({...b,operationId:randomUUID()},actor));assert.equal((await service.report(actor)).summary.treasury,before);
  });
  await t.test('posted history rejects direct edits and financial reconciliation is clean',async()=>{
   await assert.rejects(pool.query(`UPDATE finance_documents SET total=0 WHERE id=$1`,[sale.id]));const r=await service.report(actor);for(const [name,count] of Object.entries(r.reconciliations))assert.equal(count,0,name);
  });
 }finally{if(pool)await pool.end();await admin.query(`DROP SCHEMA IF EXISTS ${schema} CASCADE`);await admin.end();}
});
