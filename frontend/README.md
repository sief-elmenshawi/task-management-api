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

## Enhancements

- **Draft protection:** the create-task form writes what you type to
  `sessionStorage` and restores it on mount, so a session that expires while the
  form is open does not lose the draft; the draft is cleared once the task is
  created. Why: a silent logout should never cost the user their typing.
- **No wasted requests:** the task list polls only while a task is `Pending`
  and the tab is visible, the title search is debounced, and every effect
  aborts its fetch and clears its timer on unmount. Why: React StrictMode
  double-invokes effects, so cleanup keeps the double mount from leaving
  duplicate polling timers or stale responses behind.
- **Clear feedback:** submit buttons disable and show a spinner while a request
  is in flight, the typed values stay after a server error, and `429` responses
  show the readable `Retry-After` wait time (the API now exposes that header
  through CORS). Why: the user always knows a request is running and, when
  throttled, exactly how long to wait.
- **Silent token refresh:** on a `401` the API client calls
  `POST /api/auth/refresh` once with single-flight semantics and retries the
  original request. Why: an expired access token is renewed without
  interrupting the user, while concurrent requests share one refresh call and a
  failed refresh logs out with the session-expired message.
