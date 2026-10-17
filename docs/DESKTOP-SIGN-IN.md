# Desktop sign in

The desktop sign-in screen calls `POST /api/auth/login` at `http://localhost:3000/api/`. It retains the returned access token in memory only for this initial vertical slice. Do not persist a password. Secure operating-system-backed session storage and automatic session restoration are later work.

Run the API first, then run the desktop project. Sign in with the private bootstrap account from `.env`.
