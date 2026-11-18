const {test}=require('node:test'),assert=require('node:assert/strict');require('../apps/api/node_modules/reflect-metadata');
const {MonthlyFinanceService,monthStart}=require('../apps/api/dist/finance/monthly-finance.service');
const {MONTH_SUMMARY_SQL,COST_RECONCILIATION_SQL,ACTIVITY_CTE}=require('../apps/api/dist/finance/monthly-finance.sql');
const {FinanceService}=require('../apps/api/dist/finance/finance.service');const {InventoryService}=require('../apps/api/dist/inventory/inventory.service');
function harness({denied=false,fail=false}={}){
 const svc=new MonthlyFinanceService(),calls=[];let released=false;
 svc.pool={connect:async()=>({query:async(sql,args)=>{calls.push({sql,args});
  if(sql.includes('SELECT u.id'))return {rowCount:denied?0:1,rows:denied?[]:[{id:'user'}]};
  if(sql===MONTH_SUMMARY_SQL)return {rows:[{netSales:'0.1000',grossProfit:'-0.0001',manufacturingIncurred:'999999999999.9999'}]};
  if(sql===COST_RECONCILIATION_SQL){if(fail)throw new Error('query failed');return {rows:[{kind:'production_cost',reference:'bad',expected:'10.0000',actual:'11.0000'}]};}
  if(sql.includes('SELECT transaction_timestamp'))return {rows:[{at:'2026-11-18T12:00:00Z'}]};
  return {rows:[],rowCount:0};},release(){released=true;}})};return {svc,calls,released:()=>released};
}
test('monthly dates reject malformed, injected, missing and ambiguous months before connecting',async()=>{const h=harness();for(const month of [undefined,[],"2026-13","2026-1","2026-11;DROP TABLE x","0000-01"])await assert.rejects(h.svc.monthly('user',month));assert.equal(h.calls.length,0);assert.equal(monthStart('2026-02'),'2026-02-01');});
test('monthly snapshot is read-only, permission checked, exact and commits all projections together',async()=>{
 const h=harness();const r=await h.svc.monthly('user','2026-11');assert.match(h.calls[0].sql,/REPEATABLE READ READ ONLY/);assert.ok(h.calls[1].sql.includes('SELECT u.id'));
 assert.equal(r.summary.netSales,'0.1000');assert.equal(r.summary.grossProfit,'-0.0001');assert.equal(r.summary.manufacturingIncurred,'999999999999.9999');assert.equal(r.exceptions.length,1);
 assert.equal(r.activityAmountIsNotAdditive,true);assert.equal(h.calls.at(-1).sql,'COMMIT');assert.ok(h.released());assert.ok(h.calls.filter(c=>c.args).every(c=>c.args[0]==='user'||c.args[0]==='2026-11-01'));
});
test('revoked finance access returns no month data and releases the connection',async()=>{const h=harness({denied:true});await assert.rejects(h.svc.monthly('user','2026-11'));assert.equal(h.calls.at(-1).sql,'ROLLBACK');assert.ok(!h.calls.some(c=>c.sql===MONTH_SUMMARY_SQL));assert.ok(h.released());});
test('projection failure returns no partially reconciled month',async()=>{const h=harness({fail:true});await assert.rejects(h.svc.monthly('user','2026-11'));assert.equal(h.calls.at(-1).sql,'ROLLBACK');assert.ok(!h.calls.some(c=>c.sql==='COMMIT'));assert.ok(h.released());});
test('order drill-down rejects missing orders inside the same read authorization boundary',async()=>{const h=harness();await assert.rejects(h.svc.order('user','missing'),e=>e.getStatus()===404);assert.equal(h.calls.at(-1).sql,'ROLLBACK');});
test('source-based returns and deliveries inherit the original order and reject contradictory attribution',async()=>{
 const svc=new FinanceService(),c={query:async()=>({rowCount:1,rows:[{kind:'sale',party_id:'customer',customer_order_id:'correct',posting_date:'2026-01-01'}]})};
 const b={sourceId:'sale',date:'2026-02-01'};await svc.source(c,b,'sale');assert.equal(b.orderId,'correct');assert.equal(b.partyId,'customer');
 await assert.rejects(svc.source(c,{sourceId:'sale',date:'2026-02-01',partyId:'customer',orderId:'wrong'},'sale'));
});
test('receipt unit precision rejects fractional countable items before stock mutation',async()=>{
 const svc=new InventoryService(),calls=[];svc.pool={connect:async()=>({query:async sql=>{calls.push(sql);if(sql.includes('SELECT status FROM accounting_periods'))return {rows:[{status:'open'}]};if(sql.includes('FROM items WHERE id'))return {rowCount:1,rows:[{id:'item'}]};if(sql.includes('u.precision_digits'))return {rows:[{valid:false}]};return {rowCount:0,rows:[]};},release(){}})};
 await assert.rejects(svc.receive({operationId:'op',itemId:'item',warehouseId:'wh',movementDate:'2026-01-01',documentReference:'R',quantity:0.5,unitCost:1},'user'));assert.equal(calls.at(-1),'ROLLBACK');assert.ok(!calls.some(s=>s.includes('INSERT INTO inventory_balances')));
});
test('activity relations retain source identity and multi-order links without treating mixed amounts as profit',()=>{assert.match(ACTIVITY_CTE,/doc_orders\(document_id,order_id\)/);assert.match(ACTIVITY_CTE,/finance_invoice_sales/);assert.match(ACTIVITY_CTE,/finance_source_links/);assert.match(ACTIVITY_CTE,/finance_allocations/);assert.match(COST_RECONCILIATION_SQL,/FULL JOIN production_cost_entries/);});
