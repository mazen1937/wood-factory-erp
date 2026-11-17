const {test}=require('node:test');
const assert=require('node:assert/strict');
const {createHash}=require('node:crypto');
require('../apps/api/node_modules/reflect-metadata');
const {plainToInstance}=require('../apps/api/node_modules/class-transformer');
const {validate}=require('../apps/api/node_modules/class-validator');
const {ProductionService,canonical}=require('../apps/api/dist/production/production.service');
const {ExecutionCommandDto}=require('../apps/api/dist/production/production.dto');
const operationId='11111111-1111-4111-8111-111111111111';
const stageId='22222222-2222-4222-8222-222222222222';
const actor='33333333-3333-4333-8333-333333333333';
const command=(action='progress')=>({operationId,action,workDate:'2026-11-17',stageId,quantity:2,notes:'Factory note'});
const rows=(...items)=>({rowCount:items.length,rows:items});
function harness(options={}) {
  const service=new ProductionService(),calls=[];let released=false;
  const client={async query(sql,values=[]) {
    calls.push({sql,values});
    if(sql.includes('SELECT payload_hash,result'))return options.prior?rows(options.prior):rows();
    if(sql.includes('SELECT * FROM production_orders'))return rows({id:'job',status:options.status??'in_progress',execution_started_date:'2026-11-01',planned_quantity:'10',customer_order_id:options.customerId});
    if(sql.includes('SELECT status FROM customer_orders'))return rows({status:options.sourceStatus??'open'});
    if(sql.includes('AS after_start'))return rows({valid:options.validDate??true,after_start:true});
    if(sql.includes('SELECT status FROM accounting_periods'))return rows({status:options.closed?'closed':'open'});
    if(sql.includes('MAX(work_date)'))return rows({valid:!options.badChronology});
    if(sql.includes('SELECT * FROM production_operation_catalog'))return {rowCount:values[0].length,rows:values[0].map((code,i)=>({code,name_ar:code,sequence:i+1}))};
    if(sql.includes('SELECT * FROM production_operations'))return options.wrongStage?rows():rows({id:stageId,operation_code:options.stageCode??'cutting',completed_quantity:'4',sequence:10});
    if(sql.includes('UPDATE production_operations op'))return options.excessProgress?rows():rows({completedQuantity:'6'});
    if(sql.includes('SELECT m.id FROM production_material_postings'))return options.excessWaste?rows():rows({id:'original'});
    if(sql.includes('FROM production_material_postings m WHERE'))return rows({id:'original',item_id:'item',warehouse_id:'warehouse',stage_id:stageId,quantity:'4',amount:'400',unit_cost:'100',returned:'1',returned_amount:'100',posting_date:'2026-11-10'});
    if(sql.includes('SELECT id FROM items'))return options.badItem?rows():rows({id:'item'});
    if(sql.includes('SELECT id FROM warehouses'))return rows({id:'warehouse'});
    if(sql.includes('SELECT * FROM inventory_item_costs'))return rows({quantity_on_hand:'20',inventory_value:'3000',average_unit_cost:'150'});
    if(sql.includes('SELECT quantity_on_hand FROM inventory_balances'))return rows({quantity_on_hand:'20'});
    if(sql.includes(' AS enough,'))return rows({enough:!options.insufficient,amount:options.amount??'300'});
    if(sql.includes('UPDATE inventory_item_costs'))return rows({quantity_on_hand:'18',inventory_value:'2700',average_unit_cost:'150'});
    if(sql.includes('INSERT INTO inventory_movements'))return rows({id:'movement'});
    if(sql.includes('INSERT INTO production_material_postings'))return rows({id:'consumption'});
    if(sql.includes('INSERT INTO production_cost_entries')&&options.failCost)throw new Error('Simulated cost write failure');
    if(sql.includes('SELECT * FROM production_labor_rates'))return rows({id:'rate',code:'CUTTING',rate:'50',treatment:options.treatment??'direct'});
    if(sql.includes('FROM production_labor_entries WHERE'))return rows({valid:!options.duplicateLabor});
    if(sql.includes('INSERT INTO production_labor_entries'))return rows({id:'labor',amount:'100'});
    if(sql.includes('FROM production_acceptances a WHERE'))return rows({valid:!options.excessAcceptance});
    if(sql.includes('INSERT INTO production_acceptances'))return rows({id:'acceptance'});
    if(sql.includes('INSERT INTO production_consumable_costs'))return rows({id:'consumable'});
    if(sql.includes('INSERT INTO production_material_waste'))return rows({id:'waste'});
    if(sql.includes('INSERT INTO production_packaging_costs'))return rows({id:'packaging',amount:'300'});
    if(sql.includes('SELECT CASE $2'))return rows({periodEnd:options.periodEnd??(values[1]==='unit'?null:'2026-11-17')});
    if(sql.includes('SELECT id FROM production_wage_agreements'))return options.wageOverlap?rows({id:'prior-agreement'}):rows();
    if(sql.includes('SELECT *,worker_name')&&sql.includes('FROM production_wage_agreements')) {
      if(sql.includes('WHERE id=$1'))return options.notDue?rows():rows({id:'agreement',rate:'50',workerName:'Ahmed',treatment:'direct'});
      return options.unitAgreements?{rowCount:options.unitAgreements.length,rows:options.unitAgreements}:rows();
    }
    if(sql.includes('AS valid')&&sql.includes('production_wage_agreements'))return rows({valid:!options.excessWage});
    if(sql.includes('INSERT INTO production_wage_agreements'))return rows({id:'agreement',basis:values[3],rate:values[4],workerName:values[2],treatment:values[8],periodEnd:values[7]});
    if(sql.includes('INSERT INTO production_wage_accruals'))return rows({id:'accrual',amount:'100'});
    if(sql.includes('WITH inspected AS'))return rows({valid:!options.excessQc});
    if(sql.includes('INSERT INTO production_quality_checks'))return rows({id:'inspection'});
    if(sql.includes('completed_quantity=GREATEST'))return rows({id:'qc'});
    return rows();
  },release(){released=true;}};
  service.pool={async connect(){return client;}};
  return {service,calls,released:()=>released};
}
function rollback(h) {assert.equal(h.calls.at(-1).sql,'ROLLBACK');assert.ok(!h.calls.some(x=>x.sql==='COMMIT'));assert.ok(h.released());}

