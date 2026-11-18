import {
  BadRequestException,
  ConflictException,
  Injectable,
  NotFoundException,
} from '@nestjs/common';
import { createHash, randomUUID } from 'node:crypto';
import { mkdir, stat, unlink, writeFile } from 'node:fs/promises';
import { extname, basename, resolve } from 'node:path';
import { Pool, PoolClient, QueryResultRow } from 'pg';
import { ConfirmOrderDto } from './confirm-order.dto';
import { OrderAvailabilityService } from '../inventory/order-availability.service';

export interface CreateCustomerOrderInput {
  orderNo: string;
  customerId: string;
  orderDate: string;
  requestedDeliveryDate?: string;
  notes?: string;
}

export interface CreateEngineeringPackageInput {
  productCode: string;
  productDescription: string;
  upholsteryRequired: boolean;
  notes?: string;
}

export interface CreateEngineeringLineInput {
  lineType: 'bom' | 'cut_list';
  description: string;
  materialReference?: string;
  quantity: number;
  unitCode: string;
  dimensions?: string;
  notes?: string;
}

export interface CreateProductionOrderInput {
  orderNo: string;
  engineeringPackageId?: string;
  productCode?: string;
  plannedQuantity: number;
  productName?: string;
  productionDate?: string;
  jobName?: string;
  plannedEndDate?: string;
}

export interface ProductionWorkOrderLineInput {
  catalogCode?: string | null;
  itemNameAr: string;
  specification: string;
  unitCode: string;
  quantity?: number | null;
  isSelected: boolean;
  isCustom: boolean;
}

export interface SaveProductionWorkOrderInput {
  productName: string;
  productionDate: string;
  lines: ProductionWorkOrderLineInput[];
}

export type EngineeringAttachmentType = 'product_reference' | 'dimensioned_drawing' | 'cut_list' | 'bom_export' | 'other';

export interface UploadEngineeringFileMetadataInput {
  attachmentType?: EngineeringAttachmentType;
  description?: string;
  materialCode?: string;
  materialColor?: string;
  thicknessMm?: number;
  boardLengthMm?: number;
  boardWidthMm?: number;
  materialItemId?: string;
  boardQuantity?: number;
  wastePercentage?: number;
}

export interface CreateEngineeringProductMaterialLineInput {
  lineType: 'accessory' | 'raw_material' | 'paint';
  itemId: string;
  quantityPerUnit: number;
  notes?: string;
}

export interface CreateEngineeringCutListPartInput {
  sourceFileId: string;
  partName: string;
  lengthMm: number;
  widthMm: number;
  thicknessMm?: number;
  quantity: number;
  notes?: string;
}

export interface UploadedEngineeringFile {
  originalname: string;
  mimetype: string;
  size: number;
  buffer: Buffer;
}

@Injectable()
export class OrdersService {
  constructor(private readonly availability:OrderAvailabilityService=new OrderAvailabilityService()){}
  private readonly pool = new Pool({
    connectionString: process.env.DATABASE_URL ?? 'postgresql://erp_app:change-me-for-local-development@localhost:5432/wood_factory_erp',
  });
  private readonly storageDirectory = resolve(
    process.cwd(),
    process.env.ENGINEERING_STORAGE_DIR ?? 'storage/engineering-packages',
  );

  async listOrders() {
    const result = await this.pool.query(`
      SELECT o.id, o.order_no AS "orderNo", COALESCE(o.order_name,o.product_name,o.order_no) AS "orderName", o.customer_id AS "customerId",
             p.code AS "customerCode", p.name_ar AS "customerNameAr",
             o.order_date::text AS "orderDate",
             o.requested_delivery_date::text AS "requestedDeliveryDate",
             o.status, o.notes, o.created_at AS "createdAt",
             o.product_code AS "productCode", o.product_name AS "productName",
             o.total_boards AS "totalBoards", o.waste_percentage::text AS "wastePercentage",
             o.confirmed_at AS "confirmedAt",
             ep.id AS "currentPackageId", ep.revision AS "currentRevision",
             (SELECT COUNT(*)::int FROM engineering_packages all_ep
              WHERE all_ep.customer_order_id = o.id) AS "revisionCount",
             (SELECT COUNT(*)::int FROM production_orders po
              WHERE po.customer_order_id = o.id) AS "productionOrderCount"
      FROM customer_orders o
      JOIN parties p ON p.id = o.customer_id
      LEFT JOIN engineering_packages ep
        ON ep.customer_order_id = o.id AND ep.status = 'finalized'
      ORDER BY o.order_date DESC, o.created_at DESC`);
    return result.rows;
  }

