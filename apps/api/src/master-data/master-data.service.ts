import { ConflictException, Injectable } from '@nestjs/common';
import { Pool, QueryResultRow } from 'pg';

/**
 * Master-data queries deliberately return camelCase JSON properties so the
 * Avalonia client and any future clients share a predictable API contract.
 */
@Injectable()
export class MasterDataService {
  private readonly pool = new Pool({
    connectionString: process.env.DATABASE_URL,
  });

  async all() {
    const [units, warehouses, parties, items] = await Promise.all([
      this.pool.query(`
        SELECT id, code, name_ar AS "nameAr", name_en AS "nameEn",
               precision_digits AS "precisionDigits"
        FROM units_of_measure WHERE active = true ORDER BY code`),
      this.pool.query(`
        SELECT id, code, name_ar AS "nameAr", name_en AS "nameEn",
               warehouse_type AS "warehouseType"
        FROM warehouses WHERE active = true ORDER BY code`),
      this.pool.query(`
        SELECT id, code, party_type AS "partyType", name_ar AS "nameAr",
               name_en AS "nameEn", phone
        FROM parties WHERE active = true ORDER BY code`),
      this.pool.query(`
        SELECT id, code, name_ar AS "nameAr", name_en AS "nameEn",
               item_type AS "itemType", base_unit_id AS "baseUnitId",
               stock_tracked AS "stockTracked", classification
        FROM items WHERE active = true ORDER BY code`),
    ]);

    return {
      units: units.rows,
      warehouses: warehouses.rows,
      parties: parties.rows,
      items: items.rows,
    };
  }

  async unit(body: { code: string; nameAr: string; nameEn: string; precisionDigits: number }) {
    return this.insert(
      `INSERT INTO units_of_measure (code, name_ar, name_en, precision_digits)
       VALUES ($1, $2, $3, $4)
       RETURNING id, code, name_ar AS "nameAr", name_en AS "nameEn",
                 precision_digits AS "precisionDigits"`,
      [body.code.trim(), body.nameAr.trim(), body.nameEn.trim(), body.precisionDigits],
    );
  }

  async warehouse(body: { code: string; nameAr: string; nameEn: string; warehouseType: string }) {
    return this.insert(
      `INSERT INTO warehouses (code, name_ar, name_en, warehouse_type)
       VALUES ($1, $2, $3, $4)
       RETURNING id, code, name_ar AS "nameAr", name_en AS "nameEn",
                 warehouse_type AS "warehouseType"`,
      [body.code.trim(), body.nameAr.trim(), body.nameEn.trim(), body.warehouseType],
    );
  }

  async party(body: { code: string; partyType: string; nameAr: string; nameEn?: string; phone?: string }) {
    return this.insert(
      `INSERT INTO parties (code, party_type, name_ar, name_en, phone)
       VALUES ($1, $2, $3, $4, $5)
       RETURNING id, code, party_type AS "partyType", name_ar AS "nameAr",
                 name_en AS "nameEn", phone`,
      [body.code.trim(), body.partyType, body.nameAr.trim(), body.nameEn?.trim() || null, body.phone?.trim() || null],
    );
  }

  async item(body: {
    code: string;
    nameAr: string;
    nameEn?: string;
    itemType: string;
    baseUnitId: string;
    stockTracked: boolean;
    classification: string;
  }) {
    return this.insert(
      `INSERT INTO items (code, name_ar, name_en, item_type, base_unit_id, stock_tracked, classification)
       VALUES ($1, $2, $3, $4, $5, $6, $7)
       RETURNING id, code, name_ar AS "nameAr", name_en AS "nameEn",
                 item_type AS "itemType", base_unit_id AS "baseUnitId",
                 stock_tracked AS "stockTracked", classification`,
      [body.code.trim(), body.nameAr.trim(), body.nameEn?.trim() || null, body.itemType,
       body.baseUnitId, body.stockTracked, body.classification],
    );
  }

  private async insert(sql: string, values: unknown[]): Promise<QueryResultRow> {
    try {
      const result = await this.pool.query(sql, values);
      return result.rows[0];
    } catch (error) {
      const pgError = error as { code?: string; constraint?: string };
      if (pgError.code === '23505') {
        throw new ConflictException('يوجد سجل آخر يستخدم نفس الكود. استخدم كوداً مختلفاً.');
      }
      if (pgError.code === '23503') {
        throw new ConflictException('المرجع المحدد غير موجود. حدّث البيانات ثم حاول مرة أخرى.');
      }
      throw error;
    }
  }
}
