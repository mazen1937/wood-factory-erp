-- Sprint 7 redesign: product identity + color-specific board needs + product-wide recipe.
-- Additive migration. Existing attachments, cut-list piece rows, work orders and PDFs are preserved.

ALTER TABLE engineering_packages
  ADD COLUMN IF NOT EXISTS product_code TEXT,
  ADD COLUMN IF NOT EXISTS product_description TEXT,
  ADD COLUMN IF NOT EXISTS upholstery_required BOOLEAN;

-- New revisions must explicitly record a boolean. NULL is retained for older revisions
-- so historical records are not silently assigned a made-up upholstery value.
ALTER TABLE engineering_package_files
  ADD COLUMN IF NOT EXISTS material_item_id UUID REFERENCES items(id),
  ADD COLUMN IF NOT EXISTS waste_percentage NUMERIC(5,2);

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint
    WHERE conname = 'ck_engineering_packages_product_code'
      AND conrelid = 'engineering_packages'::regclass
  ) THEN
    ALTER TABLE engineering_packages
      ADD CONSTRAINT ck_engineering_packages_product_code
      CHECK (product_code IS NULL OR length(btrim(product_code)) BETWEEN 1 AND 80);
  END IF;

  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint
    WHERE conname = 'ck_engineering_packages_product_description'
      AND conrelid = 'engineering_packages'::regclass
  ) THEN
    ALTER TABLE engineering_packages
      ADD CONSTRAINT ck_engineering_packages_product_description
      CHECK (product_description IS NULL OR length(btrim(product_description)) BETWEEN 1 AND 240);
  END IF;

  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint
    WHERE conname = 'ck_engineering_package_files_waste_percentage'
      AND conrelid = 'engineering_package_files'::regclass
  ) THEN
    ALTER TABLE engineering_package_files
      ADD CONSTRAINT ck_engineering_package_files_waste_percentage
      CHECK (waste_percentage IS NULL OR waste_percentage BETWEEN 0 AND 100);
  END IF;

  -- Replace Sprint 7's earlier constraint, which incorrectly required board dimensions
  -- that the redesigned workflow intentionally does not capture.
  IF EXISTS (
    SELECT 1 FROM pg_constraint
    WHERE conname = 'ck_engineering_package_files_cut_list_metadata'
      AND conrelid = 'engineering_package_files'::regclass
  ) THEN
    ALTER TABLE engineering_package_files
      DROP CONSTRAINT ck_engineering_package_files_cut_list_metadata;
  END IF;

  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint
    WHERE conname = 'ck_engineering_package_files_cut_list_metadata_v2'
      AND conrelid = 'engineering_package_files'::regclass
  ) THEN
    ALTER TABLE engineering_package_files
      ADD CONSTRAINT ck_engineering_package_files_cut_list_metadata_v2
      CHECK (
        attachment_type <> 'cut_list' OR (
          length(btrim(COALESCE(material_color, ''))) > 0 AND
          COALESCE(board_quantity > 0, false) AND
          (waste_percentage IS NULL OR waste_percentage BETWEEN 0 AND 100)
        )
      );
  END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_engineering_packages_product_code
  ON engineering_packages(product_code) WHERE product_code IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_engineering_package_files_material_item
  ON engineering_package_files(material_item_id) WHERE material_item_id IS NOT NULL;

-- Shared components are expressed per one finished product; the same accessories are
-- therefore not duplicated once per color-specific cut-list.
CREATE TABLE IF NOT EXISTS engineering_product_material_lines (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  package_id UUID NOT NULL REFERENCES engineering_packages(id),
  line_no INTEGER NOT NULL CHECK (line_no > 0),
  line_type TEXT NOT NULL CHECK (line_type IN ('accessory', 'raw_material', 'paint')),
  item_id UUID NOT NULL REFERENCES items(id),
  quantity_per_unit NUMERIC(18,4) NOT NULL CHECK (quantity_per_unit > 0),
  notes TEXT,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE(package_id, line_no)
);

CREATE INDEX IF NOT EXISTS ix_engineering_product_material_lines_package
  ON engineering_product_material_lines(package_id, line_no);
CREATE INDEX IF NOT EXISTS ix_engineering_product_material_lines_item
  ON engineering_product_material_lines(item_id);