  async confirmOrder(body: ConfirmOrderDto, files: UploadedEngineeringFile[], userId?: string) {
    if (![body.orderNo, body.productCode, body.productName].every(x => x.trim().length))
      throw new BadRequestException('Order code, product code and product name are required.');
    if (body.attachments.length !== files.length) throw new BadRequestException('Attachment metadata does not match files.');
    if (files.reduce((sum, file) => sum + file.size, 0) > 100 * 1024 * 1024)
      throw new BadRequestException('Attachments exceed 100 MB in total.');
    const allowedExtensions = new Set(['.jpg', '.jpeg', '.png', '.webp', '.gif', '.pdf', '.xlsx', '.xls', '.csv', '.dxf', '.dwg', '.skp']);
    for (let index = 0; index < files.length; index++) {
      const file = files[index];
      if (!body.attachments[index].name.trim() || !file.buffer?.length || file.buffer.length > 20 * 1024 * 1024 ||
          !allowedExtensions.has(extname(file.originalname).toLowerCase()))
        throw new BadRequestException('Use a named, nonempty image, PDF, spreadsheet or design file up to 20 MB.');
    }
    if (new Set(body.accessories.map(x => x.itemId)).size !== body.accessories.length)
      throw new BadRequestException('Each accessory must appear once; enter its total quantity.');
    const materials=body.materials??[];
    if(new Set(materials.map(x=>x.itemId)).size!==materials.length)throw new BadRequestException('Each material must appear once; enter its total quantity.');
    if(materials.length && body.totalBoards!==undefined && body.totalBoards!==0)throw new BadRequestException('Use material quantities instead of an untyped board total.');
    if(body.orderName!==undefined&&!body.orderName.trim())throw new BadRequestException('Order name is required.');
    const client = await this.pool.connect();
    try {
      await client.query('BEGIN');
      // Serialize confirmations before inserting demand. The following READ
      // COMMITTED statements then see the preceding confirmation's committed
      // requirements, including when two users confirm at the same time.
      await client.query('SELECT pg_advisory_xact_lock(78231, 1)');
      const customer = await client.query(`SELECT id FROM parties WHERE id = $1 AND active = true AND party_type IN ('customer', 'both') FOR SHARE`, [body.customerId]);
      if (!customer.rowCount) throw new BadRequestException('Select an active customer.');
      for (const line of body.accessories) {
        const item = await client.query(`SELECT id FROM items WHERE id = $1 AND active = true AND item_type = 'accessory' FOR SHARE`, [line.itemId]);
        if (!item.rowCount) throw new BadRequestException('Select an active accessory from master data.');
      }
      for(const line of materials){
        const item=await client.query(`SELECT i.id,u.precision_digits FROM items i JOIN units_of_measure u ON u.id=i.base_unit_id
          WHERE i.id=$1 AND i.active AND i.classification='materials' AND i.item_type IN ('raw_material','consumable') FOR SHARE OF i,u`,[line.itemId]);
        if(!item.rowCount)throw new BadRequestException('Select an active raw material or consumable from master data.');
        const precision=await client.query('SELECT $1::numeric>0 AND $1::numeric=ROUND($1::numeric,$2::int) AS valid',[String(line.quantity),Math.min(item.rows[0].precision_digits,4)]);
        if(!precision.rows[0].valid)throw new BadRequestException('Material quantity does not match its unit precision.');
      }
      const order = await client.query(`
        INSERT INTO customer_orders (order_no, customer_id, order_date, requested_delivery_date, notes, created_by,
          product_code, product_name, total_boards, waste_percentage, confirmed_at,order_name)
        VALUES ($1,$2,$3::date,$4::date,$5,$6,$7,$8,$9,$10::numeric,now(),$11)
        RETURNING id, order_no AS "orderNo"`,
        [body.orderNo.trim(), body.customerId, body.orderDate, body.requestedDeliveryDate || null,
          cleanOptional(body.notes), userId ?? null, body.productCode.trim(), body.productName.trim(), body.totalBoards??null, String(body.wastePercentage),(body.orderName??body.productName).trim()]);
      const id = order.rows[0].id;
      for(const line of materials)await client.query('INSERT INTO customer_order_materials(order_id,item_id,quantity) VALUES($1,$2,$3::numeric)',[id,line.itemId,String(line.quantity)]);
      for (const line of body.accessories)
        await client.query('INSERT INTO customer_order_accessories (order_id,item_id,quantity) VALUES ($1,$2,$3::numeric)', [id, line.itemId, String(line.quantity)]);
      for (let index = 0; index < files.length; index++) {
        const file = files[index];
        const metadata = body.attachments[index];
        await client.query(`INSERT INTO customer_order_attachments
          (order_id,attachment_type,name,description,color,file_name,mime_type,content)
          VALUES ($1,$2,$3,$4,$5,$6,$7,$8)`,
          [id, metadata.attachmentType, metadata.name.trim(), cleanOptional(metadata.description), cleanOptional(metadata.color),
            basename(file.originalname.replace(/\\/g, '/')).replace(/[\r\n\0]/g, '').slice(0,255),
            file.mimetype || 'application/octet-stream', file.buffer]);
      }
      const availability=await this.availability.forOrder(client,id);
      await client.query('COMMIT');
      return {...order.rows[0],availability};
    } catch (error) {
      await safeRollback(client);
      this.translateDatabaseError(error);
    } finally { client.release(); }
  }

  async getOrderDetails(orderId: string) {
    const order = await this.pool.query(`SELECT o.id, o.order_no AS "orderNo",COALESCE(o.order_name,o.product_name,o.order_no) AS "orderName",o.order_date::text AS "orderDate",o.requested_delivery_date::text AS "requestedDeliveryDate", o.product_code AS "productCode",
      o.product_name AS "productName", o.total_boards AS "totalBoards", o.waste_percentage::text AS "wastePercentage",
      o.notes, o.confirmed_at AS "confirmedAt", p.name_ar AS "customerNameAr"
      FROM customer_orders o JOIN parties p ON p.id = o.customer_id WHERE o.id = $1`, [orderId]);
    if (!order.rowCount) throw new NotFoundException('Order not found.');
    const attachments = await this.pool.query(`SELECT id, name, description, color, attachment_type AS "attachmentType",
      file_name AS "fileName", mime_type AS "mimeType", octet_length(content) AS "sizeBytes"
      FROM customer_order_attachments WHERE order_id = $1 ORDER BY created_at, id`, [orderId]);
    const accessories = await this.pool.query(`SELECT a.item_id AS "itemId", i.code AS "itemCode", i.name_ar AS "itemNameAr",
      a.quantity::text AS quantity, u.code AS "unitCode"
      FROM customer_order_accessories a JOIN items i ON i.id = a.item_id
      JOIN units_of_measure u ON u.id = i.base_unit_id WHERE a.order_id = $1 ORDER BY i.code`, [orderId]);
    const materials=await this.pool.query(`SELECT m.item_id AS "itemId",i.code AS "itemCode",i.name_ar AS "itemNameAr",m.quantity::text AS quantity,u.code AS "unitCode"
      FROM customer_order_materials m JOIN items i ON i.id=m.item_id JOIN units_of_measure u ON u.id=i.base_unit_id WHERE m.order_id=$1 ORDER BY i.code`,[orderId]);
    const jobs=await this.pool.query(`SELECT id,order_no AS "orderNo",job_name AS "jobName",production_date::text AS "startDate",planned_end_date::text AS "endDate",planned_quantity::text AS quantity,status FROM production_orders WHERE customer_order_id=$1 ORDER BY created_at,order_no`,[orderId]);
    return { ...order.rows[0], attachments: attachments.rows, accessories: accessories.rows,materials:materials.rows,jobs:jobs.rows };
  }

  async getOrderAttachment(orderId: string, fileId: string) {
    const result = await this.pool.query('SELECT file_name, mime_type, content FROM customer_order_attachments WHERE order_id = $1 AND id = $2', [orderId, fileId]);
    if (!result.rowCount) throw new NotFoundException('Attachment not found.');
    return result.rows[0];
  }

