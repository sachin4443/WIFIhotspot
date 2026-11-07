# NXT Hotspot – Admin Dashboard (Frontend)

React 19 · Vite 8 · TypeScript 5.9 · Bootstrap 5.3 · React Router 8 · Axios · React Icons

## Requirements

- Node.js **20.19+** or **22.12+** (`node -v`)
- The NXT Hotspot API running locally (see `Documentation/Windows-Setup-Phase2.md`)

## Run locally (Windows)

1. Start the API from Visual Studio or with:
   ```powershell
   dotnet run --project Backend\NxtHotspot.API --launch-profile NxtHotspot.API
   ```
   It listens on `https://localhost:57314` (`Backend/NxtHotspot.API/Properties/launchSettings.json`).
2. Start the dashboard:
   ```powershell
   cd Frontend
   npm install
   npm run dev
   ```
3. Open http://localhost:5173 and sign in with your Super Admin account.

In development the browser only talks to the Vite server. Vite proxies `/api/*` and `/health`
to the API, so no CORS configuration is needed and the self-signed ASP.NET Core development
certificate is accepted by the proxy.

## Configuration

Copy `.env.example` to `.env.local` (git-ignored) only if you need to change a default.

| Variable | Default | Purpose |
|---|---|---|
| `VITE_API_BASE_URL` | empty | API origin used by the browser. Empty = same origin (the dev proxy). Set it for production builds, e.g. `https://api.example.com`. |
| `VITE_API_PROXY_TARGET` | `https://localhost:57314` | Where the dev server proxies `/api` and `/health`. Development only. |

`VITE_*` values are bundled into the browser: never put secrets in them.
When `VITE_API_BASE_URL` points at another origin, add the dashboard origin to the API's
`Cors:AllowedOrigins`.

## Scripts

| Command | What it does |
|---|---|
| `npm run dev` | Dev server with hot reload on port 5173 |
| `npm run build` | Type-check (`tsc -b`) and production build into `dist/` |
| `npm run typecheck` | Type-check only |
| `npm run preview` | Serve the production build locally |

## Authentication

- `POST /api/auth/login` returns a JWT (30 minutes, no refresh token).
- The session is kept in `sessionStorage`: it survives a reload and is cleared when the tab closes.
- On reload the token is re-validated with `GET /api/auth/me`; permissions come from the server.
- The session ends automatically at token expiry (with a 2-minute warning), and any `401`
  from the API signs the user out with a "session expired" notice.
- Menu items are shown only when the user holds a matching permission.

## Structure

```
src/
  api/          Axios client, error mapping, endpoint wrappers
  auth/         Auth context/provider, token storage, route guards
  components/
    common/     Brand mark, loading screen, page header
    layout/     App shell, sidebar, top bar, navigation config
  hooks/        Shared hooks
  pages/        Routed pages
  styles/       Theme on top of Bootstrap
  toast/        Toast notifications (Bootstrap markup, no Bootstrap JS)
  types/        API contracts mirrored from the backend DTOs
  utils/        Formatting helpers
```