test('stable canonical hash ignores property order without changing payload identity',()=> {
  assert.equal(canonical({b:2,a:1}),canonical({a:1,b:2}));
  assert.notEqual(canonical({a:1}),canonical({a:2}));
});
test('identical replay returns durable acknowledgment before checking new status or closed period',async()=> {
  const body=command(),hash=createHash('sha256').update(canonical({id:'job',...body})).digest('hex');
  const h=harness({closed:true,status:'completed',prior:{payload_hash:hash,result:{operationId,completedQuantity:'6'}}});
  assert.equal((await h.service.execute('job',body,actor)).alreadyPosted,true);
  assert.ok(!h.calls.some(x=>x.sql.includes('SELECT * FROM production_orders')));
  assert.equal(h.calls.at(-1).sql,'COMMIT');
});
test('operation ID reused with a different payload is rejected',async()=> {
  const h=harness({prior:{payload_hash:'different',result:{}}});
  await assert.rejects(h.service.execute('job',command(),actor),e=>e.getStatus()===409);rollback(h);
});
test('a selected production route is snapshotted and always includes quality control',async()=> {
  const h=harness({status:'draft'});
  await h.service.execute('job',{...command('start'),stages:['cutting','assembly']},actor);
  const stages=h.calls.filter(x=>x.sql.includes('INSERT INTO production_operations'));
  assert.deepEqual(stages.map(x=>x.values[1]),['cutting','assembly','quality_control']);
  assert.ok(h.calls.some(x=>x.sql.includes("status='in_progress'")));
  assert.equal(h.calls.at(-1).sql,'COMMIT');
});
test('cancelled customer source blocks beginning production',async()=> {
  const h=harness({status:'draft',customerId:'customer-order',sourceStatus:'cancelled'});
  await assert.rejects(h.service.execute('job',{...command('start'),stages:['cutting']},actor));rollback(h);
});
test('closed period prevents any stage progress or cost posting',async()=> {
  const h=harness({closed:true});await assert.rejects(h.service.execute('job',command(),actor));
  assert.ok(!h.calls.some(x=>x.sql.includes('UPDATE production_operations')));rollback(h);
});
test('future date and backdated stage chronology are rejected',async()=> {
  for(const option of [{validDate:false},{badChronology:true}]) {
    const h=harness(option);await assert.rejects(h.service.execute('job',command(),actor));rollback(h);
  }
});
test('partial progress records actor, date and stable operation identity atomically',async()=> {
  const h=harness();const result=await h.service.execute('job',command(),actor);
  assert.equal(result.completedQuantity,'6');
  const audit=h.calls.find(x=>x.sql.includes('INSERT INTO production_execution_events'));
  assert.equal(audit.values[0],operationId);assert.equal(audit.values[6],actor);assert.equal(audit.values[7],'2026-11-17');
  assert.equal(h.calls.at(-1).sql,'COMMIT');
});
test('excess stage progress and cross-job stage references cannot post',async()=> {
  for(const option of [{excessProgress:true},{wrongStage:true}]) {
    const h=harness(option);await assert.rejects(h.service.execute('job',command(),actor));rollback(h);
  }
});
test('hold requires a reason and halted jobs cannot record production progress',async()=> {
  const hold=harness();await assert.rejects(hold.service.execute('job',{...command('hold'),notes:' '},actor));rollback(hold);
  const stopped=harness({status:'on_hold'});await assert.rejects(stopped.service.execute('job',command(),actor));rollback(stopped);
});
test('material issue links the stock movement and one direct cost in the same transaction',async()=> {
  const h=harness();await h.service.execute('job',{...command('issue'),itemId:'item',warehouseId:'warehouse'},actor);
  const movement=h.calls.find(x=>x.sql.includes('INSERT INTO inventory_movements'));
  assert.equal(movement.values[1],'production_issue');assert.equal(movement.values[7],'150');
  const material=h.calls.find(x=>x.sql.includes('INSERT INTO production_material_postings'));
  assert.equal(material.values[2],'movement');
  const cost=h.calls.find(x=>x.sql.includes('INSERT INTO production_cost_entries'));
  assert.equal(cost.values[1],'material_issue');assert.equal(cost.values[2],'consumption');assert.equal(cost.values[5],'1');
  assert.equal(h.calls.at(-1).sql,'COMMIT');
});
test('insufficient material stock blocks the posting before stock or cost mutation',async()=> {
  const h=harness({insufficient:true});await assert.rejects(h.service.execute('job',{...command('issue'),itemId:'item',warehouseId:'warehouse'},actor));
  assert.ok(!h.calls.some(x=>x.sql.includes('UPDATE inventory_item_costs')));rollback(h);
});
test('a cost attribution failure rolls back the associated stock issue',async()=> {
  const h=harness({failCost:true});await assert.rejects(h.service.execute('job',{...command('issue'),itemId:'item',warehouseId:'warehouse'},actor));
  assert.ok(h.calls.some(x=>x.sql.includes('INSERT INTO inventory_movements')));
  assert.ok(!h.calls.some(x=>x.sql.includes('INSERT INTO production_execution_events')));rollback(h);
});
test('returns use the original issue unit cost and credit direct WIP cost',async()=> {
  const h=harness({amount:'200'});await h.service.execute('job',{...command('return'),originalIssueId:'original'},actor);
  const movement=h.calls.find(x=>x.sql.includes('INSERT INTO inventory_movements'));
  assert.equal(movement.values[1],'production_return');assert.equal(movement.values[7],'100');
  const cost=h.calls.find(x=>x.sql.includes('INSERT INTO production_cost_entries'));
  assert.equal(cost.values[1],'material_return');assert.equal(cost.values[5],'-1');
});
test('excess returns are blocked against original unreturned quantity',async()=> {
  const h=harness({insufficient:true});await assert.rejects(h.service.execute('job',{...command('return'),originalIssueId:'original'},actor));rollback(h);
});
test('actual waste is traceable to an issue without charging stock or WIP twice',async()=> {
  const h=harness();const result=await h.service.execute('job',{...command('waste'),originalIssueId:'original'},actor);
  assert.equal(result.wasteObservationId,'waste');
  assert.ok(!h.calls.some(x=>x.sql.includes('INSERT INTO inventory_movements')||x.sql.includes('INSERT INTO production_cost_entries')));
  assert.ok(h.calls.some(x=>x.sql.includes('FROM production_material_waste w')));
});
test('waste cannot exceed the unreturned and not-previously-wasted issued quantity',async()=> {
  const h=harness({excessWaste:true});await assert.rejects(h.service.execute('job',{...command('waste'),originalIssueId:'original'},actor));rollback(h);
});
test('pooled labor is captured separately from direct cost with a rate snapshot',async()=> {
  const h=harness({treatment:'pooled'});await h.service.execute('job',{...command('labor'),workerName:'Team',rateId:'rate'},actor);
  const labor=h.calls.find(x=>x.sql.includes('INSERT INTO production_labor_entries'));
  assert.equal(labor.values[6],'50');assert.equal(labor.values[7],'pooled');
  const cost=h.calls.find(x=>x.sql.includes('INSERT INTO production_cost_entries'));
  assert.equal(cost.values[3],'pooled');
});
test('labor cannot pay more units than completed or pay the same category quantity twice',async()=> {
  const h=harness({duplicateLabor:true});await assert.rejects(h.service.execute('job',{...command('labor'),workerName:'Team',rateId:'rate'},actor));rollback(h);
});
test('stock-tracked items cannot bypass stock via a direct consumable amount',async()=> {
  const h=harness({badItem:true});await assert.rejects(h.service.execute('job',{...command('consumable'),itemId:'item',amount:100,documentReference:'REF'},actor));rollback(h);
});
test('accepted quantities remain pending costing without a finished-stock receipt',async()=> {
  const h=harness();const result=await h.service.execute('job',command('accept'),actor);
  assert.equal(result.status,'pending_costing');
  assert.ok(h.calls.some(x=>x.sql.includes("status='ready_for_costing'")));
  assert.ok(!h.calls.some(x=>x.sql.includes('INSERT INTO inventory_movements')));
});
test('acceptance cannot exceed passed QC or duplicate accepted quantities',async()=> {
  const h=harness({excessAcceptance:true});await assert.rejects(h.service.execute('job',command('accept'),actor));rollback(h);
});
test('current database permissions revoke production access even with an old token',async()=> {
  const s=new ProductionService();s.pool={async query(){return rows();}};
  await assert.rejects(s.authorize(actor),e=>e.getStatus()===403);
  s.pool={async query(){return rows({code:'production_operator'});}};
  await s.authorize(actor);await assert.rejects(s.authorize(actor,true),e=>e.getStatus()===403);
});
test('server DTO rejects invalid quantities, dates and action codes',async()=> {
  assert.equal((await validate(plainToInstance(ExecutionCommandDto,command()))).length,0);
  for(const invalid of [{...command(),quantity:-1},{...command(),quantity:1.00001},{...command(),workDate:'2026-02-30'},{...command(),action:'finish-without-costing'}])
    assert.ok((await validate(plainToInstance(ExecutionCommandDto,invalid))).length>0);
});

