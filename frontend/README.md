# Task Management frontend

React + TypeScript single-page app for the TaskManagement API. The API address
comes from `VITE_API_URL`; nothing in the code hardcodes it.

## Run

```bash
npm install
cp .env.example .env
npm run dev
```

Open <http://localhost:5173> (the dev server is pinned to that port; the API
only allows that origin in CORS).

## Commands

| Command            | What it does                                    |
| ------------------ | ----------------------------------------------- |
| `npm run dev`      | Vite dev server on <http://localhost:5173>      |
| `npm run build`    | Type-check with `tsc -b`, then build into `dist/` |
| `npm run lint`     | ESLint over the project                          |
| `npm test`         | Run the Vitest test suite once                   |
| `npm run preview`  | Serve the production build locally               |

## API contract

The endpoints, payloads, validation limits and error shapes this app consumes
are documented in [`docs/api-contract.md`](docs/api-contract.md).
