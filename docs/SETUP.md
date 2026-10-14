# Setup

## Prerequisites
Git, Docker Desktop, Node.js 22+, and the .NET 8 SDK.

## Database
Copy `.env.example` to `.env`, then run:

```powershell
docker compose up -d postgres
```

The SQL migration auto-initializes only a new local Docker volume. Do not delete volumes as a normal startup step.

## API
```powershell
Set-Location apps/api
npm install
npm run start:dev
```
Check `GET http://localhost:3000/api/health`.

## Desktop
```powershell
Set-Location apps/desktop/WoodFactoryErp.Desktop
dotnet restore
dotnet run
```

The package versions are pinned to versions verified when this archive was assembled. `npm install` creates the team's lockfile.
