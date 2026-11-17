import {BadRequestException,ConflictException,ForbiddenException,Injectable,NotFoundException} from '@nestjs/common';
import {createHash,randomUUID} from 'crypto';
import {Pool,PoolClient} from 'pg';
import {FinanceCommandDto,FinanceConfigDto,FinanceLineDto} from './finance.dto';
import {canonical,decimal,DecimalInputError,multiply,percentage,prorate,rounded,SCALE,units} from './decimal';
type Totals={subtotal:bigint;discount:bigint;net:bigint;tax:bigint;delivery:bigint;cogs:bigint};
const zero=():Totals=>({subtotal:0n,discount:0n,net:0n,tax:0n,delivery:0n,cogs:0n});
const val=(v:any):bigint=>{const text=String(v);if(!/^-?\d+(\.\d{1,4})?$/.test(text))throw new Error('Invalid stored decimal');const negative=text.startsWith('-');const [a,b='']=(negative?text.slice(1):text).split('.');const n=BigInt(a)*SCALE+BigInt(b.padEnd(4,'0'));return negative?-n:n;};
@Injectable()
export class FinanceService {
 private readonly pool=new Pool({connectionString:process.env.DATABASE_URL});
 private hash(value:unknown){return createHash('sha256').update(canonical(value)).digest('hex');}
 private async role(c:PoolClient,user:string,manager=false,read=false){
  const roles=manager?['system_admin','finance_manager']:read?['system_admin','finance_manager','finance_operator','finance_viewer']:['system_admin','finance_manager','finance_operator'];
  const r=await c.query(`SELECT u.id FROM users u JOIN user_roles ur ON ur.user_id=u.id JOIN roles r ON r.id=ur.role_id WHERE u.id=$1 AND u.active AND r.code=ANY($2::text[]) FOR SHARE OF u,ur,r`,[user,roles]);
  if(!r.rowCount)throw new ForbiddenException('ليس لديك صلاحية المالية المطلوبة.');
 }
 async configure(body:FinanceConfigDto,user:string){
  const c=await this.pool.connect();try{await c.query('BEGIN');await this.role(c,user,true);await c.query("SELECT pg_advisory_xact_lock(hashtext('finance-posting-v1'))");
   const hash=this.hash({user,body});const old=await c.query('SELECT detail FROM finance_configuration_audit WHERE id=$1',[body.operationId]);
   if(old.rowCount){if(old.rows[0].detail.hash!==hash)throw new ConflictException('معرّف الإعداد مستخدم ببيانات مختلفة.');await c.query('COMMIT');return {...old.rows[0].detail.result,operationId:body.operationId};}
   let result:any;
   if(body.kind==='account'){
    if(!body.accountType)throw new BadRequestException('اختر نقدية أو بنك.');
    result=(await c.query(`INSERT INTO finance_accounts(code,name_ar,account_type,created_by) VALUES($1,$2,$3,$4) RETURNING id`,[body.code.trim(),body.name.trim(),body.accountType,user])).rows[0];
   }else{
    const rate=units(body.rate);if(rate>100n*SCALE||!body.effectiveFrom)throw new BadRequestException('أدخل نسبة من صفر إلى ١٠٠ وتاريخ السريان وأساس اعتمادها.');
    const latest=await c.query('SELECT effective_from::text FROM finance_tax_policies WHERE code=$1 ORDER BY effective_from DESC LIMIT 1',[body.code.trim().toUpperCase()]);
    if(latest.rowCount&&body.effectiveFrom<=latest.rows[0].effective_from)throw new ConflictException('الإصدار الجديد يجب أن يسري بعد الإصدار السابق.');
    result=(await c.query(`INSERT INTO finance_tax_policies(code,rate,effective_from,basis,approved_by) VALUES($1,$2,$3,$4,$5) RETURNING id`,[body.code.trim().toUpperCase(),decimal(rate),body.effectiveFrom,body.name.trim(),user])).rows[0];
   }
   await c.query('INSERT INTO finance_configuration_audit(id,actor_id,action,record_id,detail) VALUES($1,$2,$3,$4,$5::jsonb)',[body.operationId,user,body.kind,result.id,JSON.stringify({hash,result,body})]);
   await c.query('COMMIT');return {...result,operationId:body.operationId};
  }catch(e){await c.query('ROLLBACK');this.fail(e);}finally{c.release();}
 }
 async execute(b:FinanceCommandDto,user:string){
  b={...b}; // Resolve source party on a private copy; retry payload remains immutable.
  const originalBody={...b};
  const c=await this.pool.connect();try{
   await c.query('BEGIN');await this.role(c,user,['opening_treasury','reversal'].includes(b.kind));
   // Deliberately serialize finance postings at this factory's scale. Existing stock
   // services share item-row locks, so production cannot overspend stock either.
   await c.query("SELECT pg_advisory_xact_lock(hashtext('finance-posting-v1'))");
   const hash=this.hash({user,body:b});const prior=await c.query('SELECT payload_hash,result,serial::text FROM finance_documents WHERE operation_id=$1',[b.operationId]);
   if(prior.rowCount){if(prior.rows[0].payload_hash!==hash)throw new ConflictException('معرّف الحفظ مستخدم لبيانات مختلفة.');await c.query('COMMIT');return {...prior.rows[0].result,serial:prior.rows[0].serial,alreadyPosted:true};}
   const date=await c.query('SELECT $1::date<=CURRENT_DATE AS valid',[b.date]);if(!date.rows[0].valid)throw new BadRequestException('لا يمكن ترحيل تاريخ في المستقبل.');
   await c.query(`INSERT INTO accounting_periods(month) VALUES(date_trunc('month',$1::date)::date) ON CONFLICT DO NOTHING`,[b.date]);
   const period=await c.query(`SELECT status FROM accounting_periods WHERE month=date_trunc('month',$1::date)::date FOR SHARE`,[b.date]);if(period.rows[0].status!=='open')throw new ConflictException('الفترة مغلقة.');
   if(!b.reference?.trim())throw new BadRequestException('مرجع المستند مطلوب.');
   if(b.partyId)await this.party(c,b.partyId,['sale','customer_receipt','invoice'].includes(b.kind)?'customer':['supplier_invoice','purchase_fg','expense','supplier_payment'].includes(b.kind)?'supplier':null);
   if(b.orderId){const order=await c.query(`SELECT id FROM customer_orders WHERE id=$1 AND customer_id=$2 AND status<>'cancelled' FOR SHARE`,[b.orderId,b.partyId]);if(!order.rowCount)throw new BadRequestException('الطلب لا يخص العميل أو ملغى.');}
   const id=randomUUID(),t=zero();let extra:any={};let deferred:()=>Promise<void>=async()=>{};
   // Each branch prepares rows inside the same transaction. The document parent is
   // inserted first with final totals below; child operations execute afterwards.
   if(b.kind==='sale'||b.kind==='purchase_fg'){
    if(!b.partyId||!b.lines?.length)throw new BadRequestException('اختر الطرف وأضف بنود المستند.');
    this.uniqueLines(b.lines);const lines:any[]=[];
    for(const l of [...b.lines].sort((a,d)=>a.itemId.localeCompare(d.itemId)||a.warehouseId.localeCompare(d.warehouseId))){
     const q=units(l.quantity,true),price=units(l.unitPrice),sub=multiply(q,price),discount=units(l.discount??'0');
     if(discount>sub)throw new BadRequestException('الخصم أكبر من قيمة البند.');
     if(b.kind==='purchase_fg'&&(discount!==0n||l.taxPolicyId))throw new BadRequestException('استلام المنتج المشترى يستخدم صافي تكلفة الاقتناء دون ضريبة قابلة للاسترداد؛ سجل الضرائب غير المحسومة بعد اعتماد سياستها.');
     const item=await this.finishedItem(c,l.itemId,l.warehouseId,q);
     const net=sub-discount;let tax=0n;
     if(b.kind==='sale'){
      const policy=await c.query(`SELECT p.rate FROM finance_tax_policies p WHERE p.id=$1 AND p.effective_from<=$2::date
       AND NOT EXISTS(SELECT 1 FROM finance_tax_policies newer WHERE newer.code=p.code AND newer.effective_from>p.effective_from AND newer.effective_from<=$2::date)`,[l.taxPolicyId??null,b.date]);
      if(!policy.rowCount)throw new BadRequestException('اختر سياسة ضريبية معتمدة وسارية، بما فيها الإعفاء أو الصفر المعتمد.');
      tax=percentage(net,val(policy.rows[0].rate));
     }
     const stock=await this.lockStock(c,l.itemId,l.warehouseId,b.date);
     const cost=b.kind==='sale'?this.issueValue(stock,q):net;
     const entry={...l,id:randomUUID(),q,price,sub,discount,net,tax,cost,item};lines.push(entry);
     t.subtotal+=sub;t.discount+=discount;t.net+=net;t.tax+=tax;if(b.kind==='sale')t.cogs+=cost;
     // Update under locks now; rollback covers failures including document insert.
     await this.stock(c,b,id,entry,b.kind==='sale'?-1:1,cost,stock,user);
    }
    t.delivery=units(b.deliveryCharge??'0');if(b.kind==='purchase_fg'&&t.delivery!==0n)throw new BadRequestException('سجل نقل المشتريات كمصروف مستقل.');
    deferred=async()=>{for(const l of lines)await this.line(c,id,l);await this.obligation(c,id,b.partyId!,b.kind==='sale'?'ar':'ap',t.net+t.tax+t.delivery);};
   }else if(b.kind==='sales_return'){
    const source=await this.source(c,b,'sale');if(!['return','cancel'].includes(b.returnType??''))throw new BadRequestException('حدد مرتجع مستلم أم إلغاء كمية لم تسلم.');
    if(!b.lines?.length)throw new BadRequestException('اختر بنود البيع الأصلية والكميات.');this.uniqueLines(b.lines);
    const entries:any[]=[];
    for(const l of [...b.lines].sort((a,d)=>a.itemId.localeCompare(d.itemId)||a.warehouseId.localeCompare(d.warehouseId))){
     const sr=await c.query(`SELECT l.*,COALESCE((SELECT SUM(r.quantity) FROM finance_lines r WHERE r.source_line_id=l.id),0)::text AS returned,
      COALESCE((SELECT SUM(d.quantity) FROM finance_deliveries d WHERE d.sale_line_id=l.id),0)::text AS delivered
      FROM finance_lines l WHERE l.id=$1 AND l.document_id=$2 FOR UPDATE`,[l.sourceLineId,source.id]);
     if(!sr.rowCount)throw new BadRequestException('البند لا يخص البيع المحدد.');const s=sr.rows[0],q=units(l.quantity,true),original=val(s.quantity),returned=val(s.returned),delivered=val(s.delivered);
     if(s.item_id!==l.itemId||s.warehouse_id!==l.warehouseId||q>original-returned||q>(b.returnType==='return'?delivered:original-returned-delivered))throw new ConflictException('الكمية تتجاوز المسلّم القابل للرد أو غير المسلّم القابل للإلغاء.');
     const amounts=await c.query(`SELECT COALESCE(SUM(subtotal),0)::text AS subtotal,COALESCE(SUM(discount),0)::text AS discount,COALESCE(SUM(net),0)::text AS net,COALESCE(SUM(tax),0)::text AS tax,COALESCE(SUM(cogs),0)::text AS cogs FROM finance_lines WHERE source_line_id=$1`,[s.id]);const prev=amounts.rows[0];
     const part=(key:string)=>prorate(val(s[key]),returned+q,original)-val(prev[key]);
     // Derive subtotal from net+discount so independently rounded ratios cannot disagree.
     const net=part('net'),discount=part('discount'),tax=part('tax'),cost=part('cogs');
     const item={code:s.item_code,name_ar:s.item_name,unit_code:s.unit_code};
     const entry={...l,id:randomUUID(),q,price:val(s.unit_price),sub:net+discount,discount,net,tax,cost,item,taxPolicyId:s.tax_policy_id};entries.push(entry);
     t.subtotal+=net+discount;t.discount+=discount;t.net+=net;t.tax+=tax;t.cogs+=cost;
     const stock=await this.lockStock(c,l.itemId,l.warehouseId,b.date);await this.stock(c,b,id,entry,1,cost,stock,user);
    }
    t.delivery=units(b.deliveryCharge??'0');const dc=await c.query(`SELECT COALESCE(SUM(delivery_charge),0)::text AS used FROM finance_documents WHERE source_id=$1 AND kind='sales_return'`,[source.id]);
    if(t.delivery>val(source.delivery_charge)-val(dc.rows[0].used))throw new ConflictException('رد رسوم التوصيل يتجاوز الرسوم الأصلية المتبقية.');
    deferred=async()=>{for(const e of entries){await this.line(c,id,e);if(b.returnType==='return')await c.query('INSERT INTO finance_deliveries VALUES($1,$2,$3)',[id,e.sourceLineId,decimal(-BigInt(e.q))]);}
     const obligation=await c.query('SELECT id FROM finance_obligations WHERE document_id=$1',[source.id]);
     if(t.net+t.tax+t.delivery>0n){await c.query('INSERT INTO finance_credits VALUES($1,$2,$3)',[id,obligation.rows[0].id,decimal(t.net+t.tax+t.delivery)]);await this.releaseExcess(c,id,obligation.rows[0].id);}
    };
   }else if(b.kind==='customer_receipt'||b.kind==='supplier_payment'){
    const amount=units(b.amount,true),side=b.kind==='customer_receipt'?'ar':'ap';t.subtotal=t.net=amount;
    let worker:string|null=null;
    if(!b.partyId){if(side!=='ap'||!b.obligationId)throw new BadRequestException('اختر الطرف أو استحقاق أجر العامل.');const o=await c.query(`SELECT worker_name FROM finance_obligations WHERE id=$1 AND side='ap' AND party_id IS NULL`,[b.obligationId]);if(!o.rowCount)throw new BadRequestException('استحقاق العامل غير موجود.');worker=o.rows[0].worker_name;}
    extra.workerName=worker;
    deferred=async()=>{await this.treasury(c,id,b.accountId,b.date,side==='ar'?amount:-amount);
     const s=(await c.query('INSERT INTO finance_settlements(document_id,party_id,worker_name,side,amount) VALUES($1,$2,$3,$4,$5) RETURNING id',[id,b.partyId??null,worker,side,decimal(amount)])).rows[0];
     for(const a of b.allocations??[])await this.allocate(c,id,s.id,a.obligationId,units(a.amount,true),b.date);
     if(b.obligationId){if(b.allocations?.length)throw new BadRequestException('استخدم الدين المختار أو قائمة التخصيص، دون تكرار.');await this.allocate(c,id,s.id,b.obligationId,amount,b.date);}
    };
   }else if(b.kind==='expense'){
    if(!b.partyId||!b.category||!b.notes?.trim())throw new BadRequestException('اختر المورد وتصنيف المصروف واكتب الوصف. المصروف لا يسجل دفعاً تلقائياً.');
    t.subtotal=t.net=units(b.amount,true);deferred=async()=>{await this.obligation(c,id,b.partyId!,'ap',t.net);};
   }else if(b.kind==='supplier_invoice'||b.kind==='wage_payable'){
    const ids=b.sourceIds??[];if(!ids.length||new Set(ids).size!==ids.length)throw new BadRequestException('اختر مصادر غير مكررة.');
    if(b.kind==='supplier_invoice'&&(!b.partyId||b.sourceType!=='inventory_receipt'))throw new BadRequestException('فاتورة المورد تربط الاستلام المخزني الأصلي، ولا تعيد استلامه.');
    if(b.kind==='wage_payable'&&(ids.length!==1||!['wage_accrual','legacy_labor'].includes(b.sourceType??'')))throw new BadRequestException('اختر استحقاق أجر واحداً.');
    let worker:string|null=null;
    for(const sourceId of ids){
     let row:any;
     if(b.kind==='supplier_invoice')row=(await c.query(`SELECT total_cost::text AS amount,movement_date::text AS date FROM inventory_movements WHERE id=$1 AND movement_type='receipt' AND party_id=$2 FOR SHARE`,[sourceId,b.partyId])).rows[0];
     else if(b.sourceType==='wage_accrual')row=(await c.query(`SELECT a.amount::text,a.posting_date::text AS date,w.worker_name FROM production_wage_accruals a JOIN production_wage_agreements w ON w.id=a.agreement_id WHERE a.id=$1 FOR SHARE OF a,w`,[sourceId])).rows[0];
     else row=(await c.query(`SELECT amount::text,posting_date::text AS date,worker_name FROM production_labor_entries WHERE id=$1 FOR SHARE`,[sourceId])).rows[0];
     if(!row||row.date>b.date)throw new BadRequestException('مصدر غير صالح أو تاريخ المستند يسبق المصدر.');t.net+=val(row.amount);worker=row.worker_name??null;
    }
    if(t.net<=0n)throw new BadRequestException('المصدر لا يحتوي مبلغاً مستحقاً.');t.subtotal=t.net;extra.workerName=worker;
    deferred=async()=>{for(const sid of ids)await c.query('INSERT INTO finance_source_links VALUES($1,$2,$3)',[b.sourceType,sid,id]);await this.obligation(c,id,b.kind==='supplier_invoice'?b.partyId!:null,'ap',t.net,worker);};
   }else if(b.kind==='allocation'){
    if(!b.allocations?.length)throw new BadRequestException('أضف تخصيص الدفعة للدين.');
    deferred=async()=>{for(const a of b.allocations!)await this.allocate(c,id,a.settlementId,a.obligationId,units(a.amount,true),b.date);};
   }else if(b.kind==='transfer'){
    t.subtotal=t.net=units(b.amount,true);if(!b.accountId||!b.targetAccountId||b.accountId===b.targetAccountId)throw new BadRequestException('اختر حسابين مختلفين.');
    deferred=async()=>{await this.treasury(c,id,b.accountId,b.date,-t.net);await this.treasury(c,id,b.targetAccountId,b.date,t.net);};
   }else if(b.kind==='opening_treasury'){
    t.subtotal=t.net=units(b.amount,true);if(!b.notes?.trim())throw new BadRequestException('اكتب مستند اعتماد الرصيد الافتتاحي.');
    const old=await c.query('SELECT id FROM finance_treasury_movements WHERE account_id=$1 LIMIT 1',[b.accountId]);if(old.rowCount)throw new ConflictException('الرصيد الافتتاحي يسبق أول حركة على الحساب.');
    deferred=async()=>{await this.treasury(c,id,b.accountId,b.date,t.net);};
   }else if(b.kind==='customer_refund'){
    const source=await this.source(c,b,'customer_receipt');t.subtotal=t.net=units(b.amount,true);
    const s=await c.query('SELECT available::text FROM finance_settlement_balances WHERE document_id=$1',[source.id]);if(!s.rowCount||t.net>val(s.rows[0].available))throw new ConflictException('الرد يتجاوز رصيد دفعة العميل غير المخصص.');
    deferred=async()=>{await this.treasury(c,id,b.accountId,b.date,-t.net);};
   }else if(b.kind==='invoice'){
    if(!b.partyId||!b.saleIds?.length)throw new BadRequestException('اختر العميل ومبيعاته لإصدار مستند داخلي.');
    for(const sid of b.saleIds){const r=await c.query(`SELECT * FROM finance_documents WHERE id=$1 AND kind='sale' AND party_id=$2 AND posting_date<=$3::date`,[sid,b.partyId,b.date]);if(!r.rowCount)throw new BadRequestException('البيع لا يخص العميل أو تاريخه لاحق.');const s=r.rows[0];for(const k of ['subtotal','discount','net','tax','cogs'] as const)t[k]+=val(s[k]);t.delivery+=val(s.delivery_charge);}
    deferred=async()=>{for(const sid of b.saleIds!)await c.query('INSERT INTO finance_invoice_sales VALUES($1,$2)',[id,sid]);};
   }else if(b.kind==='delivery'){
    const source=await this.source(c,b,'sale');if(!b.lines?.length)throw new BadRequestException('اختر بنود البيع والكميات المسلمة.');this.uniqueLines(b.lines);
    deferred=async()=>{for(const l of b.lines!){const r=await c.query(`SELECT l.quantity::text,COALESCE((SELECT SUM(quantity) FROM finance_lines r WHERE r.source_line_id=l.id),0)::text AS returned,
     COALESCE((SELECT SUM(quantity) FROM finance_deliveries d WHERE d.sale_line_id=l.id),0)::text AS delivered FROM finance_lines l WHERE l.id=$1 AND l.document_id=$2 AND l.item_id=$3 AND l.warehouse_id=$4`,[l.sourceLineId,source.id,l.itemId,l.warehouseId]);
     const s=r.rows[0],q=units(l.quantity,true);if(!s||q>val(s.quantity)-val(s.returned)-val(s.delivered))throw new ConflictException('التسليم يتجاوز الكمية المتبقية.');await c.query('INSERT INTO finance_deliveries VALUES($1,$2,$3)',[id,l.sourceLineId,decimal(q)]);}};
   }else if(b.kind==='reversal'){
    const source=await this.source(c,b);if(!b.notes?.trim())throw new BadRequestException('سبب التصحيح مطلوب.');
    if(!['customer_receipt','supplier_payment','expense','transfer'].includes(source.kind))throw new BadRequestException('هذا المستند يصحح بمرتجع/إلغاء مرتبط؛ لا يمكن عكسه مباشرة.');
    const refunds=await c.query(`SELECT id FROM finance_documents WHERE source_id=$1 AND kind IN ('customer_refund','reversal')`,[source.id]);if(refunds.rowCount)throw new ConflictException('المستند سبق رده أو عكسه.');
    for(const k of ['subtotal','discount','net','tax','cogs'] as const)t[k]=val(source[k]);t.delivery=val(source.delivery_charge);
    deferred=async()=>{
     const ob=await c.query('SELECT * FROM finance_obligation_balances WHERE document_id=$1',[source.id]);
     if(ob.rowCount){if(val(ob.rows[0].allocated)!==0n)throw new ConflictException('المصروف مرتبط بمدفوعات؛ اعكس الدفع أولاً.');await c.query('INSERT INTO finance_credits VALUES($1,$2,$3)',[id,ob.rows[0].id,ob.rows[0].amount]);}
     const st=await c.query('SELECT id FROM finance_settlements WHERE document_id=$1',[source.id]);
     if(st.rowCount){const alloc=await c.query('SELECT obligation_id,SUM(amount)::text AS amount FROM finance_allocations WHERE settlement_id=$1 GROUP BY obligation_id HAVING SUM(amount)>0',[st.rows[0].id]);for(const a of alloc.rows)await c.query('INSERT INTO finance_allocations(document_id,settlement_id,obligation_id,amount) VALUES($1,$2,$3,$4)',[id,st.rows[0].id,a.obligation_id,decimal(-val(a.amount))]);}
     const legs=await c.query('SELECT account_id,amount::text FROM finance_treasury_movements WHERE document_id=$1 ORDER BY amount DESC',[source.id]);for(const leg of legs.rows)await this.treasury(c,id,leg.account_id,b.date,-val(leg.amount));
    };
   }else throw new BadRequestException('نوع المستند غير مدعوم.');
   const total=t.net+t.tax+t.delivery;
   const partyName=b.partyId?(await c.query('SELECT name_ar FROM parties WHERE id=$1',[b.partyId])).rows[0]?.name_ar:extra.workerName;
   const doc=await c.query(`INSERT INTO finance_documents(id,operation_id,payload_hash,actor_id,kind,reference,posting_date,party_id,customer_order_id,source_id,account_id,subtotal,discount,net,tax,delivery_charge,total,cogs,category,notes,result,party_name,request_payload)
    VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21::jsonb,$22,$23::jsonb) RETURNING serial::text`,
    [id,b.operationId,hash,user,b.kind,b.reference.trim(),b.date,b.partyId??null,b.orderId??null,
     b.sourceId??null,b.accountId??null,decimal(t.subtotal),decimal(t.discount),decimal(t.net),decimal(t.tax),decimal(t.delivery),decimal(total),decimal(t.cogs),b.category??null,b.notes?.trim()||null,
     JSON.stringify({operationId:b.operationId,id,kind:b.kind,total:decimal(total),net:decimal(t.net),tax:decimal(t.tax),cogs:decimal(t.cogs),...extra}),partyName??'',JSON.stringify(originalBody)]);
   await deferred();await this.invariants(c);await c.query('COMMIT');
   return {operationId:b.operationId,id,kind:b.kind,total:decimal(total),net:decimal(t.net),tax:decimal(t.tax),cogs:decimal(t.cogs),serial:doc.rows[0].serial,alreadyPosted:false,...extra};
  }catch(e){await c.query('ROLLBACK');this.fail(e);}finally{c.release();}
 }
 private uniqueLines(lines:FinanceLineDto[]){const keys=lines.map(l=>l.itemId+':'+l.warehouseId);if(new Set(keys).size!==keys.length)throw new BadRequestException('اجمع كمية الصنف في المخزن نفسه في بند واحد.');}
 private async party(c:PoolClient,id:string,type:string|null){const r=await c.query(`SELECT id FROM parties WHERE id=$1 AND active AND ($2::text IS NULL OR party_type IN ($2,'both')) FOR SHARE`,[id,type]);if(!r.rowCount)throw new BadRequestException('الطرف غير موجود أو غير نشط أو ليس من النوع المطلوب.');}
 private async source(c:PoolClient,b:FinanceCommandDto,kind?:string){const r=await c.query('SELECT *,posting_date::text AS posting_date FROM finance_documents WHERE id=$1 FOR UPDATE',[b.sourceId]);if(!r.rowCount)throw new NotFoundException('المستند الأصلي غير موجود.');const s=r.rows[0];
  if(kind&&s.kind!==kind||b.partyId&&s.party_id!==b.partyId||b.date<this.dateText(s.posting_date))throw new BadRequestException('المستند الأصلي أو الطرف أو التاريخ غير متوافق.');
  b.partyId=s.party_id??undefined;return s;
 }
 private async finishedItem(c:PoolClient,item:string,warehouse:string,q:bigint){const r=await c.query(`SELECT i.code,i.name_ar,u.code AS unit_code,u.precision_digits FROM items i JOIN units_of_measure u ON u.id=i.base_unit_id WHERE i.id=$1 AND i.active AND i.stock_tracked AND i.item_type='finished_good' AND i.classification='materials' FOR SHARE OF i,u`,[item]);
  if(!r.rowCount)throw new BadRequestException('اختر منتجاً تاماً مخزنياً نشطاً.');const w=await c.query(`SELECT id FROM warehouses WHERE id=$1 AND active AND warehouse_type='finished_goods' FOR SHARE`,[warehouse]);if(!w.rowCount)throw new BadRequestException('اختر مخزن منتجات تامة.');
  if(r.rows[0].precision_digits<4&&q%10n**BigInt(4-r.rows[0].precision_digits)!==0n)throw new BadRequestException('الكمية لا توافق دقة وحدة الصنف.');return r.rows[0];
 }
 private async lockStock(c:PoolClient,item:string,warehouse:string,date:string){
  await c.query('INSERT INTO inventory_item_costs(item_id) VALUES($1) ON CONFLICT DO NOTHING',[item]);
  const r=await c.query('SELECT quantity_on_hand::text,inventory_value::text,average_unit_cost::text FROM inventory_item_costs WHERE item_id=$1 FOR UPDATE',[item]);
  await c.query('INSERT INTO inventory_balances(item_id,warehouse_id) VALUES($1,$2) ON CONFLICT DO NOTHING',[item,warehouse]);
  const w=await c.query('SELECT quantity_on_hand::text FROM inventory_balances WHERE item_id=$1 AND warehouse_id=$2 FOR UPDATE',[item,warehouse]);
  const latest=await c.query('SELECT MAX(movement_date)::text AS date FROM inventory_movements WHERE item_id=$1',[item]);if(latest.rows[0].date&&date<latest.rows[0].date)throw new ConflictException('تاريخ حركة المنتج يسبق آخر حركة تكلفة؛ استخدم تاريخاً حالياً أو تصحيحاً معتمداً.');
  return {...r.rows[0],warehouse_quantity:w.rows[0].quantity_on_hand};
 }
 private issueValue(s:any,q:bigint){if(q>val(s.quantity_on_hand)||q>val(s.warehouse_quantity))throw new ConflictException('رصيد المنتج التام لا يكفي. المقبول بانتظار التكلفة ليس مخزوناً متاحاً للبيع.');
  return q===val(s.quantity_on_hand)?val(s.inventory_value):prorate(val(s.inventory_value),q,val(s.quantity_on_hand));
 }
 private async stock(c:PoolClient,b:FinanceCommandDto,doc:string,e:any,direction:number,cost:bigint,s:any,user:string){
  const q=val(s.quantity_on_hand)+BigInt(direction)*e.q,v=val(s.inventory_value)+BigInt(direction)*cost,avg=q>0n?rounded(v*SCALE,q):0n;
  if(q<0n||v<0n)throw new ConflictException('رصيد مخزني غير صالح.');
  await c.query('UPDATE inventory_item_costs SET quantity_on_hand=$2,inventory_value=$3,average_unit_cost=$4,updated_at=now() WHERE item_id=$1',[e.itemId,decimal(q),decimal(v),decimal(avg)]);
  await c.query('UPDATE inventory_balances SET quantity_on_hand=quantity_on_hand+$3::numeric,updated_at=now() WHERE item_id=$1 AND warehouse_id=$2',[e.itemId,e.warehouseId,decimal(BigInt(direction)*e.q)]);
  await c.query(`INSERT INTO inventory_movements(operation_id,movement_type,item_id,warehouse_id,party_id,document_reference,movement_date,quantity,unit_cost,total_cost,average_cost_after,quantity_after,value_after,notes,posted_by)
   VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)`,[e.id,b.kind==='sale'?'sale_issue':b.kind==='purchase_fg'?'finished_purchase':'sales_return',e.itemId,e.warehouseId,b.partyId??null,b.reference.trim(),b.date,decimal(e.q),decimal(rounded(cost*SCALE,e.q)),decimal(cost),decimal(avg),decimal(q),decimal(v),'finance:'+doc,user]);
 }
 private async line(c:PoolClient,doc:string,e:any){await c.query(`INSERT INTO finance_lines(id,document_id,item_id,warehouse_id,source_line_id,quantity,unit_price,subtotal,discount,net,tax,total,cogs,tax_policy_id,item_code,item_name,unit_code)
  VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17)`,[e.id,doc,e.itemId,e.warehouseId,e.sourceLineId??null,decimal(e.q),decimal(e.price),decimal(e.sub),decimal(e.discount),decimal(e.net),decimal(e.tax),decimal(e.net+e.tax),decimal(e.cost),e.taxPolicyId??null,e.item.code,e.item.name_ar,e.item.unit_code]);}
 private async obligation(c:PoolClient,doc:string,party:string|null,side:string,amount:bigint,worker:string|null=null){if(amount>0n)await c.query('INSERT INTO finance_obligations(document_id,party_id,worker_name,side,amount) VALUES($1,$2,$3,$4,$5)',[doc,party,worker,side,decimal(amount)]);}
 private async treasury(c:PoolClient,doc:string,account:string|undefined,date:string,amount:bigint){
  if(!amount)return;const r=await c.query('SELECT id FROM finance_accounts WHERE id=$1 AND active FOR UPDATE',[account]);if(!r.rowCount)throw new BadRequestException('اختر حساب نقدية أو بنك نشطاً.');
  const s=(await c.query('SELECT COALESCE(SUM(amount),0)::text AS balance,MAX(posting_date)::text AS latest FROM finance_treasury_movements WHERE account_id=$1',[account])).rows[0];
  if(s.latest&&date<s.latest)throw new ConflictException('تاريخ الخزينة يسبق آخر حركة؛ يمنع تغيير الأرصدة التاريخية.');if(val(s.balance)+amount<0n)throw new ConflictException('رصيد الخزينة لا يكفي.');
  await c.query('INSERT INTO finance_treasury_movements(document_id,account_id,amount,posting_date) VALUES($1,$2,$3,$4)',[doc,account,decimal(amount),date]);
 }
 private async allocate(c:PoolClient,doc:string,settlement:string,obligation:string,amount:bigint,date:string){
  const s=(await c.query('SELECT *,available::text,posting_date::text AS posting_date FROM finance_settlement_balances WHERE id=$1',[settlement])).rows[0];const o=(await c.query('SELECT *,outstanding::text,posting_date::text AS posting_date FROM finance_obligation_balances WHERE id=$1',[obligation])).rows[0];
  if(!s||!o||s.side!==o.side||s.party_id!==o.party_id||(s.worker_name??null)!==(o.worker_name??null)||amount>val(s.available)||amount>val(o.outstanding)||date<this.dateText(s.posting_date)||date<this.dateText(o.posting_date))throw new ConflictException('التخصيص يتجاوز المتاح أو الدين، أو يخص طرفاً مختلفاً أو تاريخاً سابقاً.');
  await c.query('INSERT INTO finance_allocations(document_id,settlement_id,obligation_id,amount) VALUES($1,$2,$3,$4)',[doc,settlement,obligation,decimal(amount)]);
 }
 private dateText(d:any){return typeof d==='string'?d.slice(0,10):d.toISOString().slice(0,10);}
 private async releaseExcess(c:PoolClient,doc:string,obligation:string){const o=(await c.query('SELECT outstanding::text FROM finance_obligation_balances WHERE id=$1',[obligation])).rows[0];let excess=-val(o.outstanding);
  if(excess<=0n)return;const a=await c.query(`SELECT settlement_id,SUM(amount)::text AS amount FROM finance_allocations WHERE obligation_id=$1 GROUP BY settlement_id HAVING SUM(amount)>0 ORDER BY settlement_id`,[obligation]);
  for(const row of a.rows){const amount=val(row.amount)<excess?val(row.amount):excess;await c.query('INSERT INTO finance_allocations(document_id,settlement_id,obligation_id,amount) VALUES($1,$2,$3,$4)',[doc,row.settlement_id,obligation,decimal(-amount)]);excess-=amount;if(!excess)break;}
 }
 private async invariants(c:PoolClient){const r=await c.query(`SELECT EXISTS(SELECT 1 FROM finance_obligation_balances WHERE outstanding<0 OR credited<0 OR credited>amount OR allocated<0)
  OR EXISTS(SELECT 1 FROM finance_settlement_balances WHERE available<0 OR allocated<0)
  OR EXISTS(SELECT 1 FROM finance_treasury_movements GROUP BY account_id HAVING SUM(amount)<0) AS invalid`);if(r.rows[0].invalid)throw new ConflictException('فشل تطابق الأرصدة؛ لم تُحفظ أي آثار للمعاملة.');}
 private fail(e:unknown):never{const error=e as any;if(error.getStatus)throw error;if(['23505','23514','23503','22003','22007'].includes(error.code))throw new ConflictException('مرجع مكرر أو بيانات غير متوافقة؛ لم تحفظ المعاملة. راجع المصدر والأرصدة.');if(error instanceof DecimalInputError)throw new BadRequestException(error.message);throw e;}
 async report(user:string){const c=await this.pool.connect();try{await c.query('BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY');await this.roleRead(c,user);
  const accounts=(await c.query(`SELECT a.id,a.code,a.name_ar,a.account_type,a.currency,COALESCE(SUM(m.amount),0)::text AS balance FROM finance_accounts a LEFT JOIN finance_treasury_movements m ON m.account_id=a.id GROUP BY a.id ORDER BY a.code`)).rows;
  const documents=(await c.query(`SELECT d.*,d.posting_date::text AS posting_date,u.display_name AS actor,d.party_name AS party FROM finance_documents d JOIN users u ON u.id=d.actor_id ORDER BY serial DESC LIMIT 1000`)).rows;
  const obligations=(await c.query(`SELECT b.*,b.posting_date::text AS posting_date,p.name_ar AS party FROM finance_obligation_balances b LEFT JOIN parties p ON p.id=b.party_id ORDER BY b.posting_date,b.serial`)).rows;
  const settlements=(await c.query(`SELECT b.*,b.posting_date::text AS posting_date,p.name_ar AS party FROM finance_settlement_balances b LEFT JOIN parties p ON p.id=b.party_id ORDER BY b.posting_date,b.serial`)).rows;
  const policies=(await c.query('SELECT *,effective_from::text AS effective_from FROM finance_tax_policies ORDER BY code,effective_from DESC')).rows;
  const lines=(await c.query(`SELECT l.*,d.reference,d.party_id,d.kind,d.posting_date::text AS posting_date,COALESCE((SELECT SUM(r.quantity) FROM finance_lines r WHERE r.source_line_id=l.id),0)::text AS returned,COALESCE((SELECT SUM(x.quantity) FROM finance_deliveries x WHERE x.sale_line_id=l.id),0)::text AS delivered FROM finance_lines l JOIN finance_documents d ON d.id=l.document_id ORDER BY d.serial DESC`)).rows;
  const invoiceSales=(await c.query('SELECT * FROM finance_invoice_sales')).rows;
  const treasury=(await c.query(`SELECT m.*,d.reference,d.kind,a.name_ar FROM finance_treasury_movements m JOIN finance_documents d ON d.id=m.document_id JOIN finance_accounts a ON a.id=m.account_id ORDER BY d.serial DESC LIMIT 1000`)).rows;
  const summary=(await c.query(`WITH sales AS(SELECT COALESCE(SUM(CASE WHEN kind='sale' THEN net ELSE -net END),0) AS net,COALESCE(SUM(CASE WHEN kind='sale' THEN cogs ELSE -cogs END),0) AS cogs,COALESCE(SUM(CASE WHEN kind='sale' THEN tax ELSE -tax END),0) AS tax,COALESCE(SUM(CASE WHEN kind='sale' THEN delivery_charge ELSE -delivery_charge END),0) AS delivery FROM finance_documents WHERE kind IN ('sale','sales_return'))
   SELECT net::text,cogs::text,tax::text,delivery::text,(net-cogs)::text AS profit,CASE WHEN net>0 THEN ROUND((net-cogs)/net*100,4)::text ELSE NULL END AS margin,
   (SELECT COALESCE(SUM(amount),0)::text FROM finance_treasury_movements) AS treasury,
   (SELECT COALESCE(SUM(outstanding),0)::text FROM finance_obligation_balances WHERE side='ar') AS receivables,
   (SELECT COALESCE(SUM(outstanding),0)::text FROM finance_obligation_balances WHERE side='ap') AS payables,
   (SELECT COALESCE(SUM(available),0)::text FROM finance_settlement_balances WHERE side='ar') AS advances FROM sales`)).rows[0];
  const receipts=(await c.query(`SELECT m.id,m.document_reference,m.party_id,m.total_cost::text AS amount,m.movement_date::text AS date,p.name_ar AS party FROM inventory_movements m JOIN parties p ON p.id=m.party_id WHERE m.movement_type='receipt' AND m.total_cost>0 AND NOT EXISTS(SELECT 1 FROM finance_source_links s WHERE s.source_type='inventory_receipt' AND s.source_id=m.id) ORDER BY m.movement_date,m.id`)).rows;
  const wages=(await c.query(`SELECT a.id,'wage_accrual' AS source_type,w.worker_name,a.amount::text,a.posting_date::text AS date FROM production_wage_accruals a JOIN production_wage_agreements w ON w.id=a.agreement_id WHERE a.amount>0 AND NOT EXISTS(SELECT 1 FROM finance_source_links s WHERE s.source_type='wage_accrual' AND s.source_id=a.id)
   UNION ALL SELECT a.id,'legacy_labor',a.worker_name,a.amount::text,a.posting_date::text FROM production_labor_entries a WHERE a.amount>0 AND NOT EXISTS(SELECT 1 FROM finance_source_links s WHERE s.source_type='legacy_labor' AND s.source_id=a.id)`)).rows;
  const stock=(await c.query(`SELECT i.id AS item_id,i.code,i.name_ar,w.name_ar AS warehouse,b.warehouse_id,b.quantity_on_hand::text AS quantity,c.average_unit_cost::text AS unit_cost FROM inventory_balances b JOIN items i ON i.id=b.item_id JOIN warehouses w ON w.id=b.warehouse_id JOIN inventory_item_costs c ON c.item_id=i.id WHERE i.item_type='finished_good' AND w.warehouse_type='finished_goods' ORDER BY i.code,w.code`)).rows;
  const orders=(await c.query(`SELECT id,order_no,product_name,customer_id FROM customer_orders WHERE status<>'cancelled' ORDER BY order_no`)).rows;
  const reconciliations=(await c.query(`SELECT
   (SELECT COUNT(*)::int FROM finance_obligation_balances WHERE outstanding<0 OR allocated<0 OR credited>amount) AS invalid_obligations,
   (SELECT COUNT(*)::int FROM finance_settlement_balances WHERE available<0 OR allocated<0) AS invalid_settlements,
   (SELECT COUNT(*)::int FROM finance_documents d WHERE kind='transfer' AND ((SELECT COUNT(*) FROM finance_treasury_movements m WHERE m.document_id=d.id)<>2 OR (SELECT SUM(amount) FROM finance_treasury_movements m WHERE m.document_id=d.id)<>0)) AS invalid_transfers,
   (SELECT COUNT(*)::int FROM finance_documents d WHERE kind IN ('sale','sales_return') AND (d.net<>(SELECT COALESCE(SUM(net),0) FROM finance_lines l WHERE l.document_id=d.id) OR d.tax<>(SELECT COALESCE(SUM(tax),0) FROM finance_lines l WHERE l.document_id=d.id) OR d.cogs<>(SELECT COALESCE(SUM(cogs),0) FROM finance_lines l WHERE l.document_id=d.id))) AS invalid_sales,
   (SELECT COUNT(*)::int FROM inventory_item_costs c WHERE c.quantity_on_hand<>(SELECT COALESCE(SUM(b.quantity_on_hand),0) FROM inventory_balances b WHERE b.item_id=c.item_id)) AS invalid_stock_quantities,
   (SELECT COUNT(*)::int FROM inventory_item_costs c WHERE c.inventory_value<>(SELECT COALESCE(SUM(CASE WHEN movement_type IN ('production_issue','sale_issue') THEN -total_cost ELSE total_cost END),0) FROM inventory_movements m WHERE m.item_id=c.item_id)) AS invalid_stock_values`)).rows[0];
  await c.query('COMMIT');return {accounts,documents,obligations,settlements,policies,lines,treasury,summary,receipts,wages,reconciliations,stock,orders,invoiceSales};
 }catch(e){await c.query('ROLLBACK');throw e;}finally{c.release();}}
 private async roleRead(c:PoolClient,user:string){const r=await c.query(`SELECT u.id FROM users u JOIN user_roles ur ON ur.user_id=u.id JOIN roles r ON r.id=ur.role_id WHERE u.id=$1 AND u.active AND r.code=ANY($2::text[])`,[user,['system_admin','finance_manager','finance_operator','finance_viewer']]);if(!r.rowCount)throw new ForbiddenException('ليس لديك صلاحية عرض المالية.');}
}