  async createOrder(body: CreateCustomerOrderInput, userId?: string) {
    try {
      const customer = await this.pool.query(
        `SELECT id FROM parties WHERE id = $1 AND active = true AND party_type IN ('customer', 'both')`,
        [body.customerId],
      );
      if (!customer.rowCount) throw new BadRequestException('اختر عميلاً نشطاً صالحاً.');
      const result = await this.pool.query(`
        INSERT INTO customer_orders
          (order_no, customer_id, order_date, requested_delivery_date, notes, created_by)
        VALUES ($1, $2, $3::date, $4::date, $5, $6)
        RETURNING id, order_no AS "orderNo", customer_id AS "customerId",
                  order_date::text AS "orderDate",
                  requested_delivery_date::text AS "requestedDeliveryDate",
                  status, notes, created_at AS "createdAt"`,
        [clean(body.orderNo), body.customerId, body.orderDate,
          body.requestedDeliveryDate || null, cleanOptional(body.notes), userId ?? null],
      );
      return result.rows[0];
    } catch (error) {
      this.translateDatabaseError(error);
    }
  }

  async listEngineeringPackages(orderId: string) {
    await this.ensureOrderExists(orderId);
    const result = await this.pool.query(`
      SELECT ep.id, ep.customer_order_id AS "customerOrderId", ep.revision,
             ep.product_code AS "productCode", ep.product_description AS "productDescription",
             ep.upholstery_required AS "upholsteryRequired",
             ep.technical_reference AS "technicalReference", ep.notes, ep.status,
             ep.created_at AS "createdAt", ep.finalized_at AS "finalizedAt",
             (SELECT COUNT(*)::int FROM engineering_material_lines ml WHERE ml.package_id = ep.id) AS "lineCount",
             (SELECT COUNT(*)::int FROM engineering_product_material_lines pml WHERE pml.package_id = ep.id) AS "productMaterialLineCount",
             (SELECT COUNT(*)::int FROM engineering_package_files f WHERE f.package_id = ep.id) AS "fileCount"
      FROM engineering_packages ep
      WHERE ep.customer_order_id = $1
      ORDER BY ep.revision DESC`, [orderId]);
    return result.rows;
  }

  async createEngineeringPackage(orderId: string, body: CreateEngineeringPackageInput, userId?: string) {
    const client = await this.pool.connect();
    try {
      await client.query('BEGIN');
      await this.lockOrder(client, orderId);
      const revisionResult = await client.query(
        'SELECT COALESCE(MAX(revision), 0) + 1 AS revision FROM engineering_packages WHERE customer_order_id = $1',
        [orderId],
      );
      const revision = Number(revisionResult.rows[0].revision);
      const result = await client.query(`
        INSERT INTO engineering_packages
          (customer_order_id, revision, product_code, product_description, upholstery_required,
           technical_reference, notes, status, created_by)
        VALUES ($1, $2, $3, $4, $5, $3, $6, 'draft', $7)
        RETURNING id, customer_order_id AS "customerOrderId", revision,
                  product_code AS "productCode", product_description AS "productDescription",
                  upholstery_required AS "upholsteryRequired",
                  technical_reference AS "technicalReference", notes, status,
                  created_at AS "createdAt", finalized_at AS "finalizedAt"`,
        [orderId, revision, clean(body.productCode), clean(body.productDescription), body.upholsteryRequired,
          cleanOptional(body.notes), userId ?? null],
      );
      await client.query('COMMIT');
      return result.rows[0];
    } catch (error) {
      await safeRollback(client);
      this.translateDatabaseError(error);
    } finally {
      client.release();
    }
  }

  async listEngineeringLines(packageId: string) {
    await this.ensurePackageExists(packageId);
    const result = await this.pool.query(`
      SELECT id, package_id AS "packageId", line_no AS "lineNo", line_type AS "lineType",
             description, material_reference AS "materialReference", quantity::text AS quantity,
             unit_code AS "unitCode", dimensions, notes, created_at AS "createdAt"
      FROM engineering_material_lines WHERE package_id = $1 ORDER BY line_no`, [packageId]);
    return result.rows;
  }

  async addEngineeringLine(packageId: string, body: CreateEngineeringLineInput) {
    const client = await this.pool.connect();
    try {
      await client.query('BEGIN');
      await this.lockDraftPackage(client, packageId);
      const lineNoResult = await client.query(
        'SELECT COALESCE(MAX(line_no), 0) + 1 AS line_no FROM engineering_material_lines WHERE package_id = $1',
        [packageId],
      );
      const result = await client.query(`
        INSERT INTO engineering_material_lines
          (package_id, line_no, line_type, description, material_reference, quantity,
           unit_code, dimensions, notes)
        VALUES ($1, $2, $3, $4, $5, $6::numeric, $7, $8, $9)
        RETURNING id, package_id AS "packageId", line_no AS "lineNo", line_type AS "lineType",
                  description, material_reference AS "materialReference", quantity::text AS quantity,
                  unit_code AS "unitCode", dimensions, notes, created_at AS "createdAt"`,
        [packageId, Number(lineNoResult.rows[0].line_no), body.lineType, clean(body.description),
          cleanOptional(body.materialReference), String(body.quantity), clean(body.unitCode),
          cleanOptional(body.dimensions), cleanOptional(body.notes)],
      );
      await client.query('COMMIT');
      return result.rows[0];
    } catch (error) {
      await safeRollback(client);
      this.translateDatabaseError(error);
    } finally {
      client.release();
    }
  }

  async listEngineeringProductMaterialLines(packageId: string) {
    await this.ensurePackageExists(packageId);
    const result = await this.pool.query(`
      SELECT l.id, l.package_id AS "packageId", l.line_no AS "lineNo",
             l.line_type AS "lineType", l.item_id AS "itemId",
             i.code AS "itemCode", i.name_ar AS "itemNameAr",
             u.code AS "unitCode", u.name_ar AS "unitNameAr",
             l.quantity_per_unit::text AS "quantityPerUnit", l.notes,
             l.created_at AS "createdAt"
      FROM engineering_product_material_lines l
      JOIN items i ON i.id = l.item_id
      JOIN units_of_measure u ON u.id = i.base_unit_id
      WHERE l.package_id = $1 ORDER BY l.line_no`, [packageId]);
    return result.rows;
  }

