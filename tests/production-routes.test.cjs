const {test}=require('node:test');const assert=require('node:assert/strict');
require('../apps/api/node_modules/reflect-metadata');
const {NestFactory}=require('../apps/api/node_modules/@nestjs/core');
const {ValidationPipe}=require('../apps/api/node_modules/@nestjs/common');
const {AppModule}=require('../apps/api/dist/app.module');
test('built API registers both worker routes and protects them with authentication',{timeout:20000},async()=>{
 const app=await NestFactory.create(AppModule,{logger:false});
 try{
  app.setGlobalPrefix('api');app.useGlobalPipes(new ValidationPipe({whitelist:true,transform:true}));await app.listen(0,'127.0.0.1');
  const base=await app.getUrl();
  for(const method of ['GET','POST']){
   const response=await fetch(base+'/api/production/workers',{method,headers:{'Content-Type':'application/json'},...(method==='POST'?{body:JSON.stringify({id:'12345678-1234-4234-8234-123456789012',code:'W-TEST',nameAr:'Test'})}:{})});
   assert.equal(response.status,401,`${method} workers must reach authentication rather than return a missing-route error`);
  }
  for(const endpoint of ['finance/monthly?month=2026-01','finance/orders/12345678-1234-4234-8234-123456789012/activity'])assert.equal((await fetch(base+'/api/'+endpoint)).status,401,'Monthly finance routes must require authentication');
  assert.equal((await fetch(base+'/api/finance/prices',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({operationId:'12345678-1234-4234-8234-123456789012',itemId:'12345678-1234-4234-8234-123456789013',unitPrice:'150',date:'2026-01-15'})})).status,401,'Selling-price route must exist and require authentication');
 }finally{await app.close();}
});
