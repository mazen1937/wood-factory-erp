const { test } = require('node:test');
const assert = require('node:assert/strict');
require('../apps/api/node_modules/reflect-metadata');
const { OrdersService } = require('../apps/api/dist/orders/orders.service');

const body = { orderNo: 'JO-SECOND', productCode: 'P-02', productName: 'Desk', plannedQuantity: 2, productionDate: '2026-11-17' };
function setup(order = { id: 'order-2', status: 'open', confirmed_at: '2026-11-17' }, revision = null) {
  const service = new OrdersService();
  const queries = [];
  let released = false;
  service.pool = { async connect() { return {
    async query(sql, values) {
      queries.push({ sql, values });
      if (sql.includes('FROM customer_orders')) return { rows: order ? [order] : [], rowCount: order ? 1 : 0 };
      if (sql.includes('FROM engineering_packages')) return { rows: revision ? [revision] : [], rowCount: revision ? 1 : 0 };
      if (sql.includes('INSERT INTO production_orders')) return { rows: [{ id: 'job-2', orderNo: body.orderNo, engineeringPackageId: values[2] }], rowCount: 1 };
      return { rows: [], rowCount: 0 };
    }, release() { released = true; }
  }; } };
  return { service, queries, released: () => released };
}

test('a newly confirmed order creates a job sheet without an engineering revision', async () => {
  const h = setup();
  const created = await h.service.createDraftProductionOrder('order-2', body);
  assert.equal(created.id, 'job-2');
  assert.equal(created.engineeringPackageId, null);
  assert.equal(h.queries.filter(x => x.sql.includes('FROM engineering_packages')).length, 0);
  const insert = h.queries.find(x => x.sql.includes('INSERT INTO production_orders'));
  assert.equal(insert.values[1], 'order-2');
  assert.equal(insert.values[3], 'P-02');
  assert.equal(h.queries.at(-1).sql, 'COMMIT');
  assert.ok(h.released());
});

test('historical orders automatically retain the finalized revision link', async () => {
  const h = setup({ id: 'old-order', status: 'open', confirmed_at: null }, { id: 'old-revision', status: 'finalized' });
  const created = await h.service.createDraftProductionOrder('old-order', body);
  assert.equal(created.engineeringPackageId, 'old-revision');
  assert.match(h.queries.find(x => x.sql.includes('FROM engineering_packages')).sql, /status = 'finalized'/);
});

test('an unconfirmed historical order without a finalized revision cannot create a job sheet', async () => {
  const h = setup({ id: 'old-order', status: 'open', confirmed_at: null });
  await assert.rejects(h.service.createDraftProductionOrder('old-order', body), error => error.getStatus() === 400);
  assert.equal(h.queries.at(-1).sql, 'ROLLBACK');
  assert.ok(!h.queries.some(x => x.sql.includes('INSERT INTO production_orders')));
});

test('explicit revision links are scoped to the selected order and finalized status', async () => {
  const h = setup();
  await assert.rejects(h.service.createDraftProductionOrder('order-2', { ...body, engineeringPackageId: 'wrong-revision' }));
  const query = h.queries.find(x => x.sql.includes('FROM engineering_packages'));
  assert.deepEqual(query.values, ['order-2', 'wrong-revision']);
  assert.match(query.sql, /customer_order_id = \$1/);
  assert.equal(h.queries.at(-1).sql, 'ROLLBACK');
});

test('cancelled and missing customer orders cannot create job sheets', async () => {
  for (const order of [null, { id: 'cancelled', status: 'cancelled', confirmed_at: '2026-11-17' }]) {
    const h = setup(order);
    await assert.rejects(h.service.createDraftProductionOrder('order-2', body));
    assert.equal(h.queries.at(-1).sql, 'ROLLBACK');
    assert.ok(h.released());
  }
});

test('new job sheets load the checklist catalog and blank custom rows without a revision', async () => {
  const service = new OrdersService();
  service.pool = { async query(sql) {
    if (sql.includes('FROM production_orders')) return { rowCount: 1, rows: [{ id: 'job-2', customerOrderId: 'order-2', engineeringPackageId: null }] };
    if (sql.includes('FROM production_work_order_lines')) return { rowCount: 0, rows: [] };
    return { rowCount: 1, rows: [{ catalogCode: 'WOOD', itemNameAr: 'Wood', unitCode: 'PCS', lineNo: 1 }] };
  } };
  const result = await service.getProductionWorkOrder('job-2');
  assert.equal(result.productionOrder.engineeringPackageId, null);
  assert.equal(result.lines.length, 3);
  assert.equal(result.lines[0].isSelected, false);
  assert.equal(result.lines[0].quantity, null);
  assert.ok(result.lines[1].isCustom && result.lines[2].isCustom);
});

test('job product identity comes from the selected parent, not conflicting client fields',async()=>{
 const h=setup({id:'order-2',status:'open',confirmed_at:'2026-11-17',product_code:'PARENT-CODE',product_name:'Parent product',order_date:'2026-11-17',requested_delivery_date:'2026-11-25'});
 await h.service.createDraftProductionOrder('order-2',{orderNo:'JO-1',jobName:'First batch',plannedQuantity:2});
 const insert=h.queries.find(q=>q.sql.includes('INSERT INTO production_orders'));assert.equal(insert.values[3],'PARENT-CODE');assert.equal(insert.values[4],'Parent product');assert.equal(insert.values[5],'2026-11-17');assert.equal(insert.values[8],'First batch');assert.equal(insert.values[9],'2026-11-25');
 await h.service.createDraftProductionOrder('order-2',{...body,orderNo:'JO-OTHER',productCode:'WRONG',productName:'Wrong product'});const last=h.queries.filter(q=>q.sql.includes('INSERT INTO production_orders')).at(-1);assert.equal(last.values[3],'PARENT-CODE');assert.equal(last.values[4],'Parent product');
});
test('several independently named jobs can reference the same customer order',async()=>{
 const h=setup();for(const code of ['JO-A','JO-B'])await h.service.createDraftProductionOrder('order-2',{...body,orderNo:code,jobName:code+' batch'});
 const inserts=h.queries.filter(q=>q.sql.includes('INSERT INTO production_orders'));assert.equal(inserts.length,2);assert.deepEqual(inserts.map(q=>q.values[1]),['order-2','order-2']);assert.deepEqual(inserts.map(q=>q.values[0]),['JO-A','JO-B']);
});
test('job schedule rejects an end date before its start',async()=>{
 const h=setup();await assert.rejects(h.service.createDraftProductionOrder('order-2',{...body,plannedEndDate:'2026-11-16'}));assert.equal(h.queries.at(-1).sql,'ROLLBACK');assert.ok(!h.queries.some(q=>q.sql.includes('INSERT INTO production_orders')));
});
