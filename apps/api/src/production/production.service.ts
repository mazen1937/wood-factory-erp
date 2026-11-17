import { BadRequestException, ConflictException, ForbiddenException, Injectable, NotFoundException } from '@nestjs/common';
import { createHash } from 'node:crypto';
import { Pool, PoolClient } from 'pg';
import { ExecutionCommandDto, LaborRateDto } from './production.dto';

export function canonical(value: any): string {
  if (Array.isArray(value)) return '[' + value.map(canonical).join(',') + ']';
  if (value && typeof value === 'object') return '{' + Object.keys(value).filter(k=>value[k]!==undefined).sort().map(k=>JSON.stringify(k)+':'+canonical(value[k])).join(',') + '}';
  return JSON.stringify(value);
}

@Injectable()
export class ProductionService {
  private readonly pool = new Pool({connectionString:process.env.DATABASE_URL});
  async authorize(userId: string, configure=false) {
    const result = await this.pool.query(`SELECT r.code FROM users u JOIN user_roles ur ON ur.user_id=u.id
      JOIN roles r ON r.id=ur.role_id WHERE u.id=$1 AND u.active=true`,[userId]);
    const allowed=configure ? ['system_admin','production_manager'] : ['system_admin','production_manager','production_operator'];
    if (!result.rows.some(r=>allowed.includes(r.code))) throw new ForbiddenException('ليست لديك صلاحية الإنتاج.');
  }
  async catalog() { return (await this.pool.query('SELECT code,name_ar AS "nameAr",sequence FROM production_operation_catalog WHERE active=true ORDER BY sequence')).rows; }
  async rates() { return (await this.pool.query(`SELECT id,code,name_ar AS "nameAr",stage_code AS "stageCode",rate::text,treatment,
    effective_from::text AS "effectiveFrom",effective_to::text AS "effectiveTo" FROM production_labor_rates ORDER BY code,effective_from DESC`)).rows; }
  async list() {
    return (await this.pool.query(`SELECT po.id,po.order_no AS "orderNo",po.product_code AS "productCode",po.product_name AS "productName",
      co.order_no AS "customerOrderNo",p.name_ar AS "customerNameAr",po.status,po.planned_quantity::text AS "plannedQuantity",
      po.execution_started_date::text AS "startedDate",
      COALESCE((SELECT SUM(quantity) FROM production_acceptances a WHERE a.production_order_id=po.id),0)::text AS "acceptedQuantity",
      (SELECT COUNT(*)::int FROM production_operations op WHERE op.production_order_id=po.id AND NOT op.retired_from_route) AS "stageCount"
      FROM production_orders po LEFT JOIN customer_orders co ON co.id=po.customer_order_id LEFT JOIN parties p ON p.id=co.customer_id
      WHERE po.status<>'cancelled' ORDER BY CASE po.status WHEN 'in_progress' THEN 0 WHEN 'on_hold' THEN 1 WHEN 'ready_for_costing' THEN 2 ELSE 3 END,po.created_at DESC`)).rows;
  }
  async detail(id:string,month?:string) {
    if (month && !/^\d{4}-(0[1-9]|1[0-2])$/.test(month)) throw new BadRequestException('الشهر بصيغة YYYY-MM.');
    const order=await this.pool.query(`SELECT po.*,co.order_no AS "customerOrderNo",p.name_ar AS "customerNameAr"
      FROM production_orders po LEFT JOIN customer_orders co ON co.id=po.customer_order_id LEFT JOIN parties p ON p.id=co.customer_id WHERE po.id=$1`,[id]);
    if (!order.rowCount) throw new NotFoundException('أمر الشغل غير موجود.');
    const [stages,materials,labor,costs,events,acceptances,wip,waste,wages,quality,packaging]=await Promise.all([
      this.pool.query(`SELECT id,operation_code AS code,name_ar AS "nameAr",sequence,completed_quantity::text AS "completedQuantity"
        FROM production_operations WHERE production_order_id=$1 AND NOT retired_from_route ORDER BY sequence`,[id]),
      this.pool.query(`SELECT m.id,m.stage_id AS "stageId",m.item_id AS "itemId",i.item_type AS "itemType",m.warehouse_id AS "warehouseId",i.code AS "itemCode",i.name_ar AS "itemNameAr",w.name_ar AS "warehouseNameAr",u.code AS "unitCode",
        m.quantity::text,m.unit_cost::text AS "unitCost",m.amount::text,m.original_issue_id AS "originalIssueId",m.posting_date::text AS date,
        (m.quantity-COALESCE((SELECT SUM(r.quantity) FROM production_material_postings r WHERE r.original_issue_id=m.id),0)
         -COALESCE((SELECT SUM(w.quantity) FROM production_material_waste w WHERE w.original_issue_id=m.id),0))::text AS "returnableQuantity"
        FROM production_material_postings m JOIN items i ON i.id=m.item_id JOIN warehouses w ON w.id=m.warehouse_id JOIN units_of_measure u ON u.id=i.base_unit_id
        WHERE m.production_order_id=$1 ORDER BY m.posting_date,m.id`,[id]),
      this.pool.query(`SELECT worker_name AS "workerName",rate_code AS "rateCode",quantity::text,rate_snapshot::text AS rate,amount::text,treatment,posting_date::text AS date
        FROM production_labor_entries WHERE production_order_id=$1 ORDER BY posting_date,id`,[id]),
      this.pool.query(`SELECT source_type AS type,amount::text,treatment,posting_date::text AS date,description
        FROM production_cost_entries WHERE production_order_id=$1 ORDER BY posting_date,id`,[id]),
      this.pool.query(`SELECT e.event_type AS action,e.work_date::text AS date,e.notes,u.display_name AS actor,e.payload,e.result
        FROM production_execution_events e JOIN users u ON u.id=e.actor_id WHERE e.production_order_id=$1 ORDER BY e.created_at DESC LIMIT 100`,[id]),
      this.pool.query(`SELECT quantity::text,accepted_date::text AS date,status FROM production_acceptances WHERE production_order_id=$1 ORDER BY accepted_date,id`,[id]),
      this.pool.query(`WITH bounds AS (SELECT COALESCE($2::date,date_trunc('month',CURRENT_DATE)::date) AS start)
        SELECT to_char(b.start,'YYYY-MM') AS month,
        COALESCE(SUM(c.amount) FILTER(WHERE c.treatment='direct' AND c.posting_date<b.start),0)::text AS "openingDirectCost",
        COALESCE(SUM(c.amount) FILTER(WHERE c.treatment='direct' AND c.posting_date>=b.start AND c.posting_date<(b.start+interval '1 month')),0)::text AS "currentDirectCost",
        COALESCE(SUM(c.amount) FILTER(WHERE c.treatment='direct' AND c.posting_date<(b.start+interval '1 month')),0)::text AS "closingDirectCost",
        COALESCE(SUM(c.amount) FILTER(WHERE c.treatment='pooled' AND c.posting_date>=b.start AND c.posting_date<(b.start+interval '1 month')),0)::text AS "pooledWages"
        FROM bounds b LEFT JOIN production_cost_entries c ON c.production_order_id=$1 GROUP BY b.start`,[id,month?month+'-01':null]),
      this.pool.query(`SELECT w.quantity::text,w.reason,w.observation_date::text AS date,i.code AS "itemCode"
        FROM production_material_waste w JOIN production_material_postings m ON m.id=w.original_issue_id JOIN items i ON i.id=m.item_id
        WHERE w.production_order_id=$1 ORDER BY w.observation_date,w.id`,[id]),
      this.pool.query(`SELECT a.id,a.worker_name AS "workerName",a.stage_id AS "stageId",s.name_ar AS "stageName",a.basis,a.rate::text,
        a.assigned_quantity::text AS "assignedQuantity",a.accrued_quantity::text AS "accruedQuantity",a.treatment,
        a.period_start::text AS "periodStart",a.period_end::text AS "periodEnd",
        COALESCE((SELECT SUM(amount) FROM production_wage_accruals w WHERE w.agreement_id=a.id),0)::text AS "earnedAmount"
        FROM production_wage_agreements a JOIN production_operations s ON s.id=a.stage_id WHERE a.production_order_id=$1 ORDER BY a.created_at,a.id`,[id]),
      this.pool.query(`SELECT q.quantity::text,q.decision,q.reinspection,q.reason,q.inspection_date::text AS date,u.display_name AS inspector
        FROM production_quality_checks q JOIN production_execution_events e ON e.operation_id=q.operation_id JOIN users u ON u.id=e.actor_id
        WHERE q.production_order_id=$1 ORDER BY q.inspection_date,q.id`,[id]),
      this.pool.query(`SELECT amount::text,posting_date::text AS date,description FROM production_packaging_costs WHERE production_order_id=$1`,[id])
    ]);
    return {order:order.rows[0],stages:stages.rows,materials:materials.rows,labor:labor.rows,costs:costs.rows,events:events.rows,acceptances:acceptances.rows,wip:wip.rows[0],waste:waste.rows,wages:wages.rows,quality:quality.rows,packaging:packaging.rows};
  }