  async addEngineeringProductMaterialLine(packageId: string, body: CreateEngineeringProductMaterialLineInput) {
    if (!Number.isFinite(body.quantityPerUnit) || body.quantityPerUnit <= 0)
      throw new BadRequestException('الكمية لكل وحدة يجب أن تكون أكبر من صفر.');

    const client = await this.pool.connect();
    try {
      await client.query('BEGIN');
      await this.lockDraftPackage(client, packageId);
      const itemResult = await client.query(`
        SELECT id, item_type, classification, stock_tracked, active
        FROM items WHERE id = $1 FOR SHARE`, [body.itemId]);
      const item = itemResult.rows[0];
      if (!item || !item.active) throw new BadRequestException('الصنف المختار غير موجود أو غير نشط.');
      const validItemType = body.lineType === 'accessory'
        ? item.item_type === 'accessory'
        : body.lineType === 'raw_material'
          ? item.item_type === 'raw_material'
          : ['consumable', 'raw_material'].includes(item.item_type);
      if (!validItemType) {
        throw new BadRequestException(body.lineType === 'accessory'
          ? 'اختر صنفاً من نوع إكسسوار من البيانات الأساسية.'
          : body.lineType === 'raw_material'
            ? 'اختر صنفاً من نوع خامة من البيانات الأساسية.'
            : 'اختر دهاناً/مستهلكاً أو خامة من البيانات الأساسية.');
      }
      const lineNoResult = await client.query(
        'SELECT COALESCE(MAX(line_no), 0) + 1 AS line_no FROM engineering_product_material_lines WHERE package_id = $1',
        [packageId],
      );
      const result = await client.query(`
        INSERT INTO engineering_product_material_lines
          (package_id, line_no, line_type, item_id, quantity_per_unit, notes)
        VALUES ($1, $2, $3, $4, $5::numeric, $6)
        RETURNING id, package_id AS "packageId", line_no AS "lineNo",
                  line_type AS "lineType", item_id AS "itemId",
                  quantity_per_unit::text AS "quantityPerUnit", notes, created_at AS "createdAt"`,
        [packageId, Number(lineNoResult.rows[0].line_no), body.lineType, body.itemId,
          String(body.quantityPerUnit), cleanOptional(body.notes)],
      );
      const full = await client.query(`
        SELECT l.id, l.package_id AS "packageId", l.line_no AS "lineNo",
               l.line_type AS "lineType", l.item_id AS "itemId",
               i.code AS "itemCode", i.name_ar AS "itemNameAr",
               u.code AS "unitCode", u.name_ar AS "unitNameAr",
               l.quantity_per_unit::text AS "quantityPerUnit", l.notes,
               l.created_at AS "createdAt"
        FROM engineering_product_material_lines l
        JOIN items i ON i.id = l.item_id
        JOIN units_of_measure u ON u.id = i.base_unit_id
        WHERE l.id = $1`, [result.rows[0].id]);
      await client.query('COMMIT');
      return full.rows[0];
    } catch (error) {
      await safeRollback(client);
      this.translateDatabaseError(error);
    } finally {
      client.release();
    }
  }

  async deleteEngineeringProductMaterialLine(packageId: string, lineId: string) {
    const client = await this.pool.connect();
    try {
      await client.query('BEGIN');
      await this.lockDraftPackage(client, packageId);
      const result = await client.query(
        'DELETE FROM engineering_product_material_lines WHERE id = $1 AND package_id = $2 RETURNING id',
        [lineId, packageId],
      );
      if (!result.rowCount) throw new NotFoundException('بند الخامة/الإكسسوار غير موجود.');
      await client.query('COMMIT');
      return { deleted: true, id: lineId };
    } catch (error) {
      await safeRollback(client);
      this.translateDatabaseError(error);
    } finally {
      client.release();
    }
  }

  async listEngineeringCutListParts(packageId: string) {
    await this.ensurePackageExists(packageId);
    const result = await this.pool.query(`
      SELECT p.id, p.package_id AS "packageId", p.source_file_id AS "sourceFileId",
             p.line_no AS "lineNo", p.part_name AS "partName",
             p.length_mm::text AS "lengthMm", p.width_mm::text AS "widthMm",
             p.thickness_mm::text AS "thicknessMm", p.quantity::text AS quantity, p.notes,
             f.file_name AS "fileName", f.material_code AS "materialCode",
             f.material_color AS "materialColor", f.thickness_mm::text AS "boardThicknessMm",
             f.board_length_mm::text AS "boardLengthMm", f.board_width_mm::text AS "boardWidthMm",
             f.board_quantity::text AS "boardQuantity"
      FROM engineering_cutlist_parts p
      JOIN engineering_package_files f ON f.id = p.source_file_id AND f.package_id = p.package_id
      WHERE p.package_id = $1
      ORDER BY f.created_at, f.file_name, p.line_no`, [packageId]);
    return result.rows;
  }

  async addEngineeringCutListPart(packageId: string, body: CreateEngineeringCutListPartInput) {
    if (!cleanOptional(body.partName)) throw new BadRequestException('اسم قطعة القص مطلوب.');
    if (![body.lengthMm, body.widthMm, body.quantity].every(value => typeof value === 'number' && Number.isFinite(value) && value > 0))
      throw new BadRequestException('الطول والعرض والكمية يجب أن تكون أكبر من صفر.');
    if (body.thicknessMm !== undefined && body.thicknessMm !== null &&
        (typeof body.thicknessMm !== 'number' || !Number.isFinite(body.thicknessMm) || body.thicknessMm <= 0))
      throw new BadRequestException('سمك القطعة يجب أن يكون أكبر من صفر.');

    const client = await this.pool.connect();
    try {
      await client.query('BEGIN');
      await this.lockDraftPackage(client, packageId);
      const fileResult = await client.query(`
        SELECT id FROM engineering_package_files
        WHERE id = $1 AND package_id = $2 AND attachment_type = 'cut_list'
        FOR SHARE`, [body.sourceFileId, packageId]);
      if (!fileResult.rowCount) throw new BadRequestException('اختر مرفق كاتلست تابعاً لهذه النسخة الفنية.');
      const lineNoResult = await client.query(
        'SELECT COALESCE(MAX(line_no), 0) + 1 AS line_no FROM engineering_cutlist_parts WHERE source_file_id = $1',
        [body.sourceFileId],
      );
      const result = await client.query(`
        INSERT INTO engineering_cutlist_parts
          (package_id, source_file_id, line_no, part_name, length_mm, width_mm, thickness_mm, quantity, notes)
        VALUES ($1, $2, $3, $4, $5::numeric, $6::numeric, $7::numeric, $8::numeric, $9)
        RETURNING id, package_id AS "packageId", source_file_id AS "sourceFileId", line_no AS "lineNo",
                  part_name AS "partName", length_mm::text AS "lengthMm", width_mm::text AS "widthMm",
                  thickness_mm::text AS "thicknessMm", quantity::text AS quantity, notes`,
        [packageId, body.sourceFileId, Number(lineNoResult.rows[0].line_no), clean(body.partName),
          String(body.lengthMm), String(body.widthMm), body.thicknessMm == null ? null : String(body.thicknessMm),
          String(body.quantity), cleanOptional(body.notes)],
      );
      await client.query('COMMIT');
      return result.rows[0];
    } catch (error) {
      await safeRollback(client);
      this.translateDatabaseError(error);
    } finally {
      client.release();
    }
  }