test('packaging amount is posted once as an explicit direct job cost',async()=> {
  const h=harness();await h.service.execute('job',{...command('packaging_cost'),amount:300},actor);
  const cost=h.calls.find(c=>c.sql.includes("VALUES($1,'packaging'"));
  assert.equal(cost.values[2],'300');assert.equal(h.calls.at(-1).sql,'COMMIT');
});
test('unit wage approval records a commitment without inventing earned work',async()=> {
  const h=harness();await h.service.execute('job',{...command('wage_agreement'),workerName:'Ahmed',wageBasis:'unit',rate:50,treatment:'direct',periodStart:'2026-11-17'},actor);
  assert.ok(h.calls.some(c=>c.sql.includes('INSERT INTO production_wage_agreements')));
  assert.ok(!h.calls.some(c=>c.sql.includes('INSERT INTO production_wage_accruals')));
});
test('already-completed unpaid units can accrue when explicitly approved',async()=> {
  const h=harness();await h.service.execute('job',{...command('wage_agreement'),workerName:'Ahmed',wageBasis:'unit',rate:50,treatment:'direct',periodStart:'2026-11-17',workAlreadyCompleted:true},actor);
  assert.ok(h.calls.some(c=>c.sql.includes('INSERT INTO production_wage_accruals')));
  assert.ok(h.calls.some(c=>c.sql.includes('SUM(accrued_quantity)')));
});
test('stage progress automatically accrues a selected worker unit agreement',async()=> {
  const h=harness({unitAgreements:[{id:'agreement',rate:'50',workerName:'Ahmed',treatment:'direct'}]});
  const result=await h.service.execute('job',{...command(),wageAgreementId:'agreement'},actor);
  assert.equal(result.earnedAmount,'100');
  assert.ok(h.calls.some(c=>c.sql.includes("VALUES($1,'wage_accrual'")));
});
test('multiple piece-rate workers require an explicit worker before attributing earnings',async()=> {
  const h=harness({unitAgreements:[{id:'one'},{id:'two'}]});
  await assert.rejects(h.service.execute('job',command(),actor));rollback(h);
});
test('per-worker completion cannot exceed that worker assigned units',async()=> {
  const h=harness({excessWage:true,unitAgreements:[{id:'agreement',rate:'50',workerName:'Ahmed',treatment:'direct'}]});
  await assert.rejects(h.service.execute('job',{...command(),wageAgreementId:'agreement'},actor));rollback(h);
});
test('daily weekly and monthly agreements preserve explicit basis and only earn completed periods',async()=> {
  for(const wageBasis of ['daily','weekly','monthly']) {
    const h=harness({periodEnd:'2026-11-17'});await h.service.execute('job',{...command('wage_agreement'),workerName:'Ahmed',wageBasis,rate:50,treatment:'pooled',periodStart:'2026-11-01'},actor);
    assert.equal(h.calls.find(c=>c.sql.includes('INSERT INTO production_wage_agreements')).values[3],wageBasis);
    assert.ok(h.calls.some(c=>c.sql.includes('INSERT INTO production_wage_accruals')));
  }
  const future=harness({periodEnd:'2026-12-01'});await future.service.execute('job',{...command('wage_agreement'),workerName:'Ahmed',wageBasis:'monthly',rate:50,treatment:'pooled',periodStart:'2026-11-02'},actor);
  assert.ok(!future.calls.some(c=>c.sql.includes('INSERT INTO production_wage_accruals')));
});
test('overlapping wages for a worker cannot be approved as a second charge',async()=> {
  const h=harness({wageOverlap:true});await assert.rejects(h.service.execute('job',{...command('wage_agreement'),workerName:'Ahmed',wageBasis:'monthly',rate:50,treatment:'pooled',periodStart:'2026-11-01'},actor));rollback(h);
});
test('future or already-accrued fixed periods cannot accrue a second time',async()=> {
  const h=harness({notDue:true});await assert.rejects(h.service.execute('job',{...command('wage_due'),wageAgreementId:'agreement'},actor));rollback(h);
});
test('QC acceptance records the decision and accepted quantity atomically',async()=> {
  const h=harness();await h.service.execute('job',{...command('quality_check'),decision:'accepted'},actor);
  assert.ok(h.calls.some(c=>c.sql.includes('INSERT INTO production_quality_checks')));
  assert.ok(h.calls.some(c=>c.sql.includes('INSERT INTO production_acceptances')));
  assert.ok(!h.calls.some(c=>c.sql.includes('INSERT INTO inventory_movements')));
});
test('QC rejection needs a reason and creates no accepted stock or acceptance',async()=> {
  const h=harness();await h.service.execute('job',{...command('quality_check'),decision:'rejected',notes:'Repair edge'},actor);
  assert.ok(!h.calls.some(c=>c.sql.includes('INSERT INTO production_acceptances')));
  const missing=harness();await assert.rejects(missing.service.execute('job',{...command('quality_check'),decision:'rejected',notes:''},actor));rollback(missing);
});
test('QC reinspection is bounded by rejected units and cannot accept them twice',async()=> {
  const h=harness({excessQc:true});await assert.rejects(h.service.execute('job',{...command('quality_check'),decision:'accepted',reinspection:true},actor));rollback(h);
});
test('QC is no longer an ambiguous generic stage completion button',async()=> {
  const h=harness({stageCode:'quality_control'});await assert.rejects(h.service.execute('job',command(),actor));rollback(h);
});

test('legacy labor cannot charge a stage already using unit wage agreements',async()=>{
  const h=harness({wageOverlap:true});
  await assert.rejects(h.service.execute('job',{...command('labor'),workerName:'Ahmed',rateId:operationId},actor));
  rollback(h);assert.ok(!h.calls.some(c=>c.sql.includes('INSERT INTO production_labor_entries')));
});
