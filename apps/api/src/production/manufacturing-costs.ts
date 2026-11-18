import {BadRequestException,ConflictException} from '@nestjs/common';
import {PoolClient} from 'pg';
import {ExecutionCommandDto} from './production.dto';

export async function enterWorkCost(c:PoolClient,id:string,b:ExecutionCommandDto,user:string){
 if(!['cutting','ashareet','assembly','general_labor'].includes(b.costStage??'')||!['unit','minute','hour','daily','weekly','monthly'].includes(b.costBasis??'')||
 !Number.isFinite(b.quantity)||b.quantity!<=0||!Number.isFinite(b.rate)||b.rate!<=0||!b.documentReference?.trim())throw new BadRequestException('حدد القسم وأساس الحساب والكمية والسعر ومرجع العمل الفريد.');
 let worker:string|null=null;
 const workerCost=b.costStage==='assembly'||b.costStage==='general_labor';
 if(b.costStage==='general_labor'&&!b.notes?.trim())throw new BadRequestException('اكتب وصف العمل الذي يستحق العامل الأجر مقابله.');
 if(workerCost){
  if(!b.workerId||b.machineName)throw new BadRequestException('اختر العامل المسجل للتجميع وتركيب الإكسسوارات.');
  const row=await c.query('SELECT id,code,name_ar FROM production_workers WHERE id=$1 AND active FOR SHARE',[b.workerId]);
  if(!row.rowCount)throw new BadRequestException('العامل غير موجود أو غير نشط.');
  worker=row.rows[0].code+' — '+row.rows[0].name_ar;
 }else if(!b.machineName?.trim()||b.workerId||!['unit','minute','hour'].includes(b.costBasis!))throw new BadRequestException('حدد الماكينة وحسابها بالوحدة أو الدقيقة أو الساعة.');
 const legacy=await c.query(`SELECT a.id FROM production_wage_agreements a JOIN production_operations s ON s.id=a.stage_id
  WHERE a.production_order_id=$1 AND s.operation_code=$2 UNION ALL
  SELECT l.id FROM production_labor_entries l JOIN production_operations s ON s.id=l.stage_id WHERE l.production_order_id=$1 AND s.operation_code=$2`,[id,b.costStage]);
 if(legacy.rowCount)throw new ConflictException('يوجد تسجيل أجور سابق لهذا القسم؛ راجع المصدر السابق قبل إضافة تكلفة جديدة لتجنب حسابه مرتين.');
 const r=await c.query(`INSERT INTO production_work_costs(operation_id,production_order_id,stage_code,worker_id,worker_snapshot,machine_name,basis,quantity,rate,reference,posting_date,description,actor_id)
 VALUES($1,$2,$3,$4,$5,$6,$7,$8::numeric,$9::numeric,$10,$11::date,$12,$13) RETURNING id,amount::text`,
 [b.operationId,id,b.costStage,b.workerId??null,worker,workerCost?null:b.machineName!.trim(),b.costBasis,String(b.quantity),String(b.rate),b.documentReference.trim(),b.workDate,b.notes?.trim()||null,user]);
 await c.query(`INSERT INTO production_cost_entries(production_order_id,source_type,source_id,amount,treatment,posting_date,description)
 VALUES($1,'manufacturing_work',$2,$3::numeric,'direct',$4::date,$5)`,[id,r.rows[0].id,r.rows[0].amount,b.workDate,(worker??b.machineName)+' | '+b.costStage+' | '+b.documentReference.trim()]);
 return {workCostId:r.rows[0].id,amount:r.rows[0].amount};
}

export const MANUFACTURING_REPORT_SQL=`SELECT po.id AS job_id,po.order_no AS job_code,co.order_no AS order_code,
 po.status,date_trunc('month',c.posting_date)::date::text AS month,
 COALESCE(SUM(c.amount) FILTER(WHERE c.source_type='manufacturing_work'),0)::text AS work_cost,
 COALESCE(SUM(c.amount) FILTER(WHERE c.source_type='packaging'),0)::text AS packaging_cost,
 COALESCE(SUM(c.amount) FILTER(WHERE c.source_type IN ('labor','wage_accrual') AND c.treatment='direct'),0)::text AS legacy_wages,
 COALESCE(SUM(c.amount) FILTER(WHERE c.source_type IN ('manufacturing_work','packaging','labor','wage_accrual') AND c.treatment='direct'),0)::text AS direct_manufacturing,
 COALESCE(SUM(c.amount) FILTER(WHERE c.treatment='pooled'),0)::text AS pooled_wages
 FROM production_orders po JOIN production_cost_entries c ON c.production_order_id=po.id
 LEFT JOIN customer_orders co ON co.id=po.customer_order_id
 GROUP BY po.id,co.order_no,date_trunc('month',c.posting_date) ORDER BY month DESC,po.order_no`;