  async finalizeEngineeringPackage(packageId: string) {
    const client = await this.pool.connect();
    try {
      await client.query('BEGIN');
      const ownerResult = await client.query(
        'SELECT customer_order_id FROM engineering_packages WHERE id = $1',
        [packageId],
      );
      if (!ownerResult.rowCount) throw new NotFoundException('النسخة الفنية غير موجودة.');
      const customerOrderId = ownerResult.rows[0].customer_order_id as string;
      // Keep lock ordering consistent with production-order creation: order first, then package.
      await this.lockOrder(client, customerOrderId);
      const packageResult = await client.query(
        'SELECT id, customer_order_id, status, product_code FROM engineering_packages WHERE id = $1 FOR UPDATE',
        [packageId],
      );
      const current = packageResult.rows[0];
      if (!current) throw new NotFoundException('النسخة الفنية غير موجودة.');
      if (current.customer_order_id !== customerOrderId) throw new ConflictException('تغير ارتباط النسخة الفنية بالطلب. حدّث الشاشة ثم حاول مرة أخرى.');
      if (current.status === 'finalized') {
        await client.query('COMMIT');
        return this.getPackage(packageId);
      }
      if (current.status === 'superseded') {
        throw new ConflictException('هذه نسخة قديمة؛ أنشئ نسخة جديدة بدلاً من إعادة تفعيلها.');
      }
      if (!cleanOptional(current.product_code)) {
        throw new BadRequestException('كود المنتج مطلوب قبل تثبيت النسخة الفنية.');
      }
      const requiredFiles = await client.query(`
        SELECT
          COUNT(*) FILTER (WHERE attachment_type = 'product_reference')::int AS product_reference_count,
          COUNT(*) FILTER (WHERE attachment_type = 'cut_list')::int AS cut_list_count
        FROM engineering_package_files WHERE package_id = $1`, [packageId]);
      const requiredCounts = requiredFiles.rows[0];
      if (Number(requiredCounts.product_reference_count) < 1) {
        throw new BadRequestException('ارفع صورة المنتج المرجعية قبل تثبيت النسخة الفنية.');
      }
      if (Number(requiredCounts.cut_list_count) < 1) {
        throw new BadRequestException('أضف كاتلست واحداً على الأقل قبل تثبيت النسخة الفنية.');
      }
      await client.query(`UPDATE engineering_packages SET status = 'superseded'
        WHERE customer_order_id = $1 AND status = 'finalized'`, [current.customer_order_id]);
      await client.query(`UPDATE engineering_packages SET status = 'finalized', finalized_at = now()
        WHERE id = $1`, [packageId]);
      await client.query('COMMIT');
      return this.getPackage(packageId);
    } catch (error) {
      await safeRollback(client);
      this.translateDatabaseError(error);
    } finally {
      client.release();
    }
  }

  async listPackageFiles(packageId: string) {
    await this.ensurePackageExists(packageId);
    const result = await this.pool.query(`
      SELECT id, package_id AS "packageId", file_name AS "fileName", mime_type AS "mimeType",
             size_bytes AS "sizeBytes", checksum_sha256 AS "checksumSha256",
             attachment_type AS "attachmentType", description,
             material_code AS "materialCode", material_item_id AS "materialItemId",
             material_color AS "materialColor", waste_percentage::text AS "wastePercentage",
             thickness_mm::text AS "thicknessMm", board_length_mm::text AS "boardLengthMm",
             board_width_mm::text AS "boardWidthMm", board_quantity::text AS "boardQuantity",
             created_at AS "createdAt"
      FROM engineering_package_files WHERE package_id = $1 ORDER BY created_at, id`, [packageId]);
    return result.rows;
  }

