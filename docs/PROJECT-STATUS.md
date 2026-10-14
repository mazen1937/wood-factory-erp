# Project status — 2026-10-14

## Milestone reached

The Wood Factory ERP foundation is pushed to GitHub and the local development setup has been tested successfully.

## Confirmed working locally

- Docker PostgreSQL was started with `docker compose up -d postgres`.
- The NestJS API dependencies installed and the API starts in watch mode with `npm.cmd run start:dev` from `apps/api`.
- The Avalonia desktop shell builds and opens from `apps/desktop/WoodFactoryErp.Desktop` with `dotnet run`.
- The desktop window displays its Arabic-first starter interface.

## Setup adjustments made after the initial scaffold

- In `apps/api/package.json`, `@nestjs/cli` was changed from the unavailable `12.1.1` to `12.0.5`.
- `class-validator` and `class-transformer` were installed for NestJS `ValidationPipe`.
- In `apps/desktop/WoodFactoryErp.Desktop/Program.cs`, the unavailable `.WithInterFont()` call was removed.
- In `apps/desktop/WoodFactoryErp.Desktop/MainWindow.axaml.cs`, `InitializeComponent()` was replaced with `AvaloniaXamlLoader.Load(this)`.
- These changes were committed and pushed to the repository.

## Important operating notes

- In PowerShell, use `npm.cmd` rather than `npm`; PowerShell blocks the `npm.ps1` script under the current execution policy.
- Do not run `npm audit fix --force` as part of setup.
- `.env` stays local and should not be committed.
- PostgreSQL is authoritative. The desktop uses SQLite only as local cache/outbox; sync operations must be idempotent and server-validated.

## Next implementation target

Build the first vertical slice: authentication, users, and roles.

Suggested order:

1. PostgreSQL schema/migration for users and roles.
2. NestJS authentication endpoints with password hashing, JWT/session strategy, and server-side role authorization.
3. Arabic-first Avalonia sign-in screen.
4. Local session handling and protected API calls.
5. Tests for authentication and authorization.

## Repository location

`C:\Users\HP\source\wood-factory-erp`
