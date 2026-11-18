const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');

test('017 keeps complete PostgreSQL dollar-quoted function bodies inside its transaction',()=>{
 const sql=fs.readFileSync(path.join(__dirname,'../database/postgres/migrations/017_sales_pricing_and_finished_goods.sql'),'utf8');
 assert.match(sql,/^BEGIN;/);
 assert.match(sql,/COMMIT;\s*$/);
 const declarations=[...sql.matchAll(/CREATE OR REPLACE FUNCTION (\w+)\(\) RETURNS trigger LANGUAGE plpgsql AS ([^\r\n]+)/g)];
 assert.equal(declarations.length,4);
 for(const declaration of declarations){
  assert.equal(declaration[2],'$$',declaration[1]+' requires a complete opening dollar quote');
  const tail=sql.slice(declaration.index+declaration[0].length);
  const end=tail.indexOf('END $$;');
  assert.ok(end>=0,declaration[1]+' requires a complete closing dollar quote');
  assert.ok(!tail.slice(0,end).includes('CREATE OR REPLACE FUNCTION'),declaration[1]+' must end before the next function');
 }
 assert.doesNotMatch(sql,/AS \$(?!\$)|END \$(?!\$)/);
});