  async uploadPackageFile(
    packageId: string,
    file: UploadedEngineeringFile,
    userId?: string,
    metadata: UploadEngineeringFileMetadataInput = {},
  ) {
    if (!file?.buffer?.length) throw new BadRequestException('اختر ملفاً صالحاً أولاً.');
    if (file.size > 20 * 1024 * 1024) throw new BadRequestException('الحد الأقصى لحجم الملف 20 ميجابايت.');
    const attachmentType = metadata.attachmentType ?? 'other';
    const allowedTypes = new Set<EngineeringAttachmentType>(['product_reference', 'dimensioned_drawing', 'cut_list', 'bom_export', 'other']);
    if (!allowedTypes.has(attachmentType)) throw new BadRequestException('نوع المرفق غير معروف.');
    if (attachmentType === 'cut_list') {
      if (!metadata.materialItemId || !cleanOptional(metadata.materialColor))
        throw new BadRequestException('اختر خامة اللوح وحدد اللون لمرفق الكاتلست.');
      if (typeof metadata.boardQuantity !== 'number' || !Number.isFinite(metadata.boardQuantity) || metadata.boardQuantity <= 0)
        throw new BadRequestException('أدخل عدد الألواح بقيمة أكبر من صفر.');
      if (typeof metadata.wastePercentage !== 'number' || !Number.isFinite(metadata.wastePercentage) || metadata.wastePercentage < 0 || metadata.wastePercentage > 100)
        throw new BadRequestException('أدخل نسبة هالك من 0 إلى 100.');
    }
    const originalName = basename(file.originalname.replace(/\\/g, '/')).replace(/[\r\n\0]/g, '').trim();
    const extension = extname(originalName).toLowerCase();
    const allowed = new Set(['.pdf', '.png', '.jpg', '.jpeg', '.webp', '.xlsx', '.csv', '.dwg', '.dxf', '.doc', '.docx', '.zip']);
    if (!originalName || !allowed.has(extension)) {
      throw new BadRequestException('الملفات المسموحة: PDF، الصور، Excel/CSV، DWG/DXF، Word أو ZIP.');
    }

    const client = await this.pool.connect();
    let storedPath: string | undefined;
    try {
      await client.query('BEGIN');
      await this.lockDraftPackage(client, packageId);
      let boardItemCode: string | null = null;
      if (attachmentType === 'cut_list') {
        const boardItemResult = await client.query(`
          SELECT id, code, item_type, stock_tracked, classification, active
          FROM items WHERE id = $1 FOR SHARE`, [metadata.materialItemId]);
        const boardItem = boardItemResult.rows[0];
        if (!boardItem || !boardItem.active || boardItem.item_type !== 'raw_material' ||
            !boardItem.stock_tracked || boardItem.classification !== 'materials') {
          throw new BadRequestException('خامة اللوح يجب أن تكون خامة نشطة ومتابعة بالمخزون من البيانات الأساسية.');
        }
        boardItemCode = boardItem.code as string;
      }
      await mkdir(this.storageDirectory, { recursive: true });
      const storageKey = `${randomUUID()}${extension}`;
      storedPath = resolve(this.storageDirectory, storageKey);
      const checksum = createHash('sha256').update(file.buffer).digest('hex');
      await writeFile(storedPath, file.buffer, { flag: 'wx' });
      const result = await client.query(`
        INSERT INTO engineering_package_files
          (package_id, file_name, mime_type, size_bytes, storage_key, checksum_sha256, uploaded_by,
           attachment_type, description, material_code, material_color, material_item_id,
           waste_percentage, thickness_mm, board_length_mm, board_width_mm, board_quantity)
        VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13::numeric, NULL, NULL, NULL, $14::numeric)
        RETURNING id, package_id AS "packageId", file_name AS "fileName", mime_type AS "mimeType",
                  size_bytes AS "sizeBytes", checksum_sha256 AS "checksumSha256",
                  attachment_type AS "attachmentType", description,
                  material_code AS "materialCode", material_item_id AS "materialItemId",
                  material_color AS "materialColor", waste_percentage::text AS "wastePercentage",
                  thickness_mm::text AS "thicknessMm", board_length_mm::text AS "boardLengthMm",
                  board_width_mm::text AS "boardWidthMm", board_quantity::text AS "boardQuantity",
                  created_at AS "createdAt"`,
        [packageId, originalName.slice(0, 255), file.mimetype || 'application/octet-stream', file.size,
          storageKey, checksum, userId ?? null, attachmentType, cleanOptional(metadata.description),
          attachmentType === 'cut_list' ? boardItemCode : null,
          attachmentType === 'cut_list' ? cleanOptional(metadata.materialColor) : null,
          attachmentType === 'cut_list' ? metadata.materialItemId : null,
          attachmentType === 'cut_list' ? String(metadata.wastePercentage) : null,
          attachmentType === 'cut_list' ? String(metadata.boardQuantity) : null],
      );
      await client.query('COMMIT');
      return result.rows[0];
    } catch (error) {
      await safeRollback(client);
      if (storedPath) await unlink(storedPath).catch(() => undefined);
      this.translateDatabaseError(error);
    } finally {
      client.release();
    }
  }

  async getPackageFile(packageId: string, fileId: string) {
    const result = await this.pool.query(`
      SELECT id, package_id AS "packageId", file_name AS "fileName", mime_type AS "mimeType",
             size_bytes AS "sizeBytes", storage_key AS "storageKey"
      FROM engineering_package_files WHERE id = $1 AND package_id = $2`, [fileId, packageId]);
    const file = result.rows[0];
    if (!file) throw new NotFoundException('الملف غير موجود.');
    const filePath = resolve(this.storageDirectory, basename(file.storageKey));
    try {
      const fileStats = await stat(filePath);
      if (!fileStats.isFile()) throw new Error('Not a file');
    } catch {
      throw new NotFoundException('الملف غير متاح على وحدة التخزين.');
    }
    return { ...file, filePath };
  }

  async listProductionOrders() {
    const result = await this.pool.query(`
      SELECT po.id, po.order_no AS "orderNo",po.job_name AS "jobName",po.planned_end_date::text AS "plannedEndDate", po.production_mode AS "productionMode",
             po.customer_order_id AS "customerOrderId", co.order_no AS "customerOrderNo",
             p.name_ar AS "customerNameAr",
             po.engineering_package_id AS "engineeringPackageId", ep.revision AS "engineeringRevision",
             po.product_code AS "productCode", po.product_name AS "productName",
             po.production_date::text AS "productionDate",
             po.planned_quantity::text AS "plannedQuantity",
             po.status, po.created_at AS "createdAt"
      FROM production_orders po
      LEFT JOIN customer_orders co ON co.id = po.customer_order_id
      LEFT JOIN parties p ON p.id = co.customer_id
      LEFT JOIN engineering_packages ep ON ep.id = po.engineering_package_id
      ORDER BY po.created_at DESC`);
    return result.rows;
  }

