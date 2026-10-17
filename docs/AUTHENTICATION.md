# Authentication foundation

The API owns authentication and role authorization. It provides `POST /api/auth/login` and bearer-protected `GET /api/auth/me`. Passwords are stored using scrypt hashes. The local bootstrap account exists only when both `BOOTSTRAP_ADMIN_USERNAME` and `BOOTSTRAP_ADMIN_PASSWORD` are present in the uncommitted `.env`; it is created once and never updated automatically.

Before first use, apply `database/postgres/migrations/002_identity.sql` to the development database. Never use the example bootstrap password or development token secret outside a local machine.
