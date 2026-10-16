# Task Management frontend

React + TypeScript single-page app for the TaskManagement API. It talks to the API over `fetch`, keeps
the token pair in `localStorage`, renews the access token silently on a `401`, and renders the whole
product: sign in/register, the owner-scoped task list with live status polling, task creation and
detail views, and the admin-only user management screens.

The API address comes from `VITE_API_URL`; nothing in the code hardcodes it. The endpoints, payloads,
validation limits and error shapes this app consumes are documented in
[`docs/api-contract.md`](docs/api-contract.md) (derived from the backend source, not guessed).

> Demo videos for the whole project (backend and full stack) are linked at the top of the
> [root README](../README.md#demo-videos).

## Stack

- React 19 + TypeScript (strict) + Vite 8
- React Router 7 (`createBrowserRouter`)
- Plain CSS with design tokens (no CSS framework)
- Vitest 5 + Testing Library + jsdom for tests
- ESLint 10 (flat config, `typescript-eslint` + `react-hooks` + `react-refresh`)

## Prerequisites

- **Node.js 22.12+** (Node 24 LTS recommended - the same major the Docker image uses). Vite 8 requires
  `^20.19.0 || >=22.12.0` and Vitest 5 requires `^22.12.0 || ^24.0.0`, so Node 20 is not enough.
- The **API running** and reachable at `VITE_API_URL` (default `http://localhost:8080`), either through
  Docker Compose or `dotnet run` (see the [root README](../README.md)).

## Run the dev server

With the API already running:

```bash
cd frontend
cp .env.example .env      # VITE_API_URL=http://localhost:8080
npm ci
npm run dev               # http://localhost:5173
```

The dev server is pinned to port `5173` (`server.strictPort` in `vite.config.ts`), which matches the
API's default `CORS_ORIGIN`. Because the port is strict, a busy `5173` makes Vite exit with
`EADDRINUSE` instead of silently moving to another port.

> Tip: the Vite dev server and the Compose `frontend` container both want port `5173`. If the Docker
> stack is up, stop the container first with `docker compose stop frontend`, otherwise `npm run dev`
> fails with `EADDRINUSE`. If you would rather keep the container running, give it a different host
> port with `FRONTEND_PORT` (see the [root README](../README.md#quick-start-docker-whole-stack)) and
> leave `5173` to the dev server.

## Build and preview

```bash
npm run build     # tsc -b (type-check) then vite build into dist/
npm run preview   # serve the production build locally
```

The build requires `VITE_API_URL` to be set, because
[`src/core/config/env.ts`](src/core/config/env.ts) throws `Missing VITE_API_URL` at import time when it
is missing or blank. The value is baked into the bundle at build time (there is no runtime config
endpoint), so changing the API URL requires rebuilding.

## Environment variables

Each app ships a committed `.env.example`; copy it to `.env` (git-ignored) and adjust.

`frontend/.env.example` (local dev server):

| Variable       | Default                 | Purpose                                        |
| -------------- | ----------------------- | ---------------------------------------------- |
| `VITE_API_URL` | `http://localhost:8080` | Base URL of the API the browser calls.         |

- Only `VITE_`-prefixed variables are exposed to the bundle (Vite rule). `VITE_API_URL` is the only one.
- The trailing slash is trimmed, so `http://localhost:8080/` and `http://localhost:8080` are equal.
- In Docker the value is passed as a **build arg** (`VITE_API_URL`) to the image, not as a runtime
  variable - see [Docker](#docker).

## Commands

| Command           | What it does                                      |
| ----------------- | ------------------------------------------------- |
| `npm run dev`     | Vite dev server on <http://localhost:5173>        |
| `npm run build`   | Type-check with `tsc -b`, then build into `dist/` |
| `npm run lint`    | ESLint over the project                           |
| `npm test`        | Run the Vitest test suite once                    |
| `npm run preview` | Serve the production build locally                |

## Routes and screens

| Path                | Screen                       | Access          |
| ------------------- | ---------------------------- | --------------- |
| `/`                 | Redirect to `/tasks`         | public          |
| `/login`            | `LoginPage`                  | public          |
| `/register`         | `RegisterPage`               | public          |
| `/tasks`            | `TasksPage`                  | authenticated   |
| `/tasks/new`        | `CreateTaskPage`             | authenticated   |
| `/tasks/:id`        | `TaskDetailsPage`            | authenticated   |
| `/admin/users`      | `UsersPage`                  | Admin           |
| `/admin/users/new`  | `CreateUserPage`             | Admin           |
| anything else       | `NotFoundPage` ("Page not found") | public     |

A logged-in user who opens `/login` or `/register` is redirected to `/tasks`.

## Project structure

```text
frontend/src/
  app/      app shell, router, layout, header
  core/     api (fetch client + single-flight refresh), config (env), storage (token pair), session
  shared/   account, components, forms, hooks, pages, styles, utils (reusable, no feature code)
  features/
    auth/   AuthProvider, guards (ProtectedRoute, RoleRoute), login/register pages, validation
    tasks/  api, hooks (useTasks, useTask), pages, components (badges), draft, filters, logout cleanup
    users/  api, hook (useUsers), pages, components (RoleBadge), validation
  test/     Vitest setup
```

Rules (enforced by review):

- Dependencies point inward: `pages -> components -> hooks -> api -> core`.
- `shared` never imports from `features`.
- Components never call `fetch` directly - all requests go through the API client in `core/api`.
- The API base URL is read only from `VITE_API_URL`.
- Each feature owns its own API calls, hooks and pages; the `tasks` and `users` features build on the
  `auth` feature's context and `UserRole` type rather than duplicating auth logic.

## How it works

### App shell and routing

[`main.tsx`](src/main.tsx) registers the task-draft logout cleanup once, then mounts `App` inside
`StrictMode`. `App` renders a `RouterProvider`. The router
([`src/app/router.tsx`](src/app/router.tsx)) mounts `RootLayout`, which wraps `AuthProvider` around
`Layout` (header + `<Outlet/>`).

### Authentication and session lifecycle

`AuthProvider` ([`src/features/auth/AuthProvider.tsx`](src/features/auth/AuthProvider.tsx)) owns the
session:

- **State** - `user`, `status` (`loading` | `authenticated` | `unauthenticated` | `error`) and the
  actions `login`, `register`, `logout`, `retrySession`.
- **Storage** - the token pair lives in `localStorage` via
  [`core/storage/tokenStorage.ts`](src/core/storage/tokenStorage.ts) under `taskmanager.accessToken`,
  `taskmanager.refreshToken` and `taskmanager.expiresAt`.
- **Startup** - if a stored access token exists, it calls `GET /api/auth/me` to restore the profile. A
  definitive `401` clears the session; a recoverable failure (network/`5xx`/`429`) keeps the tokens and
  sets `status: 'error'` so `ProtectedRoute` shows a **retry screen** instead of logging the user out.
- **Login** - `POST /api/auth/login`, store the tokens, then `GET /api/auth/me` to populate the user.
- **Logout** - fires `POST /api/auth/logout` (fire-and-forget) to revoke the refresh token, runs the
  logout cleanups, navigates to `/login`, and clears the session.
- **Session generation** - [`core/session/session.ts`](src/core/session/session.ts) keeps a counter
  that is bumped on every login/logout. A refresh or startup check that started under an older
  generation is dropped, so a late response can never restore a logged-out session or overwrite a newer
  login.
- **Login redirect** - [`features/auth/loginRedirect.ts`](src/features/auth/loginRedirect.ts) carries a
  small `{ sessionExpired }` flag to `/login`, so an expired session shows the info notice
  "Your session expired. Please sign in again." while an explicit logout does not.

### API client and silent token refresh

[`core/api/client.ts`](src/core/api/client.ts) is the single place that talks to the API:

- Adds `Authorization: Bearer <accessToken>` (from `tokenStorage`) to protected requests and
  `Content-Type: application/json` when there is a body.
- Parses `application/problem+json` errors into an [`ApiError`](src/core/api/ApiError.ts) with
  `status`, `code`, `message`, `fieldErrors` and `retryAfterSeconds`. A transport failure becomes
  `ApiError` with `status: 0`, `code: NetworkError`.
- On a protected `401` it performs **one silent refresh** (`POST /api/auth/refresh`) with
  **single-flight** semantics: concurrent `401`s share a single refresh instead of firing duplicates
  (which the API's rotation/reuse detection would treat as an attack). The original request is then
  retried once.
- It **only logs out when the refresh token is rejected** (`400`/`401`). A recoverable refresh failure
  (network/`5xx`/`429`) keeps the session and surfaces the original error instead.
- **Multi-tab safety**: if the refresh is rejected because another tab already rotated the token, the
  tab notices newer tokens in `localStorage` and retries with them, so two tabs do not sign each other
  out.
- A `429` is turned into a readable message using the `Retry-After` header.

### Error handling and form validation

- [`shared/forms/formErrors.ts`](src/shared/forms/formErrors.ts) `describeApiError(error, fields)` maps
  `error.fieldErrors` onto the given field names (case-insensitively, via
  [`mapFieldErrors`](src/shared/forms/mapFieldErrors.ts)) and otherwise returns the error message as a
  form-level error.
- [`shared/account/formErrors.ts`](src/shared/account/formErrors.ts) adds one special case:
  `EmailAlreadyExists` (`409`) is shown on the `Email` field rather than as a form error.
- Validation is client-side and mirrors the API limits:
  [`shared/account/validation.ts`](src/shared/account/validation.ts) is shared by **register** and
  **create user**; [`features/tasks/validation.ts`](src/features/tasks/validation.ts) validates title,
  description and priority. Every form is `noValidate` and renders field errors through `FormField`
  (which wires `aria-invalid` and `aria-describedby`).

### Tasks feature

- **List** (`TasksPage` + [`useTasks`](src/features/tasks/hooks/useTasks.ts)) loads `GET /api/tasks`
  (already ordered by priority high→low, then newest first). It polls every `2000ms` **only while at
  least one task is `Pending`** and the tab is visible
  ([`usePolling`](src/shared/hooks/usePolling.ts)); a background poll is silent, so a dropped poll keeps
  the last rendered data instead of flashing an error. A pending task shows a "Processing..." spinner
  next to its status.
- **Filters and search** ([`filters.ts`](src/features/tasks/filters.ts)): All / Pending / In Progress /
  Done segmented control with live counts, plus a debounced (`250ms`) title search.
- **Create** (`CreateTaskPage`): validates, `POST /api/tasks`, clears the draft and returns to the list.
  A `409 TaskTitleDuplicate` shows "You already created a task with this title today." The typed values
  are kept after a server error.
- **Draft** ([`draft.ts`](src/features/tasks/draft.ts)): what you type is written to `sessionStorage`
  under `taskmanager.createTaskDraft.{userId}` and restored on mount. It is cleared once the task is
  created **and on an explicit logout** (registered as a logout cleanup in
  [`logoutCleanup.ts`](src/features/tasks/logoutCleanup.ts)), but kept when the session expires - so a
  silent logout never costs the user their typing, and two users on a shared browser never see each
  other's drafts.
- **Details** (`TaskDetailsPage` + [`useTask`](src/features/tasks/hooks/useTask.ts)): `GET
  /api/tasks/{id}`; a `404` renders "Task not found". Changing the status via `PATCH
  /api/tasks/{id}/status` applies the returned task to local state **immediately** (so the new status
  is visible even if the follow-up refresh fails), then refreshes silently. The page keeps polling
  while the task is `Pending`.

### Users feature (Admin only)

- **List** (`UsersPage` + [`useUsers`](src/features/users/hooks/useUsers.ts)) loads `GET /api/users`.
- **Create** (`CreateUserPage`): same form and validation as register; the created role is always
  `User`. On success it returns to the list with a "The user was created." notice.
- **Delete**: a `ConfirmDialog` asks first; the admin's own row has its Delete button disabled, and the
  handler ignores repeated entry while a delete is in flight, so a double click sends a **single**
  `DELETE`. Errors are shown inline.

### Access control

- [`ProtectedRoute`](src/features/auth/components/ProtectedRoute.tsx) renders a spinner while the
  session is loading, the retry `ErrorState` on `status: 'error'`, and redirects to `/login` (remembering
  where the user was going) when unauthenticated.
- [`RoleRoute`](src/features/auth/components/RoleRoute.tsx) renders `AccessDeniedPage` ("Access denied")
  when the user's role does not match - e.g. a normal user opening `/admin/users`. The Users nav link
  is hidden for non-admins.

### UX details

- Submit buttons disable and show a spinner while a request is running (`Button` `loading`), with
  `aria-busy`.
- Alerts use `role="alert"` for errors and `role="status"` for info/success (`Alert`); empty lists use
  `EmptyState`; unrecoverable load failures use `ErrorState` with a **Retry** button.
- The document title updates per screen via `useDocumentTitle` (e.g. "My tasks · Task Management").
- Dates render through [`formatDate`](src/shared/utils/formatDate.ts) (`en-GB`, e.g. "15 Oct 2026").
- Long values such as emails wrap in the users table below `640px`, so a `375px` viewport never scrolls
  horizontally.

## Testing

Vitest + Testing Library in a `jsdom` environment. The setup
([`src/test/setup.ts`](src/test/setup.ts)) loads `@testing-library/jest-dom` and runs `cleanup` after
each test. The API URL is injected through `test.env` in `vite.config.ts`, and tests mock `fetch`
rather than hitting a server.

```bash
npm test      # vitest run
npm run lint  # eslint .
```

Coverage includes the API client's refresh/retry logic, the auth session lifecycle (login, logout,
failed-refresh logout, manual logout redirect, guards), the tasks hooks and pages (list, create,
details, polling, draft), the users page, and the shared validation/error-mapping helpers.

## Docker

[`frontend/Dockerfile`](Dockerfile) is a two-stage build:

1. `node:24-slim` runs `npm ci` and `npm run build`, taking `VITE_API_URL` as a build arg (default
   `http://localhost:8080`) and baking it into the bundle.
2. `nginx:alpine` serves `dist/` with [`nginx.conf`](nginx.conf), which falls back to `index.html` for
   unknown paths (SPA routing). nginx listens on container port `80`; Compose maps it to the host via
   `FRONTEND_PORT` (default `5173`).

The image serves static files only - it does not proxy the API - so the API must be reachable from the
browser and its CORS origin must match the origin the SPA is served from.

## CORS

The API allows the SPA origin through CORS (`CORS_ORIGIN`, default `http://localhost:5173`) and exposes
the `Retry-After` and `Location` response headers, which the client reads for `429` messages and for
the `201 Created` `Location`. If you serve the SPA from a different origin (for example a non-default
`FRONTEND_PORT`), set `CORS_ORIGIN` to that same origin (see the [root README](../README.md)).

## Enhancements

- **Per-user draft protection:** the create-task form writes what you type to `sessionStorage` under a
  key scoped to the signed-in user and restores it on mount; it is cleared once the task is created or
  on an explicit logout, but kept when the session expires. Why: a silent logout should never cost the
  user their typing, and two users on a shared browser must not see each other's drafts.
- **No wasted requests:** the task list polls only while a task is `Pending` and the tab is visible,
  the title search is debounced, and every effect aborts its fetch and clears its timer on unmount. A
  failed background poll keeps the last rendered data instead of flashing an error. Why: React
  StrictMode double-invokes effects, so cleanup keeps the double mount from leaving duplicate polling
  timers or stale responses behind, and a dropped poll should not replace good data with an error
  screen.
- **Clear feedback:** submit buttons disable and show a spinner while a request is in flight, the typed
  values stay after a server error, and `429` responses show the readable `Retry-After` wait time.
  Why: the user always knows a request is running and, when throttled, exactly how long to wait.
- **Silent token refresh:** on a `401` the API client calls `POST /api/auth/refresh` once with
  single-flight semantics and retries the original request. It only logs out when the refresh token is
  rejected (`400`/`401`); a network or `5xx`/`429` failure keeps the session. Why: an expired access
  token is renewed without interrupting the user, while a transient server problem never forces a
  re-login.
- **Resilient startup:** if the boot-time `GET /api/auth/me` cannot reach the server, or its silent
  refresh fails with a recoverable error (network/`5xx`/`429`), the app keeps the stored tokens and
  shows a retry screen; only a definitive `401` clears the session. Why: a brief outage at startup
  should not log the user out.
- **Refresh cannot undo a logout or overwrite a newer login:** a session generation counter in
  `core/session` drops a refresh that started before a logout or a newer login, so a pending refresh
  can no longer restore the token pair after logout or clobber a fresh session. Why: a deliberate
  logout or a new login must always win over an older in-flight refresh.
- **Multi-tab refresh:** when a tab's refresh is rejected because another tab already rotated the
  token, the tab retries with the tokens the other tab stored instead of logging out. Why: two tabs
  sharing one session must not sign each other out.
- **Status changes apply immediately:** a successful `PATCH /status` is applied to local state from the
  response before the follow-up refresh, and a stale in-flight read cannot roll it back. Why: the new
  status must be visible even if the refresh that follows it fails.
- **One delete per confirmation:** the dialog disables both actions while a delete is in flight and the
  handler ignores repeated entry, so a double click sends a single `DELETE`. Why: a duplicate request
  returned `404` and showed a failure after a successful deletion.
- **No mobile overflow:** long values such as email addresses wrap in the users table below `640px`.
  Why: the table used to scroll horizontally on a 375px viewport.

## Known limitations

- `VITE_API_URL` is baked into the bundle at build time, so changing the API URL requires rebuilding
  the frontend image (there is no runtime-config endpoint).
- The frontend image serves static files only; it does not proxy the API, so the API must be reachable
  from the browser and its CORS origin must match.
- There is no automated browser end-to-end suite (Playwright/Cypress) in the repository; the frontend
  flows are covered by component/unit tests with a mocked `fetch`, not driven through a real browser.

## AI tools used

See [AI tools used](../README.md#ai-tools-used) in the root README.
