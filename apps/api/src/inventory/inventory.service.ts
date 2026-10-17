import { BadRequestException, ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { Pool } from 'pg';

@Injectable()
export class InventoryService {
  private readonly pool = new Pool({ connectionString: process.env.DATABASE_URL });

  async balances() {
    const result = await this.pool.query(`
      SELECT b.item_id AS "itemId", i.code AS "itemCode", i.name_ar AS "itemNameAr",
             u.code AS "unitCode", b.warehouse_id AS "warehouseId", w.code AS "warehouseCode",
             w.name_ar AS "warehouseNameAr", b.quantity_on_hand AS "quantityOnHand",
             c.average_unit_cost AS "averageUnitCost", c.inventory_value AS "inventoryValue"
      FROM inventory_balances b
      JOIN items i ON i.id = b.item_id
      JOIN units_of_measure u ON u.id = i.base_unit_id
      JOIN warehouses w ON w.id = b.warehouse_id
      JOIN inventory_item_costs c ON c.item_id = b.item_id
      ORDER BY i.code, w.code`);
    return result.rows;
  }

  async movements() {
    const result = await this.pool.query(`
      SELECT m.id, m.operation_id AS "operationId", m.document_reference AS "documentReference",
             m.movement_date AS "movementDate", m.item_id AS "itemId", i.code AS "itemCode",
             i.name_ar AS "itemNameAr", m.warehouse_id AS "warehouseId", w.code AS "warehouseCode",
             m.party_id AS "partyId", p.code AS "supplierCode", p.name_ar AS "supplierNameAr",
             m.quantity, m.unit_cost AS "unitCost", m.total_cost AS "totalCost",
             m.average_cost_after AS "averageCostAfter", m.quantity_after AS "quantityAfter",
             m.value_after AS "valueAfter", m.notes, m.posted_at AS "postedAt"
      FROM inventory_movements m
      JOIN items i ON i.id = m.item_id
      JOIN warehouses w ON w.id = m.warehouse_id
      LEFT JOIN parties p ON p.id = m.party_id
      ORDER BY m.posted_at DESC, m.id DESC LIMIT 250`);
    return result.rows;
  }

  async receive(body: {
    operationId: string; itemId: string; warehouseId: string; supplierId?: string;
    documentReference: string; movementDate: string; quantity: number; unitCost: number; notes?: string;
  }, userId?: string) {
    const client = await this.pool.connect();
    try {
      await client.query('BEGIN');
      // An identical retry returns the already-posted result and never applies stock twice.
      const existing = await client.query(
        `SELECT id, operation_id AS "operationId", item_id AS "itemId", warehouse_id AS "warehouseId",
                party_id AS "supplierId", document_reference AS "documentReference",
                movement_date::text AS "movementDate", quantity, unit_cost AS "unitCost",
                quantity_after AS "quantityAfter", average_cost_after AS "averageCostAfter",
                value_after AS "valueAfter"
         FROM inventory_movements WHERE operation_id = $1`, [body.operationId]);
      if (existing.rowCount) {
        const row = existing.rows[0];
        const samePayload = row.itemId === body.itemId && row.warehouseId === body.warehouseId &&
          (row.supplierId ?? null) === (body.supplierId ?? null) && row.documentReference === body.documentReference.trim() &&
          row.movementDate === body.movementDate && Number(row.quantity) === Number(body.quantity) && Number(row.unitCost) === Number(body.unitCost);
        if (!samePayload) throw new ConflictException('معرّف العملية مستخدم لطلب مختلف؛ لم يتم تكرار الحركة.');
        await client.query('COMMIT');
        return { ...row, alreadyPosted: true };
      }

      const itemResult = await client.query(
        `SELECT id, code, item_type, stock_tracked, classification FROM items WHERE id = $1 AND active = true FOR SHARE`,
        [body.itemId]);
      if (!itemResult.rowCount) throw new NotFoundException('الصنف غير موجود أو غير نشط.');
      const item = itemResult.rows[0];
      if (!item.stock_tracked || item.classification !== 'materials' || item.item_type === 'finished_good') {
        throw new BadRequestException('هذا الصنف غير مؤهل لاستلام خامات بالمخزن.');
      }

      const warehouseResult = await client.query(
        `SELECT id, warehouse_type FROM warehouses WHERE id = $1 AND active = true FOR SHARE`, [body.warehouseId]);
      if (!warehouseResult.rowCount) throw new NotFoundException('المخزن غير موجود أو غير نشط.');
      if (warehouseResult.rows[0].warehouse_type !== 'raw_material') {
        throw new BadRequestException('استلام الخامات يتطلب اختيار مخزن خامات.');
      }
      if (body.supplierId) {
        const supplier = await client.query(
          `SELECT id FROM parties WHERE id = $1 AND active = true AND party_type IN ('supplier','both')`, [body.supplierId]);
        if (!supplier.rowCount) throw new BadRequestException('المورد المحدد غير صالح.');
      }

      // Lock one item-wide valuation row so simultaneous receipts cannot calculate from stale cost.
      await client.query(`INSERT INTO inventory_item_costs(item_id) VALUES ($1) ON CONFLICT (item_id) DO NOTHING`, [body.itemId]);
      const costResult = await client.query(
        `SELECT quantity_on_hand, inventory_value FROM inventory_item_costs WHERE item_id = $1 FOR UPDATE`, [body.itemId]);
      const oldQty = Number(costResult.rows[0].quantity_on_hand);
      const oldValue = Number(costResult.rows[0].inventory_value);
      const qty = Number(body.quantity);
      const unitCost = Number(body.unitCost);
      const lineCost = Number((qty * unitCost).toFixed(4));
      const newQty = Number((oldQty + qty).toFixed(4));
      const newValue = Number((oldValue + lineCost).toFixed(4));
      const average = newQty > 0 ? Number((newValue / newQty).toFixed(4)) : 0;

      await client.query(`
        INSERT INTO inventory_balances(item_id, warehouse_id, quantity_on_hand)
        VALUES ($1, $2, $3)
        ON CONFLICT (item_id, warehouse_id) DO UPDATE
          SET quantity_on_hand = inventory_balances.quantity_on_hand + EXCLUDED.quantity_on_hand,
              updated_at = now()`, [body.itemId, body.warehouseId, qty]);
      await client.query(`
        UPDATE inventory_item_costs SET quantity_on_hand = $2, inventory_value = $3,
          average_unit_cost = $4, updated_at = now() WHERE item_id = $1`,
        [body.itemId, newQty, newValue, average]);

      const inserted = await client.query(`
        INSERT INTO inventory_movements(
          operation_id, movement_type, item_id, warehouse_id, party_id, document_reference,
          movement_date, quantity, unit_cost, total_cost, average_cost_after, quantity_after,
          value_after, notes, posted_by)
        VALUES ($1, 'receipt', $2, $3, $4, $5, $6::date, $7, $8, $9, $10, $11, $12, $13, $14)
        RETURNING id, operation_id AS "operationId", quantity_after AS "quantityAfter",
                  average_cost_after AS "averageCostAfter", value_after AS "valueAfter"`,
        [body.operationId, body.itemId, body.warehouseId, body.supplierId || null,
         body.documentReference.trim(), body.movementDate, qty, unitCost, lineCost, average, newQty, newValue,
         body.notes?.trim() || null, userId || null]);
      await client.query('COMMIT');
      return { ...inserted.rows[0], alreadyPosted: false };
    } catch (error) {
      await client.query('ROLLBACK');
      const pgError = error as { code?: string };
      if (pgError.code === '23505') throw new ConflictException('رقم العملية أو مرجع مستند المورد مستخدم بالفعل. راجع الحركات قبل إعادة المحاولة.');
      throw error;
    } finally {
      client.release();
    }
  }
}