  async createDraftProductionOrder(orderId: string, body: CreateProductionOrderInput, userId?: string) {
    const client = await this.pool.connect();
    try {
      await client.query('BEGIN');
      const orderResult = await client.query(
        'SELECT id, status, confirmed_at,product_code,product_name,order_date::text,requested_delivery_date::text FROM customer_orders WHERE id = $1 FOR UPDATE', [orderId]);
      const order = orderResult.rows[0];
      if (!order) throw new NotFoundException('الطلب غير موجود.');
      if (order.status !== 'open') throw new BadRequestException('لا يمكن إنشاء أمر شغل لطلب ملغي.');
      let packageId: string | null = body.engineeringPackageId ?? null;
      let legacyProductCode:string|undefined,legacyProductName:string|undefined;
      // Historical orders can retain their finalized revision. Confirmed orders
      // from the complete order form are themselves the source for the job sheet.
      if (packageId || !order.confirmed_at) {
        const packageResult = await client.query(`
          SELECT id, status,product_code,product_description FROM engineering_packages
          WHERE customer_order_id = $1 AND ($2::uuid IS NULL OR id = $2)
            AND status = 'finalized' ORDER BY revision DESC LIMIT 1 FOR UPDATE`, [orderId, packageId]);
        if (!packageResult.rowCount) throw new BadRequestException('أكد بيانات الطلب أولاً أو استخدم طلباً قديماً له نسخة فنية مثبتة.');
        packageId = packageResult.rows[0].id;
        legacyProductCode=packageResult.rows[0].product_code;
        legacyProductName=packageResult.rows[0].product_description;
      }
      // Confirmed parent identity is authoritative; callers cannot silently create
      // a job for a different product beneath the selected customer order.
      const productCode=order.product_code??legacyProductCode??body.productCode;
      const productName=order.product_name??legacyProductName??body.productName??productCode;
      if(!productCode?.trim()||!productName?.trim())throw new BadRequestException('The selected order needs a product code and name.');
      const start=body.productionDate??order.order_date??new Date().toISOString().slice(0,10);
      const end=body.plannedEndDate??(body.productionDate?null:order.requested_delivery_date)??null;
      if(!body.orderNo.trim()||(body.jobName!==undefined&&!body.jobName.trim()))throw new BadRequestException('A job code and nonempty job name are required.');
      if(!/^\d{4}-\d{2}-\d{2}$/.test(start)||(end&&!/^\d{4}-\d{2}-\d{2}$/.test(end)))throw new BadRequestException('Use YYYY-MM-DD for job dates.');
      if(end&&end<start)throw new BadRequestException('The job finish date cannot precede its start date.');
      const result = await client.query(`
        INSERT INTO production_orders
          (order_no, production_mode, customer_order_id, engineering_package_id,
             product_code, product_name, production_date, planned_quantity, status, created_by,job_name,planned_end_date)
          VALUES ($1, 'customer_order', $2, $3, $4, $5, COALESCE($6::date, CURRENT_DATE), $7::numeric, 'draft', $8,$9,$10::date)
        RETURNING id, order_no AS "orderNo", production_mode AS "productionMode",
                  customer_order_id AS "customerOrderId", engineering_package_id AS "engineeringPackageId",
                  product_code AS "productCode", product_name AS "productName",
                  production_date::text AS "productionDate", planned_quantity::text AS "plannedQuantity",
                  status, created_at AS "createdAt"`,
          [clean(body.orderNo), orderId, packageId, clean(productCode),
            clean(productName), start,
            String(body.plannedQuantity), userId ?? null,clean(body.jobName??body.orderNo),end],
      );
      await client.query('COMMIT');
      return result.rows[0];
    } catch (error) {
      await safeRollback(client);
      this.translateDatabaseError(error);
    } finally {
      client.release();
    }
  }

  async getProductionWorkOrder(productionOrderId: string) {
    const orderResult = await this.pool.query(`
      SELECT po.id, po.order_no AS "orderNo",po.job_name AS "jobName",po.planned_end_date::text AS "plannedEndDate", po.production_mode AS "productionMode",
             po.customer_order_id AS "customerOrderId", co.order_no AS "customerOrderNo",
             p.name_ar AS "customerNameAr", po.engineering_package_id AS "engineeringPackageId",
             ep.revision AS "engineeringRevision", ep.technical_reference AS "technicalReference",
             po.product_code AS "productCode", po.product_name AS "productName",
             po.production_date::text AS "productionDate", po.planned_quantity::text AS "plannedQuantity",
             po.status, po.created_at AS "createdAt"
      FROM production_orders po
      LEFT JOIN customer_orders co ON co.id = po.customer_order_id
      LEFT JOIN parties p ON p.id = co.customer_id
      LEFT JOIN engineering_packages ep ON ep.id = po.engineering_package_id
      WHERE po.id = $1`, [productionOrderId]);
    if (!orderResult.rowCount) throw new NotFoundException('أمر الإنتاج غير موجود.');

    const savedLines = await this.pool.query(`
      SELECT line_no AS "lineNo", catalog_code AS "catalogCode", item_name_ar AS "itemNameAr",
             specification, unit_code AS "unitCode", quantity::text AS quantity,
             is_selected AS "isSelected", is_custom AS "isCustom"
      FROM production_work_order_lines
      WHERE production_order_id = $1 ORDER BY line_no`, [productionOrderId]);

    if (savedLines.rowCount) return { productionOrder: orderResult.rows[0], lines: savedLines.rows };

    const catalog = await this.pool.query(`
      SELECT item_code AS "catalogCode", item_name_ar AS "itemNameAr",
             default_specification AS specification, unit_code AS "unitCode",
             sort_order AS "lineNo"
      FROM production_work_order_catalog WHERE active = true ORDER BY sort_order`);
    const defaults = catalog.rows.map((row) => ({
      ...row, quantity: null, isSelected: false, isCustom: false,
    }));
    defaults.push(
      { lineNo: defaults.length + 1, catalogCode: null, itemNameAr: '', specification: '', unitCode: '', quantity: null, isSelected: false, isCustom: true },
      { lineNo: defaults.length + 2, catalogCode: null, itemNameAr: '', specification: '', unitCode: '', quantity: null, isSelected: false, isCustom: true },
    );
    return { productionOrder: orderResult.rows[0], lines: defaults };
  }

