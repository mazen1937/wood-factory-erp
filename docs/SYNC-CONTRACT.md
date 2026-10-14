# Offline synchronization contract

## Request
Each logical write has one immutable UUID `operationId`:

```json
{"operationId":"UUID","clientId":"UUID","entityType":"item","entityId":"UUID","operationType":"item.upsert","expectedVersion":4,"payload":{}}
```

Commit the local data change and outbox entry together in SQLite. On retry, resend the same operation ID.

## Server rules
The API authenticates and authorizes the request; then in one PostgreSQL transaction it checks idempotency, validates the DTO/business rules, checks expected version, makes the write, and stores a response. Duplicate IDs return the original stored result. Rejected operations are terminal and return a safe code for the UI.

There is no generic last-write-wins rule. Inventory, costing, and finance must be append-only domain transactions, not overwritten cache rows.