  async addRate(body:LaborRateDto,userId:string) {
    if (!body.code.trim()||!body.nameAr.trim()) throw new BadRequestException('أدخل كود واسم فئة الأجر.');
    body={...body,code:body.code.trim().toUpperCase()};
    const client=await this.pool.connect();
    try {
      await client.query('BEGIN');
      await client.query('SELECT pg_advisory_xact_lock(hashtext($1))',['labor-rate:'+body.code.trim()]);
      const stage=await client.query('SELECT code FROM production_operation_catalog WHERE code=$1 AND active=true',[body.stageCode]);
      if(!stage.rowCount) throw new BadRequestException('اختر مرحلة نشطة.');
      const existing=await client.query('SELECT stage_code FROM production_labor_rates WHERE code=$1 LIMIT 1',[body.code]);
      if(existing.rowCount&&existing.rows[0].stage_code!==body.stageCode) throw new ConflictException('كود فئة الأجر يخص مرحلة أخرى.');
      const overlap=await client.query(`SELECT id FROM production_labor_rates WHERE code=$1
        AND effective_from<=COALESCE($3::date,'infinity'::date) AND COALESCE(effective_to,'infinity'::date)>=$2::date`,[body.code.trim(),body.effectiveFrom,body.effectiveTo??null]);
      let superseded:string|null=null;
      if(overlap.rowCount) {
        if(overlap.rowCount!==1) throw new ConflictException('فترة الأجر تتداخل مع أسعار مسجلة.');
        const previous=await client.query(`UPDATE production_labor_rates SET effective_to=$2::date-1
          WHERE id=$1 AND effective_to IS NULL AND effective_from<$2::date AND $2::date>=CURRENT_DATE RETURNING id`,[overlap.rows[0].id,body.effectiveFrom]);
        if(!previous.rowCount) throw new ConflictException('فترة الأجر تتداخل مع سعر مسجل؛ لا يمكن تغيير فترة سابقة.');
        superseded=previous.rows[0].id;
      }
      const row=await client.query(`INSERT INTO production_labor_rates(code,name_ar,stage_code,rate,treatment,effective_from,effective_to,approved_by)
        VALUES($1,$2,$3,$4::numeric,$5,$6::date,$7::date,$8) RETURNING id`,[body.code.trim(),body.nameAr.trim(),body.stageCode,String(body.rate),body.treatment,body.effectiveFrom,body.effectiveTo??null,userId]);
      await client.query('INSERT INTO production_labor_rate_audit(rate_id,actor_id,details) VALUES($1,$2,$3::jsonb)',[row.rows[0].id,userId,JSON.stringify({configuration:body,supersededRateId:superseded})]);
      await client.query('COMMIT'); return row.rows[0];
    } catch(error) { await client.query('ROLLBACK'); this.translate(error); } finally {client.release();}
  }