  async saveProductionWorkOrder(productionOrderId: string, body: SaveProductionWorkOrderInput) {
    if (!clean(body.productName)) throw new BadRequestException('اسم المنتج مطلوب.');
    if (!/^\d{4}-\d{2}-\d{2}$/.test(body.productionDate) || Number.isNaN(Date.parse(body.productionDate))) {
      throw new BadRequestException('أدخل تاريخ أمر الشغل بصيغة YYYY-MM-DD.');
    }
    if (!Array.isArray(body.lines)) throw new BadRequestException('بنود أمر الشغل غير صالحة.');

    const client = await this.pool.connect();
    try {
      await client.query('BEGIN');
      const orderResult = await client.query(
        'SELECT id, status FROM production_orders WHERE id = $1 FOR UPDATE', [productionOrderId],
      );
      if (!orderResult.rowCount) throw new NotFoundException('أمر الإنتاج غير موجود.');
      if (orderResult.rows[0].status !== 'draft') {
        throw new ConflictException('لا يمكن تعديل أمر الشغل بعد بدء تنفيذ أمر الإنتاج.');
      }

      const catalogResult = await client.query(`
        SELECT item_code, item_name_ar, default_specification, unit_code, sort_order
        FROM production_work_order_catalog WHERE active = true ORDER BY sort_order`);
      const catalogByCode = new Map<string, Record<string, any>>(
        catalogResult.rows.map((row): [string, Record<string, any>] => [String(row.item_code), row]),
      );
      const catalogLines = body.lines.filter((line) => !line.isCustom);
      const customLines = body.lines.filter((line) => line.isCustom);
      const submittedCodes = catalogLines.map((line) => clean(line.catalogCode || ''));
      if (customLines.length !== 2 || catalogLines.length !== catalogByCode.size ||
          new Set(submittedCodes).size !== submittedCodes.length ||
          submittedCodes.some((code) => !catalogByCode.has(code)) ||
          Array.from(catalogByCode.keys()).some((code) => !submittedCodes.includes(code))) {
        throw new BadRequestException('تحقق من بنود النموذج: يجب الاحتفاظ بكل البنود القياسية وبسطرَي الإضافات.');
      }
      if (body.lines.length > 40) throw new BadRequestException('عدد بنود أمر الشغل أكبر من الحد المسموح.');
      for (const line of body.lines) {
        if (line.isSelected && (!(Number(line.quantity) > 0) ||
            !(line.isCustom ? clean(line.itemNameAr) : true) ||
            !(line.isCustom ? clean(line.unitCode) : true))) {
          throw new BadRequestException('كل بند محدد يجب أن يحتوي على اسم ووحدة وكمية أكبر من صفر.');
        }
      }

      await client.query(`UPDATE production_orders
        SET product_name = $2, production_date = $3::date
        WHERE id = $1`, [productionOrderId, clean(body.productName), body.productionDate]);
      await client.query('DELETE FROM production_work_order_lines WHERE production_order_id = $1', [productionOrderId]);

      for (let index = 0; index < body.lines.length; index++) {
        const line = body.lines[index];
        const catalogItem = line.isCustom ? null : catalogByCode.get(clean(line.catalogCode || ''));
        if (!catalogItem && !line.isCustom) throw new BadRequestException('بند قياسي غير معروف في أمر الشغل.');
        const itemName = line.isCustom ? cleanOptional(line.itemNameAr) ?? '' : String(catalogItem!.item_name_ar);
        const unitCode = line.isCustom ? cleanOptional(line.unitCode) ?? '' : String(catalogItem!.unit_code);
        const quantity = line.quantity === undefined || line.quantity === null
          ? null : String(line.quantity);
        await client.query(`
          INSERT INTO production_work_order_lines
            (production_order_id, line_no, catalog_code, item_name_ar, specification,
             unit_code, quantity, is_selected, is_custom)
          VALUES ($1, $2, $3, $4, $5, $6, $7::numeric, $8, $9)`,
        [productionOrderId, index + 1, line.isCustom ? null : line.catalogCode,
          itemName, cleanOptional(line.specification) ?? '', unitCode, quantity,
          Boolean(line.isSelected), Boolean(line.isCustom)]);
      }

      await client.query('COMMIT');
      return await this.getProductionWorkOrder(productionOrderId);
    } catch (error) {
      await safeRollback(client);
      this.translateDatabaseError(error);
    } finally {
      client.release();
    }
  }

  private async getPackage(packageId: string) {
    const result = await this.pool.query(`
      SELECT id, customer_order_id AS "customerOrderId", revision,
             product_code AS "productCode", product_description AS "productDescription",
             upholstery_required AS "upholsteryRequired",
             technical_reference AS "technicalReference", notes, status,
             created_at AS "createdAt", finalized_at AS "finalizedAt",
             (SELECT COUNT(*)::int FROM engineering_material_lines ml WHERE ml.package_id = engineering_packages.id) AS "lineCount",
             (SELECT COUNT(*)::int FROM engineering_product_material_lines pml WHERE pml.package_id = engineering_packages.id) AS "productMaterialLineCount",
             (SELECT COUNT(*)::int FROM engineering_package_files f WHERE f.package_id = engineering_packages.id) AS "fileCount"
      FROM engineering_packages WHERE id = $1`, [packageId]);
    if (!result.rowCount) throw new NotFoundException('النسخة الفنية غير موجودة.');
    return result.rows[0];
  }

  private async ensureOrderExists(orderId: string) {
    const result = await this.pool.query('SELECT id FROM customer_orders WHERE id = $1', [orderId]);
    if (!result.rowCount) throw new NotFoundException('الطلب غير موجود.');
  }

  private async ensurePackageExists(packageId: string) {
    const result = await this.pool.query('SELECT id FROM engineering_packages WHERE id = $1', [packageId]);
    if (!result.rowCount) throw new NotFoundException('النسخة الفنية غير موجودة.');
  }

  private async lockOrder(client: PoolClient, orderId: string) {
    const result = await client.query('SELECT id FROM customer_orders WHERE id = $1 FOR UPDATE', [orderId]);
    if (!result.rowCount) throw new NotFoundException('الطلب غير موجود.');
  }

  private async lockDraftPackage(client: PoolClient, packageId: string) {
    const result = await client.query('SELECT id, status FROM engineering_packages WHERE id = $1 FOR UPDATE', [packageId]);
    if (!result.rowCount) throw new NotFoundException('النسخة الفنية غير موجودة.');
    if (result.rows[0].status !== 'draft') {
      throw new ConflictException('النسخة الفنية مثبتة ولا يمكن تعديلها. أنشئ نسخة جديدة لأي تغيير.');
    }
  }

  private translateDatabaseError(error: unknown): never {
    if (error instanceof BadRequestException || error instanceof ConflictException || error instanceof NotFoundException) throw error;
    const pgError = error as { code?: string };
    if (pgError?.code === '23505') throw new ConflictException('يوجد سجل بنفس رقم الطلب أو مرجع مكرر.');
    if (pgError?.code === '23503') throw new BadRequestException('أحد المراجع المحددة غير موجود. حدّث البيانات ثم حاول مرة أخرى.');
    if (pgError?.code === '23514' || pgError?.code === '22P02' || pgError?.code === '22007' || pgError?.code === '22008') {
      throw new BadRequestException('تحقق من البيانات المدخلة ثم حاول مرة أخرى.');
    }
    throw error;
  }
}

function clean(value: string): string {
  return value.trim();
}

function cleanOptional(value?: string): string | null {
  const cleaned = value?.trim();
  return cleaned ? cleaned : null;
}

async function safeRollback(client: PoolClient): Promise<void> {
  await client.query('ROLLBACK').catch(() => undefined);
}
