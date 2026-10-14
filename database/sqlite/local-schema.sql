-- One SQLite database per desktop profile: cache/outbox only, never authoritative.
PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS outbox_operations (
  operation_id TEXT PRIMARY KEY,
  entity_type TEXT NOT NULL,
  entity_id TEXT NOT NULL,
  operation_type TEXT NOT NULL,
  payload_json TEXT NOT NULL,
  expected_version INTEGER,
  created_at_utc TEXT NOT NULL,
  attempt_count INTEGER NOT NULL DEFAULT 0,
  last_attempt_at_utc TEXT,
  state TEXT NOT NULL CHECK (state IN ('pending','sending','acknowledged','rejected')),
  rejection_code TEXT,
  rejection_detail TEXT
);
CREATE INDEX IF NOT EXISTS ix_outbox_operations_state_created ON outbox_operations(state, created_at_utc);
CREATE TABLE IF NOT EXISTS sync_cursor (
  stream_name TEXT PRIMARY KEY,
  cursor_value TEXT NOT NULL,
  updated_at_utc TEXT NOT NULL
);
