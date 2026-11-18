// Run after the API build: node --test tests/complete-orders.test.cjs
const { test } = require('node:test');
const assert = require('node:assert/strict');
require('../apps/api/node_modules/reflect-metadata');
const { plainToInstance } = require('../apps/api/node_modules/class-transformer');
const { validate } = require('../apps/api/node_modules/class-validator');
const { OrdersService } = require('../apps/api/dist/orders/orders.service');
const { OrdersController } = require('../apps/api/dist/orders/orders.controller');
const { ConfirmOrderDto } = require('../apps/api/dist/orders/confirm-order.dto');
const customerId = '11111111-1111-4111-8111-111111111111';
const itemId = '22222222-2222-4222-8222-222222222222';
const payload = () => ({ orderNo: 'SO-TEST', customerId, orderDate: '2026-11-17', productCode: 'P-01',
  productName: 'Wardrobe', totalBoards: 7, wastePercentage: 8.5, notes: 'Order description',
  accessories: [{ itemId, quantity: 12 }],
  attachments: [{ name: 'Brown cut list', attachmentType: 'cut_list', description: 'Side panels', color: 'Brown' },
    { name: 'White cut list', attachmentType: 'cut_list', description: 'Shelves' }] });
const files = () => ['brown.png', 'white.pdf'].map(name => ({ originalname: name, mimetype: name.endsWith('png') ? 'image/png' : 'application/pdf', size: 3, buffer: Buffer.from('abc') }));

function harness(failAt) {
  const service = new OrdersService();
  const calls = [];
  let released = false;
  const client = {
    async query(sql, values) {
      calls.push({ sql, values });
      if(sql.includes('/* order-stock-availability */')){
        if(failAt==='stock-report')throw new Error('Stock calculation failed');
        const result=failAt==='stock-shortage'?[{orderId:'order-id',itemId,shortageQuantity:'3.0000',checkable:true,insufficient:true}]:[];return {rowCount:result.length,rows:result};
      }
      if (failAt === 'customer' && sql.includes('FROM parties')) return { rowCount: 0, rows: [] };
      if (failAt === 'accessory' && sql.includes('FROM items')) return { rowCount: 0, rows: [] };
      if(sql.includes('FROM items i JOIN units_of_measure'))return {rowCount:failAt==='material'?0:1,rows:failAt==='material'?[]:[{id:itemId,precision_digits:0}]};
      if(sql.includes('ROUND($1::numeric'))return {rowCount:1,rows:[{valid:failAt!=='material-precision'}]};
      if(failAt==='material-write'&&sql.includes('INSERT INTO customer_order_materials'))throw new Error('Material save failed');
      if (failAt === 'file' && sql.includes('INSERT INTO customer_order_attachments')) throw new Error('Simulated storage failure');
      if (sql.includes('INSERT INTO customer_orders')) return { rowCount: 1, rows: [{ id: 'order-id', orderNo: 'SO-TEST' }] };
      return { rowCount: 1, rows: [{ id: 'valid' }] };
    },
    release() { released = true; }
  };
  service.pool = { async connect() { return client; } };
  return { service, calls, released: () => released };
}

test('confirmation binds order totals once, all attachment metadata and accessory totals before committing', async () => {
  const h = harness();
  assert.equal((await h.service.confirmOrder(payload(), files(), customerId)).id, 'order-id');
  assert.equal(h.calls[0].sql, 'BEGIN');
  assert.match(h.calls[1].sql, /pg_advisory_xact_lock/);
  assert.equal(h.calls.at(-1).sql, 'COMMIT');
  assert.ok(h.released());
  const order = h.calls.find(x => x.sql.includes('INSERT INTO customer_orders'));
  assert.equal(order.values[8], 7);
  assert.equal(order.values[9], '8.5');
  const attachments = h.calls.filter(x => x.sql.includes('INSERT INTO customer_order_attachments'));
  assert.equal(attachments.length, 2);
  assert.equal(attachments[0].values[2], 'Brown cut list');
  assert.equal(attachments[0].values[4], 'Brown');
  assert.equal(attachments[1].values[4], null);
  assert.deepEqual(attachments[1].values[7], Buffer.from('abc'));
  assert.equal(h.calls.find(x => x.sql.includes('INSERT INTO customer_order_accessories')).values[2], '12');
});

for (const failure of ['customer', 'accessory', 'file']) {
  test(`${failure} failure rolls the whole confirmation back and releases the connection`, async () => {
    const h = harness(failure);
    await assert.rejects(h.service.confirmOrder(payload(), files()));
    assert.equal(h.calls.at(-1).sql, 'ROLLBACK');
    assert.ok(!h.calls.some(x => x.sql === 'COMMIT'));
    assert.ok(h.released());
  });
}

