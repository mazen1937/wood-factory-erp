-- Sprint 7: categorize engineer deliverables and capture cut-list board/piece data.
-- Additive migration: existing attachments remain and default to attachment_type = other.

ALTER TABLE engineering_package_files
  ADD COLUMN IF NOT EXISTS attachment_type TEXT NOT NULL DEFAULT 'other',
  ADD COLUMN IF NOT EXISTS description TEXT,
  ADD COLUMN IF NOT EXISTS material_code TEXT,
  ADD COLUMN IF NOT EXISTS material_color TEXT,
  ADD COLUMN IF NOT EXISTS thickness_mm NUMERIC(10,2),
  ADD COLUMN IF NOT EXISTS board_length_mm NUMERIC(12,2),
  ADD COLUMN IF NOT EXISTS board_width_mm NUMERIC(12,2),
  ADD COLUMN IF NOT EXISTS board_quantity NUMERIC(18,4);

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint
    WHERE conname = 'ck_engineering_package_files_attachment_type'
      AND conrelid = 'engineering_package_files'::regclass
  ) THEN
    ALTER TABLE engineering_package_files
      ADD CONSTRAINT ck_engineering_package_files_attachment_type
      CHECK (attachment_type IN ('product_reference', 'dimensioned_drawing', 'cut_list', 'bom_export', 'other'));
  END IF;

  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint
    WHERE conname = 'ck_engineering_package_files_cut_list_metadata'
      AND conrelid = 'engineering_package_files'::regclass
  ) THEN
    ALTER TABLE engineering_package_files
      ADD CONSTRAINT ck_engineering_package_files_cut_list_metadata
      CHECK (
        attachment_type <> 'cut_list' OR (
          length(btrim(COALESCE(material_code, ''))) > 0 AND
          length(btrim(COALESCE(material_color, ''))) > 0 AND
          COALESCE(thickness_mm > 0, false) AND
          COALESCE(board_length_mm > 0, false) AND
          COALESCE(board_width_mm > 0, false) AND
          COALESCE(board_quantity > 0, false)
        )
      );
  END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_engineering_package_files_type
  ON engineering_package_files(package_id, attachment_type, created_at);
CREATE UNIQUE INDEX IF NOT EXISTS ux_engineering_package_files_id_package
  ON engineering_package_files(id, package_id);

CREATE TABLE IF NOT EXISTS engineering_cutlist_parts (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  package_id UUID NOT NULL REFERENCES engineering_packages(id),
  source_file_id UUID NOT NULL,
  line_no INTEGER NOT NULL CHECK (line_no > 0),
  part_name TEXT NOT NULL CHECK (length(btrim(part_name)) BETWEEN 1 AND 240),
  length_mm NUMERIC(12,2) NOT NULL CHECK (length_mm > 0),
  width_mm NUMERIC(12,2) NOT NULL CHECK (width_mm > 0),
  thickness_mm NUMERIC(10,2) CHECK (thickness_mm IS NULL OR thickness_mm > 0),
  quantity NUMERIC(18,4) NOT NULL CHECK (quantity > 0),
  notes TEXT,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  CONSTRAINT fk_engineering_cutlist_parts_source_file
    FOREIGN KEY (source_file_id, package_id)
    REFERENCES engineering_package_files(id, package_id) ON DELETE CASCADE,
  UNIQUE(source_file_id, line_no)
);

CREATE INDEX IF NOT EXISTS ix_engineering_cutlist_parts_package_file_line
  ON engineering_cutlist_parts(package_id, source_file_id, line_no);
