-- Sprint 6: production work-order sheet based on the factory's existing A3 form.
-- Additive migration: preserve existing production orders and inventory records.

ALTER TABLE production_orders
  ADD COLUMN IF NOT EXISTS product_name TEXT;
ALTER TABLE production_orders
  ADD COLUMN IF NOT EXISTS production_date DATE;

UPDATE production_orders
SET product_name = product_code
WHERE product_name IS NULL OR length(btrim(product_name)) = 0;

UPDATE production_orders
SET production_date = created_at::date
WHERE production_date IS NULL;

ALTER TABLE production_orders
  ALTER COLUMN product_name SET DEFAULT '',
  ALTER COLUMN product_name SET NOT NULL,
  ALTER COLUMN production_date SET DEFAULT CURRENT_DATE,
  ALTER COLUMN production_date SET NOT NULL;

CREATE TABLE IF NOT EXISTS production_work_order_catalog (
  item_code TEXT PRIMARY KEY,
  item_name_ar TEXT NOT NULL CHECK (length(btrim(item_name_ar)) BETWEEN 1 AND 160),
  default_specification TEXT NOT NULL DEFAULT '',
  unit_code TEXT NOT NULL CHECK (length(btrim(unit_code)) BETWEEN 1 AND 40),
  sort_order INTEGER NOT NULL UNIQUE CHECK (sort_order > 0),
  active BOOLEAN NOT NULL DEFAULT TRUE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- The standard entries follow the source work-order form (A3 page 1).
INSERT INTO production_work_order_catalog
  (item_code, item_name_ar, default_specification, unit_code, sort_order)
VALUES
  ('MDF', 'MDF', 'عدد الألواح للتقطيع: (                 )', 'لوح', 1),
  ('CNT', 'CNT', 'عدد الألواح للتقطيع: (                 )', 'لوح', 2),
  ('HDF', 'HDF', 'عدد الألواح للتقطيع: (                 )', 'لوح', 3),
  ('PVC-22', 'PVC 22', 'شريط تكسية عريض (22 مم)', 'متر طولي', 4),
  ('PVC-40', 'PVC 40', 'شريط تكسية عريض (40 مم)', 'متر طولي', 5),
  ('HINGE-STANDARD', 'مفصلة عادية', 'مفصلة تجميع عادية', 'قطعة', 6),
  ('HINGE-SOFT-CLOSE', 'مفصلة سوفت كلوز', 'مفصلة هيدروليك ممتصة للصدمات', 'قطعة', 7),
  ('DRAWER-RUNNER-STANDARD', 'مجرى عادية', 'مقاس: (                 ) سم', 'طقم', 8),
  ('DRAWER-RUNNER-SOFT-CLOSE', 'مجرى سوفت كلوز', 'مقاس: (                 ) سم', 'طقم', 9),
  ('HANDLE-BUTTON', 'مقبض زرار', 'مقبض مفرد (نقطة واحدة)', 'قطعة', 10),
  ('HANDLE-PIPE', 'مقبض ماسورة', 'مقبض معدني ماسورة', 'قطعة', 11),
  ('HANDLE-RECESSED', 'مقبض دفن', 'مقبض غاطس داخل الخشب', 'قطعة', 12),
  ('HANDLE-DECORATIVE', 'مقبض ديكور', 'مقبض تصميم خاص / ديكوري', 'قطعة', 13),
  ('SLIDING-DOOR-WHEELS', 'طقم عجل دولاب', 'عجل ومجاري دولاب جرار', 'طقم', 14),
  ('SLIDING-DOOR-ALUMINIUM-RAIL', 'عود ألومنيوم جرار', 'مجرى / مجري ألومنيوم للدولاب', 'عود', 15),
  ('SLIDING-DOOR-PVC-RAIL', 'عود PVC جرار', 'مجرى بلاستيك / PVC للدولاب', 'عود', 16),
  ('HANGER-PIPE', 'ماسورة شماعة', 'ماسورة تعليق ملابس (بالمتر)', 'متر طولي', 17),
  ('HANGER-PIPE-HOLDER', 'حامل ماسورة شماعة', 'كابولي / تثبيت جانبي للماسورة', 'قطعة', 18),
  ('BED-ASSEMBLY-SET', 'طقم تجميع سرير', 'زوايا وإكسسوارات تجميع السرير', 'طقم', 19),
  ('SHELF-SUPPORT', 'تكاية رف', 'حوامل وركاكات تثبيت الأرفف', 'قطعة', 20),
  ('FLOOR-LEVELER', 'تكاية أرضية', 'رجلاش / قواعد أرضية حماية', 'قطعة', 21),
  ('ASSEMBLY-SILICONE', 'فراشة تجميع سيليكون', 'تجميع مستلزمات وسيليكون', 'قطعة', 22),
  ('METAL-L-ANGLE', 'زوايا معدن تجميع حرف L', 'زوايا تثبيت معدنية قائمة', 'قطعة', 23),
  ('WARDROBE-CONNECTOR-CLIP', 'كلبس تجميع دولاب فك وربط', 'كلبسات تجميع وميني فيكس', 'قطعة', 24)
ON CONFLICT (item_code) DO NOTHING;

CREATE TABLE IF NOT EXISTS production_work_order_lines (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  production_order_id UUID NOT NULL REFERENCES production_orders(id),
  line_no INTEGER NOT NULL CHECK (line_no > 0),
  catalog_code TEXT REFERENCES production_work_order_catalog(item_code),
  item_name_ar TEXT NOT NULL DEFAULT '',
  specification TEXT NOT NULL DEFAULT '',
  unit_code TEXT NOT NULL DEFAULT '',
  quantity NUMERIC(18,4) CHECK (quantity IS NULL OR quantity > 0),
  is_selected BOOLEAN NOT NULL DEFAULT FALSE,
  is_custom BOOLEAN NOT NULL DEFAULT FALSE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (production_order_id, line_no),
  CHECK (NOT is_selected OR (quantity IS NOT NULL AND quantity > 0 AND length(btrim(item_name_ar)) > 0 AND length(btrim(unit_code)) > 0)),
  CHECK ((is_custom AND catalog_code IS NULL) OR (NOT is_custom AND catalog_code IS NOT NULL))
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_work_order_line_catalog_code
  ON production_work_order_lines(production_order_id, catalog_code)
  WHERE catalog_code IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_work_order_lines_order
  ON production_work_order_lines(production_order_id, line_no);