test('file/metadata mismatch, duplicate accessories and unsupported files are rejected before any write', async () => {
  const h = harness();
  await assert.rejects(h.service.confirmOrder(payload(), files().slice(0, 1)));
  const duplicate = payload(); duplicate.accessories.push({ itemId, quantity: 1 });
  await assert.rejects(h.service.confirmOrder(duplicate, files()));
  const unsupported = files(); unsupported[0].originalname = 'program.exe';
  await assert.rejects(h.service.confirmOrder(payload(), unsupported));
  assert.equal(h.calls.length, 0);
});

test('no attachments and no accessories are supported without invented requirements', async () => {
  const h = harness();
  await h.service.confirmOrder({ ...payload(), attachments: [], accessories: [], totalBoards: 0, wastePercentage: 0 }, []);
  assert.equal(h.calls.at(-1).sql, 'COMMIT');
});

test('nested server validation rejects invalid quantities, waste and board counts', async () => {
  assert.equal((await validate(plainToInstance(ConfirmOrderDto, payload()))).length, 0);
  for (const invalid of [
    { ...payload(), wastePercentage: 101 }, { ...payload(), wastePercentage: -1 },
    { ...payload(), totalBoards: 1.5 }, { ...payload(), accessories: [{ itemId, quantity: 0 }] },
    { ...payload(), attachments: [{ name: 'Cut', attachmentType: 'unknown' }] },
  ]) assert.ok((await validate(plainToInstance(ConfirmOrderDto, invalid))).length > 0);
});

test('confirmation endpoint rejects malformed JSON and missing authorization without calling persistence', async () => {
  let writes = 0;
  const controller = new OrdersController({ async me() { return { sub: customerId }; } }, { async confirmOrder() { writes++; } });
  await assert.rejects(controller.confirmOrder('', JSON.stringify(payload()), files()));
  await assert.rejects(controller.confirmOrder('Bearer test', '{broken', files()));
  await assert.rejects(controller.confirmOrder('Bearer test', JSON.stringify({ ...payload(), wastePercentage: 101 }), files()));
  assert.equal(writes, 0);
});

test('attachment lookup is scoped to both order and attachment IDs', async () => {
  const service = new OrdersService();
  service.pool = { async query(sql, values) {
    assert.ok(sql.includes('order_id = $1 AND id = $2'));
    assert.deepEqual(values, ['other-order', 'file-id']);
    return { rowCount: 0, rows: [] };
  } };
  await assert.rejects(service.getOrderAttachment('other-order', 'file-id'), error => error.getStatus() === 404);
});

test('material item quantities and searchable order name commit with the whole order',async()=>{
  const h=harness();const b={...payload(),orderName:'Living room order',totalBoards:undefined,materials:[{itemId,quantity:4}]};
  await h.service.confirmOrder(b,files());
  const header=h.calls.find(c=>c.sql.includes('INSERT INTO customer_orders'));assert.equal(header.values[8],null);assert.equal(header.values[10],'Living room order');
  const m=h.calls.find(c=>c.sql.includes('INSERT INTO customer_order_materials'));assert.deepEqual(m.values,['order-id',itemId,'4']);assert.equal(h.calls.at(-1).sql,'COMMIT');
});
test('duplicate materials and conflicting old board totals reject before writing',async()=>{
  const h=harness();await assert.rejects(h.service.confirmOrder({...payload(),totalBoards:undefined,materials:[{itemId,quantity:2},{itemId,quantity:3}]},files()));
  await assert.rejects(h.service.confirmOrder({...payload(),materials:[{itemId,quantity:2}]},files()));assert.equal(h.calls.length,0);
});
for(const fail of ['material','material-precision','material-write'])test(`${fail} rejects and rolls back the entire order`,async()=>{
  const h=harness(fail);await assert.rejects(h.service.confirmOrder({...payload(),totalBoards:undefined,materials:[{itemId,quantity:2}]},files()));assert.equal(h.calls.at(-1).sql,'ROLLBACK');assert.ok(!h.calls.some(c=>c.sql==='COMMIT'));
});
test('new material payload validates without a legacy board total',async()=>{
  const good={...payload(),totalBoards:undefined,materials:[{itemId,quantity:4}]};assert.equal((await validate(plainToInstance(ConfirmOrderDto,good))).length,0);
  assert.ok((await validate(plainToInstance(ConfirmOrderDto,{...good,materials:[{itemId,quantity:0}]}))).length>0);
});

test('confirmation returns shortage quantities without rejecting or issuing stock',async()=>{
 const h=harness('stock-shortage');const result=await h.service.confirmOrder(payload(),files());assert.equal(result.availability.hasShortages,true);assert.equal(result.availability.items[0].shortageQuantity,'3.0000');assert.equal(h.calls.at(-1).sql,'COMMIT');assert.ok(!h.calls.some(q=>/INSERT INTO inventory_|UPDATE inventory_/.test(q.sql)));
});
test('stock report failure rolls back the whole confirmation rather than reporting unchecked success',async()=>{
 const h=harness('stock-report');await assert.rejects(h.service.confirmOrder(payload(),files()));assert.equal(h.calls.at(-1).sql,'ROLLBACK');assert.ok(!h.calls.some(c=>c.sql==='COMMIT'));
});
