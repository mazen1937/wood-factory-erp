-- Durable receipt book for client-originated changes. PostgreSQL is authoritative.
CREATE TABLE IF NOT EXISTS sync_operations (
  operation_id UUID PRIMARY KEY,
  client_id UUID NOT NULL,
  user_id UUID,
  entity_type TEXT NOT NULL,
  entity_id UUID NOT NULL,
  operation_type TEXT NOT NULL,
  payload JSONB NOT NULL,
  payload_hash TEXT NOT NULL,
  status TEXT NOT NULL CHECK (status IN ('accepted', 'rejected', 'applied')),
  rejection_code TEXT,
  rejection_detail TEXT,
  server_result JSONB,
  received_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  applied_at TIMESTAMPTZ
);
CREATE INDEX IF NOT EXISTS ix_sync_operations_client_received_at ON sync_operations (client_id, received_at DESC);
CREATE INDEX IF NOT EXISTS ix_sync_operations_entity ON sync_operations (entity_type, entity_id);
CREATE TABLE IF NOT EXISTS entity_versions (
  entity_type TEXT NOT NULL,
  entity_id UUID NOT NULL,
  version BIGINT NOT NULL DEFAULT 0,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  PRIMARY KEY (entity_type, entity_id)
);
