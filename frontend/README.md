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

- **Per-user draft protection:** the create-task form writes what you type to
  `sessionStorage` under a key scoped to the signed-in user and restores it on
  mount; it is cleared once the task is created or on an explicit logout, but
  kept when the session expires. Why: a silent logout should never cost the user
  their typing, and two users on a shared browser must not see each other's
  drafts.
- **No wasted requests:** the task list polls only while a task is `Pending`
  and the tab is visible, the title search is debounced, and every effect
  aborts its fetch and clears its timer on unmount. A failed background poll
  keeps the last rendered data instead of flashing an error. Why: React
  StrictMode double-invokes effects, so cleanup keeps the double mount from
  leaving duplicate polling timers or stale responses behind, and a dropped poll
  should not replace good data with an error screen.
- **Clear feedback:** submit buttons disable and show a spinner while a request
  is in flight, the typed values stay after a server error, and `429` responses
  show the readable `Retry-After` wait time (the API now exposes that header
  through CORS). Why: the user always knows a request is running and, when
  throttled, exactly how long to wait.
- **Silent token refresh:** on a `401` the API client calls
  `POST /api/auth/refresh` once with single-flight semantics and retries the
  original request. It only logs out when the refresh token is rejected
  (`400`/`401`); a network or `5xx`/`429` failure keeps the session. Why: an
  expired access token is renewed without interrupting the user, while a
  transient server problem never forces a re-login.
- **Resilient startup:** if the boot-time `GET /api/auth/me` cannot reach the
  server, or its silent refresh fails with a recoverable error (network/`5xx`/`429`),
  the app keeps the stored tokens and shows a retry screen; only a definitive `401`
  clears the session. Why: a brief outage at startup should not log the user out.
- **Refresh cannot undo a logout or overwrite a newer login:** a session generation
  counter in `core/session` drops a refresh that started before a logout or a newer
  login, so a pending refresh can no longer restore the token pair after logout or
  clobber a fresh session. Why: a deliberate logout or a new login must always win
  over an older in-flight refresh.
- **Status changes apply immediately:** a successful `PATCH /status` is applied to
  local state from the response before the follow-up refresh, and a stale in-flight
  read cannot roll it back. Why: the new status must be visible even if the refresh
  that follows it fails.
- **One delete per confirmation:** the dialog disables both actions while a delete is
  in flight and the handler ignores repeated entry, so a double click sends a single
  `DELETE`. Why: a duplicate request returned `404` and showed a failure after a
  successful deletion.
- **No mobile overflow:** long values such as email addresses wrap in the users table
  below `640px`. Why: the table used to scroll horizontally on a 375px viewport.

## AI tools used

See [AI tools used](../README.md#ai-tools-used) in the root README.
