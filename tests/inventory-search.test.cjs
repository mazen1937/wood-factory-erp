const {test}=require('node:test');const assert=require('node:assert/strict');
require('../apps/api/node_modules/reflect-metadata');
const {InventoryService}=require('../apps/api/dist/inventory/inventory.service');
const {InventoryController}=require('../apps/api/dist/inventory/inventory.controller');
test('movement search binds literal tokens and searches before limiting the full history',async()=>{
 const service=new InventoryService();let query;
 service.pool={query:async(sql,args)=>{query={sql,args};return {rows:[{itemCode:'OLD'}]};}};
 assert.deepEqual(await service.movements("MDF 50%_\\ '"),[{itemCode:'OLD'}]);
 assert.deepEqual(query.args,[['%MDF%','%50\\%\\_\\\\%',"%'%"]]);
 assert.ok(query.sql.indexOf('WHERE NOT EXISTS')<query.sql.indexOf('LIMIT 250'));
 for(const field of ['i.name_en','w.name_ar','p.name_ar','m.document_reference','m.notes'])assert.ok(query.sql.includes(field));
});
test('empty movement search retains newest-250 behavior without filtering any terms',async()=>{
 const service=new InventoryService();service.pool={query:async(sql,args)=>{assert.deepEqual(args,[[]]);return {rows:[]};}};assert.deepEqual(await service.movements(),[]);
});
test('movement search requires authentication, validates length and forwards valid text',async()=>{
 const searches=[];const controller=new InventoryController({me:async()=>({})},{movements:async q=>{searches.push(q);return [];}},{});
 await assert.rejects(controller.movements('', 'MDF'));await assert.rejects(controller.movements('Bearer session','x'.repeat(201)));
 await controller.movements('Bearer session','MDF old-ref');assert.deepEqual(searches,['MDF old-ref']);
});