  async execute(id:string,body:ExecutionCommandDto,userId:string) {
    const hash=createHash('sha256').update(canonical({id,...body})).digest('hex');
    const client=await this.pool.connect();
    try {
      await client.query('BEGIN');
      await client.query('SELECT pg_advisory_xact_lock(hashtext($1))',[body.operationId]);
      const prior=await client.query('SELECT payload_hash,result FROM production_execution_events WHERE operation_id=$1',[body.operationId]);
      if(prior.rowCount) {
        if(prior.rows[0].payload_hash!==hash) throw new ConflictException('نفس معرف العملية استُخدم لبيانات أخرى.');
        await client.query('COMMIT'); return {...prior.rows[0].result,alreadyPosted:true};
      }
      const orderResult=await client.query('SELECT * FROM production_orders WHERE id=$1 FOR UPDATE',[id]);
      const order=orderResult.rows[0];
      if(!order) throw new NotFoundException('أمر الشغل غير موجود.');
      const date=await client.query(`SELECT $1::date<=CURRENT_DATE AS valid,
        ($2::date IS NULL OR $1::date>=$2::date) AS after_start`,[body.workDate,order.execution_started_date]);
      if(!date.rows[0].valid||!date.rows[0].after_start) throw new BadRequestException('تاريخ الحركة لا يسبق بدء الإنتاج ولا يكون في المستقبل.');
      await client.query(`INSERT INTO accounting_periods(month) VALUES(date_trunc('month',$1::date)::date) ON CONFLICT DO NOTHING`,[body.workDate]);
      const period=await client.query(`SELECT status FROM accounting_periods WHERE month=date_trunc('month',$1::date)::date FOR SHARE`,[body.workDate]);
      if(period.rows[0].status!=='open') throw new ConflictException('الفترة مغلقة؛ لا يمكن الترحيل إليها.');
      if(['progress','accept','quality_check'].includes(body.action)) {
        const chronology=await client.query(`SELECT COALESCE(MAX(work_date)<=$2::date,true) AS valid
          FROM production_execution_events WHERE production_order_id=$1 AND event_type IN ('start','progress','accept','quality_check')`,[id,body.workDate]);
        if(!chronology.rows[0].valid) throw new ConflictException('إنجاز المرحلة والقبول لا يسبقان آخر حركة تشغيل مسجلة.');
      }
      let result:any={operationId:body.operationId,action:body.action};
      if(body.action==='start') {
        if(!['draft','open'].includes(order.status)) throw new ConflictException('بدأ الإنتاج بالفعل أو الأمر غير قابل للبدء.');
        if(order.customer_order_id) {
          const source=await client.query('SELECT status FROM customer_orders WHERE id=$1 FOR SHARE',[order.customer_order_id]);
          if(source.rows[0]?.status!=='open') throw new ConflictException('طلب العميل ملغي أو غير متاح.');
        }
        const route=[...new Set([...(body.stages??[]),'quality_control'])];
        if(!body.stages?.length) throw new BadRequestException('اختر مراحل الإنتاج المناسبة للمنتج.');
        const catalog=await client.query('SELECT * FROM production_operation_catalog WHERE code=ANY($1::text[]) AND active=true ORDER BY sequence',[route]);
        if(catalog.rowCount!==route.length) throw new BadRequestException('إحدى المراحل غير متاحة.');
        for(const stage of catalog.rows) await client.query(`INSERT INTO production_operations(production_order_id,operation_code,name_ar,sequence) VALUES($1,$2,$3,$4)`,[id,stage.code,stage.name_ar,stage.sequence]);
        await client.query(`UPDATE production_orders SET status='in_progress',execution_started_date=$2::date WHERE id=$1`,[id,body.workDate]);
      } else if(body.action==='hold'||body.action==='resume') {
        if(!body.notes?.trim()) throw new BadRequestException('سجل سبب الإيقاف / الاستئناف.');
        if((body.action==='hold'&&order.status!=='in_progress')||(body.action==='resume'&&order.status!=='on_hold')) throw new ConflictException('حالة الأمر لا تسمح بهذه الحركة.');
        await client.query('UPDATE production_orders SET status=$2 WHERE id=$1',[id,body.action==='hold'?'on_hold':'in_progress']);
      } else {
        const allowed=['return','waste','labor','consumable','packaging_cost','wage_agreement','wage_due'].includes(body.action)
          ? ['in_progress','on_hold','ready_for_costing'] : ['in_progress'];
        if(body.action==='quality_check') allowed.push('on_hold');
        if(!allowed.includes(order.status)) throw new ConflictException('ابدأ أو استأنف الإنتاج قبل تسجيل هذه الحركة.');
        if(body.action==='progress') result={...result,...await this.progress(client,id,body)};
        else if(body.action==='issue'||body.action==='return') result={...result,...await this.material(client,id,body,userId)};
        else if(body.action==='labor') result={...result,...await this.labor(client,id,body)};
        else if(body.action==='waste') result={...result,...await this.waste(client,id,body)};
        else if(body.action==='consumable') result={...result,...await this.consumable(client,id,body)};
        else if(body.action==='accept') result={...result,...await this.accept(client,id,body)};
        else if(body.action==='packaging_cost') result={...result,...await this.packaging(client,id,body)};
        else if(body.action==='wage_agreement') result={...result,...await this.wageAgreement(client,id,body,userId)};
        else if(body.action==='wage_due') result={...result,...await this.wageDue(client,id,body)};
        else if(body.action==='quality_check') result={...result,...await this.qualityCheck(client,id,body)};
        else throw new BadRequestException('حركة غير معروفة.');
      }
      await client.query(`INSERT INTO production_execution_events(operation_id,production_order_id,event_type,payload,payload_hash,result,actor_id,work_date,notes)
        VALUES($1,$2,$3,$4::jsonb,$5,$6::jsonb,$7,$8::date,$9)`,[body.operationId,id,body.action,JSON.stringify(body),hash,JSON.stringify(result),userId,body.workDate,body.notes?.trim()||null]);
      await client.query('COMMIT'); return {...result,alreadyPosted:false};
    } catch(error) { await client.query('ROLLBACK').catch(()=>undefined); this.translate(error); } finally {client.release();}
  }

