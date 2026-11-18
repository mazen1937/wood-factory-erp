import {BadRequestException,ConflictException,ForbiddenException,Injectable} from '@nestjs/common';
import {Pool} from 'pg';import {createHash} from 'node:crypto';
import {SellingPriceDto} from './finance.dto';import {canonical,decimal,units} from './decimal';
@Injectable()
export class SellingPricesService{
 private readonly pool=new Pool({connectionString:process.env.DATABASE_URL});
 async save(b:SellingPriceDto,user:string){
  let amount:bigint;try{amount=units(b.unitPrice);}catch(e){throw new BadRequestException((e as Error).message);}const hash=createHash('sha256').update(canonical({user,b})).digest('hex');const c=await this.pool.connect();
  try{await c.query('BEGIN');const role=await c.query(`SELECT u.id FROM users u JOIN user_roles ur ON ur.user_id=u.id JOIN roles r ON r.id=ur.role_id WHERE u.id=$1 AND u.active AND r.code=ANY($2::text[]) FOR SHARE OF u,ur,r`,[user,['system_admin','finance_manager','finance_operator']]);if(!role.rowCount)throw new ForbiddenException();
   await c.query('SELECT pg_advisory_xact_lock(hashtext($1))',[b.operationId]);const prior=(await c.query('SELECT id,payload_hash FROM product_selling_prices WHERE operation_id=$1',[b.operationId])).rows[0];
   if(prior){if(prior.payload_hash!==hash)throw new ConflictException('معرّف العملية مستخدم لسعر مختلف.');await c.query('COMMIT');return {operationId:b.operationId,id:prior.id,alreadyPosted:true};}
   const item=await c.query(`SELECT id FROM items WHERE id=$1 AND active AND stock_tracked AND classification='materials' AND item_type='finished_good' FOR SHARE`,[b.itemId]);if(!item.rowCount)throw new BadRequestException('اختر صنف منتج نهائي مخزني.');
   if(b.orderId&&!(await c.query('SELECT id FROM customer_orders WHERE id=$1 AND status<>\'cancelled\' FOR SHARE',[b.orderId])).rowCount)throw new BadRequestException('طلب العميل غير متاح.');
   const row=(await c.query(`INSERT INTO product_selling_prices(operation_id,payload_hash,item_id,customer_order_id,unit_price,effective_date,notes,actor_id) VALUES($1,$2,$3,$4,$5::numeric,$6::date,$7,$8) RETURNING id`,[b.operationId,hash,b.itemId,b.orderId??null,decimal(amount),b.date,b.notes?.trim()||null,user])).rows[0];
   await c.query('COMMIT');return {operationId:b.operationId,id:row.id,unitPrice:decimal(amount),alreadyPosted:false};
  }catch(e){await c.query('ROLLBACK');if((e as any).code==='23505')throw new ConflictException('معرّف العملية مستخدم بالفعل.');throw e;}finally{c.release();}
 }
}
