# Test strategy

- API: health, authorization, validation, idempotency, integration tests.
- Desktop: view models and SQLite repository tests.
- Contract: sync-replay and response compatibility tests.
- End-to-end: Docker-backed workflow for each vertical slice.

Never rely only on UI validation for a production business action.