  private requireQuantity(body:ExecutionCommandDto) {
    if(!Number.isFinite(body.quantity)||body.quantity!<=0) throw new BadRequestException('أدخل كمية أكبر من صفر.');
    return String(body.quantity);
  }
  private async stage(client:PoolClient,id:string,stageId?:string) {
    const row=await client.query('SELECT * FROM production_operations WHERE id=$1 AND production_order_id=$2 AND NOT retired_from_route',[stageId??null,id]);
    if(!row.rowCount) throw new BadRequestException('اختر مرحلة من مراحل أمر الشغل المحدد.');
    return row.rows[0];
  }
  private async progress(client:PoolClient,id:string,body:ExecutionCommandDto) {
    const quantity=this.requireQuantity(body),stage=await this.stage(client,id,body.stageId);
    if(stage.operation_code==='quality_control') throw new BadRequestException('سجل الفحص من قسم الفحص والقبول: مقبول أو مرفوض.');
    const updated=await client.query(`UPDATE production_operations op SET completed_quantity=op.completed_quantity+$3::numeric
      FROM production_orders po WHERE op.id=$2 AND po.id=$1
      AND op.completed_quantity+$3::numeric <= COALESCE((SELECT prev.completed_quantity FROM production_operations prev
        WHERE prev.production_order_id=$1 AND NOT prev.retired_from_route AND prev.sequence<op.sequence ORDER BY prev.sequence DESC LIMIT 1),po.planned_quantity)
      RETURNING op.completed_quantity::text AS "completedQuantity"`,[id,stage.id,quantity]);
    if(!updated.rowCount) throw new ConflictException('الكمية تتجاوز الخطة أو الكمية المنجزة في المرحلة السابقة.');
    return {...updated.rows[0],...await this.accrueUnitProgress(client,id,stage.id,body)};
  }

