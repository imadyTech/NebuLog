# NebuLog dashboard

React + TypeScript + Vite. `npm run dev` proxies `/api`, `/health` and `/hubs` to the
backend (`NEBULOG_BACKEND`, default `http://localhost:5080`). `npm run build` writes the
bundle into `src/NebuLog.Server.Host/wwwroot`, which the host serves on the same origin.
