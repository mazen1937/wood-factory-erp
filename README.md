# Wood Factory ERP — Foundation Scaffold

This is an **initial foundation**, not a complete ERP. It contains a runnable NestJS health endpoint, local PostgreSQL, a minimal Avalonia shell, and contracts for offline-first synchronization.

## Architecture

- **Desktop:** Avalonia on .NET 8; Arabic-first and English-capable.
- **Authority:** NestJS API and PostgreSQL. Validation and conflict decisions belong to the server.
- **Offline:** SQLite cache plus append-only outbox. Each operation has a stable UUID idempotency key.
- **Development:** Docker Compose provides PostgreSQL.

## Run locally

1. Copy `.env.example` to `.env`.
2. Run `docker compose up -d postgres`.
3. In `apps/api`, run `npm install` and `npm run start:dev`.
4. Open `http://localhost:3000/api/health`.
5. In `apps/desktop/WoodFactoryErp.Desktop`, run `dotnet restore` and `dotnet run`.

The first product increment should add identity and one server-validated master-data workflow. See `docs/` for the roadmap and sync rules.