  private async material(client:PoolClient,id:string,body:ExecutionCommandDto,userId:string) {
    const quantity=this.requireQuantity(body),isReturn=body.action==='return';
    let original:any=null,itemId=body.itemId,warehouseId=body.warehouseId,stageId=body.stageId??null;
    if(isReturn) {
      if(!body.notes?.trim()) throw new BadRequestException('سجل سبب رد الخامة.');
      const issue=await client.query(`SELECT m.*,
        COALESCE((SELECT SUM(quantity) FROM production_material_postings r WHERE r.original_issue_id=m.id),0) AS returned,
        COALESCE((SELECT SUM(amount) FROM production_material_postings r WHERE r.original_issue_id=m.id),0) AS returned_amount,
        COALESCE((SELECT SUM(quantity) FROM production_material_waste w WHERE w.original_issue_id=m.id),0) AS wasted
        FROM production_material_postings m WHERE m.id=$1 AND m.production_order_id=$2 AND m.original_issue_id IS NULL AND m.posting_date<=$3::date FOR UPDATE`,[body.originalIssueId??null,id,body.workDate]);
      original=issue.rows[0]; if(!original) throw new BadRequestException('اختر حركة صرف أصلية من نفس الأمر.');
      itemId=original.item_id;warehouseId=original.warehouse_id;stageId=original.stage_id;
    } else if(stageId) await this.stage(client,id,stageId);
    const item=await client.query(`SELECT id FROM items WHERE id=$1 AND active=true AND stock_tracked=true AND classification='materials' AND item_type<>'finished_good' FOR SHARE`,[itemId??null]);
    const warehouse=await client.query(`SELECT id FROM warehouses WHERE id=$1 AND active=true AND warehouse_type='raw_material' FOR SHARE`,[warehouseId??null]);
    if(!item.rowCount||!warehouse.rowCount) throw new BadRequestException('اختر خامة مخزنية ومخزن خامات نشطين.');
    const costs=await client.query('SELECT * FROM inventory_item_costs WHERE item_id=$1 FOR UPDATE',[itemId]);
    if(!costs.rowCount) throw new ConflictException('لا يوجد رصيد تكلفة للصنف.');
    await client.query(`INSERT INTO inventory_balances(item_id,warehouse_id) VALUES($1,$2) ON CONFLICT DO NOTHING`,[itemId,warehouseId]);
    const balance=await client.query('SELECT quantity_on_hand FROM inventory_balances WHERE item_id=$1 AND warehouse_id=$2 FOR UPDATE',[itemId,warehouseId]);
    const cost=costs.rows[0];
    const math=await client.query(`SELECT
      CASE WHEN $1::boolean THEN $6::numeric-$7::numeric-$10::numeric>=$2::numeric
        ELSE $3::numeric>=$2::numeric AND $4::numeric>=$2::numeric END AS enough,
      CASE WHEN $1::boolean THEN ROUND(($7::numeric+$2::numeric)*$8::numeric/NULLIF($6::numeric,0),4)-$9::numeric
        ELSE CASE WHEN $3::numeric=$2::numeric THEN $5::numeric ELSE ROUND($2::numeric*$5::numeric/NULLIF($3::numeric,0),4) END END::text AS amount`,
      [isReturn,quantity,cost.quantity_on_hand,balance.rows[0].quantity_on_hand,cost.inventory_value,
        original?.quantity??0,original?.returned??0,original?.amount??0,original?.returned_amount??0,original?.wasted??0]);
    if(!math.rows[0].enough) throw new ConflictException(isReturn?'الرد يتجاوز الكمية المصروفة المتبقية.':'رصيد المخزن غير كافٍ للصرف.');
    const amount=math.rows[0].amount,sign=isReturn?'1':'-1';
    const updated=await client.query(`UPDATE inventory_item_costs SET quantity_on_hand=quantity_on_hand+$2::numeric*$4::numeric,
      inventory_value=inventory_value+$3::numeric*$4::numeric,
      average_unit_cost=CASE WHEN quantity_on_hand+$2::numeric*$4::numeric=0 THEN 0
        ELSE ROUND((inventory_value+$3::numeric*$4::numeric)/(quantity_on_hand+$2::numeric*$4::numeric),4) END,updated_at=now()
      WHERE item_id=$1 RETURNING *`,[itemId,quantity,amount,sign]);
    await client.query(`UPDATE inventory_balances SET quantity_on_hand=quantity_on_hand+$3::numeric*$4::numeric,updated_at=now() WHERE item_id=$1 AND warehouse_id=$2`,[itemId,warehouseId,quantity,sign]);
    const after=updated.rows[0],unitCost=isReturn?original.unit_cost:cost.average_unit_cost;
    const movement=await client.query(`INSERT INTO inventory_movements(operation_id,movement_type,item_id,warehouse_id,document_reference,movement_date,
      quantity,unit_cost,total_cost,average_cost_after,quantity_after,value_after,notes,posted_by)
      VALUES($1,$2,$3,$4,$5,$6::date,$7::numeric,$8::numeric,$9::numeric,$10,$11,$12,$13,$14) RETURNING id`,
      [body.operationId,isReturn?'production_return':'production_issue',itemId,warehouseId,'PROD-'+body.operationId,body.workDate,quantity,unitCost,amount,after.average_unit_cost,after.quantity_on_hand,after.inventory_value,body.notes??null,userId]);
    const posting=await client.query(`INSERT INTO production_material_postings(production_order_id,stage_id,inventory_movement_id,original_issue_id,item_id,warehouse_id,quantity,unit_cost,amount,posting_date)
      VALUES($1,$2,$3,$4,$5,$6,$7::numeric,$8::numeric,$9::numeric,$10::date) RETURNING id`,[id,stageId,movement.rows[0].id,original?.id??null,itemId,warehouseId,quantity,unitCost,amount,body.workDate]);
    await client.query(`INSERT INTO production_cost_entries(production_order_id,source_type,source_id,item_id,amount,treatment,posting_date,description)
      VALUES($1,$2,$3,$4,$5::numeric*$6::numeric,'direct',$7::date,$8)`,[id,isReturn?'material_return':'material_issue',posting.rows[0].id,itemId,amount,isReturn?'-1':'1',body.workDate,body.notes??null]);
    return {materialPostingId:posting.rows[0].id,amount};
  }

  private async waste(client:PoolClient,id:string,body:ExecutionCommandDto) {
    const quantity=this.requireQuantity(body);
    if(!body.notes?.trim()) throw new BadRequestException('سجل سبب الهالك الفعلي.');
    const original=await client.query(`SELECT m.id FROM production_material_postings m WHERE m.id=$2 AND m.production_order_id=$1 AND m.original_issue_id IS NULL
      AND m.posting_date<=$4::date AND m.quantity
        -COALESCE((SELECT SUM(r.quantity) FROM production_material_postings r WHERE r.original_issue_id=m.id),0)
        -COALESCE((SELECT SUM(w.quantity) FROM production_material_waste w WHERE w.original_issue_id=m.id),0)>=$3::numeric`,[id,body.originalIssueId??null,quantity,body.workDate]);
    if(!original.rowCount) throw new ConflictException('الهالك يتجاوز الكمية المصروفة غير المردودة أو سبق تسجيلها كهالك.');
    const row=await client.query(`INSERT INTO production_material_waste(production_order_id,original_issue_id,quantity,reason,observation_date)
      VALUES($1,$2,$3::numeric,$4,$5::date) RETURNING id`,[id,body.originalIssueId,quantity,body.notes.trim(),body.workDate]);
    return {wasteObservationId:row.rows[0].id};
  }
  private async labor(client:PoolClient,id:string,body:ExecutionCommandDto) {
    const quantity=this.requireQuantity(body),stage=await this.stage(client,id,body.stageId);
    const agreements=await client.query(`SELECT id FROM production_wage_agreements WHERE production_order_id=$1 AND stage_id=$2 AND basis='unit'`,[id,stage.id]);
    if(agreements.rowCount)throw new ConflictException('تستخدم هذه المرحلة اتفاقات أجور الوحدة؛ سجل الإنجاز أو الاستحقاق من الاتفاق حتى لا يتكرر الأجر بالنظام القديم.');
    if(!body.workerName?.trim()) throw new BadRequestException('أدخل اسم العامل / الفريق.');
    const rate=await client.query(`SELECT * FROM production_labor_rates WHERE id=$1 AND stage_code=$2
      AND effective_from<=$3::date AND (effective_to IS NULL OR effective_to>=$3::date) FOR SHARE`,[body.rateId??null,stage.operation_code,body.workDate]);
    if(!rate.rowCount) throw new BadRequestException('اختر أجراً سارياً لهذه المرحلة في تاريخ الحركة.');
    const r=rate.rows[0];
    const chronology=await client.query(`SELECT COALESCE(MAX(work_date)<=$3::date,true) AS valid FROM production_execution_events
      WHERE production_order_id=$1 AND event_type='progress' AND payload->>'stageId'=$2`,[id,stage.id,body.workDate]);
    if(!chronology.rows[0].valid) throw new ConflictException('تاريخ الأجر لا يسبق الإنجاز المسجل للمرحلة.');
    const eligible=await client.query(`SELECT COALESCE(SUM(quantity),0)+$3::numeric <= $4::numeric AS valid
      FROM production_labor_entries WHERE production_order_id=$1 AND rate_code=$2`,[id,r.code,quantity,stage.completed_quantity]);
    if(!eligible.rows[0].valid) throw new ConflictException('كمية الأجر تتجاوز المنجز أو سبق تسجيل أجرها لنفس الفئة.');
    const entry=await client.query(`INSERT INTO production_labor_entries(production_order_id,stage_id,rate_id,rate_code,worker_name,quantity,rate_snapshot,treatment,amount,posting_date)
      VALUES($1,$2,$3,$4,$5,$6::numeric,$7::numeric,$8,ROUND($6::numeric*$7::numeric,4),$9::date) RETURNING id,amount::text`,[id,stage.id,r.id,r.code,body.workerName.trim(),quantity,r.rate,r.treatment,body.workDate]);
    await client.query(`INSERT INTO production_cost_entries(production_order_id,source_type,source_id,amount,treatment,posting_date,description)
      VALUES($1,'labor',$2,$3::numeric,$4,$5::date,$6)`,[id,entry.rows[0].id,entry.rows[0].amount,r.treatment,body.workDate,body.workerName.trim()]);
    return {laborEntryId:entry.rows[0].id,amount:entry.rows[0].amount};
  }
  private async consumable(client:PoolClient,id:string,body:ExecutionCommandDto) {
    if(!Number.isFinite(body.amount)||body.amount!<=0||!body.documentReference?.trim()) throw new BadRequestException('أدخل التكلفة الفعلية ومرجع المستند.');
    const item=await client.query(`SELECT id FROM items WHERE id=$1 AND active=true AND stock_tracked=false
      AND item_type='consumable' AND classification='materials' FOR SHARE`,[body.itemId??null]);
    if(!item.rowCount) throw new BadRequestException('التكلفة المباشرة مخصصة لمستهلك غير متابع بالمخزون؛ الأصناف المخزنية تسجل بالصرف.');
    const entry=await client.query(`INSERT INTO production_consumable_costs(production_order_id,item_id,document_reference,amount,posting_date)
      VALUES($1,$2,$3,$4::numeric,$5::date) RETURNING id`,[id,body.itemId,body.documentReference.trim(),String(body.amount),body.workDate]);
    await client.query(`INSERT INTO production_cost_entries(production_order_id,source_type,source_id,item_id,amount,treatment,posting_date,description)
      VALUES($1,'consumable',$2,$3,$4::numeric,'direct',$5::date,$6)`,[id,entry.rows[0].id,body.itemId,String(body.amount),body.workDate,body.documentReference.trim()]);
    return {costId:entry.rows[0].id};
  }
  private async accept(client:PoolClient,id:string,body:ExecutionCommandDto) {
    const quantity=this.requireQuantity(body);
    const valid=await client.query(`SELECT COALESCE(SUM(a.quantity),0)+$2::numeric <=
      (SELECT completed_quantity FROM production_operations WHERE production_order_id=$1 AND operation_code='quality_control') AS valid
      FROM production_acceptances a WHERE a.production_order_id=$1`,[id,quantity]);
    if(!valid.rows[0].valid) throw new ConflictException('القبول يتجاوز كمية الفحص المنجزة أو الكمية المقبولة المتبقية.');
    const row=await client.query(`INSERT INTO production_acceptances(production_order_id,operation_id,quantity,accepted_date)
      VALUES($1,$2,$3::numeric,$4::date) RETURNING id`,[id,body.operationId,quantity,body.workDate]);
    await client.query(`UPDATE production_orders SET status='ready_for_costing' WHERE id=$1
      AND planned_quantity=(SELECT SUM(quantity) FROM production_acceptances WHERE production_order_id=$1)`,[id]);
    return {acceptanceId:row.rows[0].id,status:'pending_costing'};
  }
  private async packaging(client:PoolClient,id:string,body:ExecutionCommandDto) {
    if(!Number.isFinite(body.amount)||body.amount!<=0) throw new BadRequestException('أدخل إجمالي تكلفة التعبئة والتغليف لهذا الطلب.');
    const entry=await client.query(`INSERT INTO production_packaging_costs(production_order_id,amount,posting_date,description)
      VALUES($1,$2::numeric,$3::date,$4) RETURNING id,amount::text`,[id,String(body.amount),body.workDate,body.notes?.trim()||'تكلفة التعبئة والتغليف للطلب']);
    await client.query(`INSERT INTO production_cost_entries(production_order_id,source_type,source_id,amount,treatment,posting_date,description)
      VALUES($1,'packaging',$2,$3::numeric,'direct',$4::date,$5)`,[id,entry.rows[0].id,entry.rows[0].amount,body.workDate,body.notes?.trim()||'تعبئة وتغليف']);
    return {packagingCostId:entry.rows[0].id,amount:entry.rows[0].amount};
  }
  private async wageAgreement(client:PoolClient,id:string,body:ExecutionCommandDto,userId:string) {
    const stage=await this.stage(client,id,body.stageId);
    if(!body.workerName?.trim()||!body.wageBasis||!['unit','daily','weekly','monthly'].includes(body.wageBasis)||
      !Number.isFinite(body.rate)||body.rate!<=0||!body.periodStart||!['direct','pooled'].includes(body.treatment??''))
      throw new BadRequestException('أدخل اسم العامل والمرحلة ونوع الأجر والسعر وبداية الفترة والمعاملة.');
    const period=await client.query(`SELECT CASE $2 WHEN 'unit' THEN NULL::date WHEN 'daily' THEN $1::date
      WHEN 'weekly' THEN $1::date+6 WHEN 'monthly' THEN ($1::date+interval '1 month'-interval '1 day')::date END::text AS "periodEnd"`,[body.periodStart,body.wageBasis]);
    const end=period.rows[0].periodEnd;
    await client.query('SELECT pg_advisory_xact_lock(hashtext($1))',['wage-worker:'+body.workerName.trim().toLowerCase()]);
    if(body.wageBasis!=='unit') {
      const paidPeriod=await client.query(`SELECT id FROM production_wage_agreements WHERE basis<>'unit'
        AND lower(btrim(worker_name))=lower(btrim($1)) AND period_start<=$3::date AND period_end>=$2::date`,[body.workerName,body.periodStart,end]);
      if(paidPeriod.rowCount) throw new ConflictException('يوجد اتفاق أجر زمني لنفس العامل في هذه الفترة؛ لا تسجل نفس أجره على أكثر من طلب.');
    }
    const overlap=await client.query(`SELECT id FROM production_wage_agreements WHERE production_order_id=$1 AND stage_id=$2
      AND lower(btrim(worker_name))=lower(btrim($3)) AND
      ((basis='unit' AND accrued_quantity<assigned_quantity) OR (basis<>'unit' AND period_start<=COALESCE($5::date,'infinity'::date) AND period_end>=$4::date))`,[id,stage.id,body.workerName,body.periodStart,end]);
    if(overlap.rowCount) throw new ConflictException('للعامل اتفاق أجر متداخل في نفس المرحلة. راجع الاتفاق المسجل أولاً.');
    let quantity:string|null=null;
    if(body.wageBasis==='unit') {
      quantity=this.requireQuantity(body);
      const available=body.workAlreadyCompleted
        ? await client.query(`SELECT $3::numeric <= $4::numeric
          -COALESCE((SELECT SUM(accrued_quantity) FROM production_wage_agreements WHERE production_order_id=$1 AND stage_id=$2 AND basis='unit'),0)
          -COALESCE((SELECT SUM(quantity) FROM production_labor_entries WHERE production_order_id=$1 AND stage_id=$2),0) AS valid`,[id,stage.id,quantity,stage.completed_quantity])
        : await client.query(`SELECT $3::numeric+COALESCE(SUM(assigned_quantity-accrued_quantity),0)
          <=(SELECT planned_quantity FROM production_orders WHERE id=$1)-$4::numeric AS valid
          FROM production_wage_agreements WHERE production_order_id=$1 AND stage_id=$2 AND basis='unit'`,[id,stage.id,quantity,stage.completed_quantity]);
      if(!available.rows[0].valid) throw new ConflictException('الوحدات المكلف بها العامل تتجاوز المتبقي غير المكلف به في هذه المرحلة.');
    }
    const agreement=await client.query(`INSERT INTO production_wage_agreements(production_order_id,stage_id,worker_name,basis,rate,assigned_quantity,
      period_start,period_end,treatment,approved_by) VALUES($1,$2,$3,$4,$5::numeric,$6::numeric,$7::date,$8::date,$9,$10)
      RETURNING id,basis,rate::text,treatment,period_end::text AS "periodEnd",worker_name AS "workerName"`,
      [id,stage.id,body.workerName.trim(),body.wageBasis,String(body.rate),quantity,body.periodStart,end,body.treatment,userId]);
    let earning:any={};
    if(body.wageBasis!=='unit'&&end<=body.workDate) earning=await this.accrueWage(client,id,agreement.rows[0],body,'1');
    if(body.wageBasis==='unit'&&body.workAlreadyCompleted) {
      if(body.periodStart>body.workDate) throw new BadRequestException('لا يمكن استحقاق عمل منجز لاتفاق يبدأ في المستقبل.');
      earning=await this.accrueWage(client,id,agreement.rows[0],body,quantity!);
    }
    return {agreementId:agreement.rows[0].id,periodEnd:end,...earning};
  }
  private async accrueWage(client:PoolClient,id:string,agreement:any,body:ExecutionCommandDto,quantity:string) {
    const row=await client.query(`INSERT INTO production_wage_accruals(agreement_id,operation_id,quantity,amount,posting_date)
      VALUES($1,$2,$3::numeric,ROUND($3::numeric*$4::numeric,4),$5::date) RETURNING id,amount::text`,[agreement.id,body.operationId,quantity,agreement.rate,body.workDate]);
    await client.query(`INSERT INTO production_cost_entries(production_order_id,source_type,source_id,amount,treatment,posting_date,description)
      VALUES($1,'wage_accrual',$2,$3::numeric,$4,$5::date,$6)`,[id,row.rows[0].id,row.rows[0].amount,agreement.treatment,body.workDate,agreement.workerName??agreement.worker_name]);
    await client.query('UPDATE production_wage_agreements SET accrued_quantity=accrued_quantity+$2::numeric WHERE id=$1',[agreement.id,quantity]);
    return {wageAccrualId:row.rows[0].id,earnedAmount:row.rows[0].amount};
  }
  private async wageDue(client:PoolClient,id:string,body:ExecutionCommandDto) {
    const row=await client.query(`SELECT *,worker_name AS "workerName" FROM production_wage_agreements
      WHERE id=$1 AND production_order_id=$2 AND basis<>'unit' AND period_end<=$3::date AND accrued_quantity=0 FOR UPDATE`,[body.wageAgreementId??null,id,body.workDate]);
    if(!row.rowCount) throw new ConflictException('الفترة لم تنتهِ بعد أو سبق تسجيل استحقاقها.');
    return this.accrueWage(client,id,row.rows[0],body,'1');
  }
  private async accrueUnitProgress(client:PoolClient,id:string,stageId:string,body:ExecutionCommandDto) {
    const rows=await client.query(`SELECT *,worker_name AS "workerName" FROM production_wage_agreements
      WHERE production_order_id=$1 AND stage_id=$2 AND basis='unit' AND period_start<=$3::date AND accrued_quantity<assigned_quantity
      ORDER BY created_at,id FOR UPDATE`,[id,stageId,body.workDate]);
    if(!rows.rowCount) {
      if(body.wageAgreementId) throw new BadRequestException('اتفاق العامل غير سارٍ أو انتهت وحداته المكلف بها.');
      return {};
    }
    let agreement=body.wageAgreementId?rows.rows.find(x=>x.id===body.wageAgreementId):rows.rowCount===1?rows.rows[0]:null;
    if(!agreement) throw new BadRequestException('اختر العامل الذي أنهى هذه الوحدات لحساب أجره.');
    const valid=await client.query('SELECT assigned_quantity-accrued_quantity >= $2::numeric AS valid FROM production_wage_agreements WHERE id=$1',[agreement.id,String(body.quantity)]);
    if(!valid.rows[0].valid) throw new ConflictException('عدد الوحدات المنجزة يتجاوز الوحدات المتبقية لهذا العامل.');
    return this.accrueWage(client,id,agreement,body,String(body.quantity));
  }
  private async qualityCheck(client:PoolClient,id:string,body:ExecutionCommandDto) {
    const quantity=this.requireQuantity(body);
    if(!['accepted','rejected'].includes(body.decision??'')) throw new BadRequestException('اختر مقبول أو مرفوض.');
    if(body.decision==='rejected'&&!body.notes?.trim()) throw new BadRequestException('اكتب سبب الرفض والإصلاح المطلوب.');
    const available=await client.query(`WITH inspected AS (
      SELECT COALESCE(SUM(quantity) FILTER(WHERE decision='rejected'),0)-COALESCE(SUM(quantity) FILTER(WHERE reinspection),0) AS rejected
      FROM production_quality_checks WHERE production_order_id=$1)
      SELECT CASE WHEN $3::boolean THEN rejected >= $2::numeric ELSE
        COALESCE((SELECT completed_quantity FROM production_operations WHERE production_order_id=$1 AND NOT retired_from_route AND operation_code<>'quality_control' ORDER BY sequence DESC LIMIT 1),
          (SELECT planned_quantity FROM production_orders WHERE id=$1))
        -COALESCE((SELECT SUM(quantity) FROM production_acceptances WHERE production_order_id=$1),0)-rejected >= $2::numeric END AS valid FROM inspected`,[id,quantity,body.reinspection??false]);
    if(!available.rows[0].valid) throw new ConflictException(body.reinspection?'الكمية تتجاوز المرفوض المنتظر إعادة الفحص.':'الكمية تتجاوز الجاهز للفحص الذي لم يُقبل أو يُرفض بعد.');
    const check=await client.query(`INSERT INTO production_quality_checks(production_order_id,operation_id,quantity,decision,reinspection,reason,inspection_date)
      VALUES($1,$2,$3::numeric,$4,$5,$6,$7::date) RETURNING id`,[id,body.operationId,quantity,body.decision,body.reinspection??false,body.notes?.trim()||null,body.workDate]);
    let acceptance:any={};
    if(body.decision==='accepted') {
      const qc=await client.query(`UPDATE production_operations SET completed_quantity=GREATEST(completed_quantity,
        COALESCE((SELECT SUM(quantity) FROM production_acceptances WHERE production_order_id=$1),0)+$2::numeric)
        WHERE production_order_id=$1 AND operation_code='quality_control' AND NOT retired_from_route RETURNING id`,[id,quantity]);
      if(!qc.rowCount) throw new ConflictException('ابدأ مسار الإنتاج والفحص أولاً.');
      acceptance=await this.accept(client,id,body);
    }
    return {qualityCheckId:check.rows[0].id,decision:body.decision,...acceptance};
  }
  private translate(error:unknown):never {
    if(error instanceof BadRequestException||error instanceof ConflictException||error instanceof NotFoundException) throw error;
    const code=(error as {code?:string}).code;
    if(code==='23505') throw new ConflictException('هذا المصدر مسجل بالفعل؛ لا تعِد تسجيل نفس التكلفة.');
    if(['23503','23514','22P02','22007','22008','22003'].includes(code??'')) throw new BadRequestException('تحقق من البيانات والتواريخ والمراجع.');
    throw error;
  }
}
