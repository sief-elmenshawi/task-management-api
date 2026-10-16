# TaskManagement

A full-stack task management solution: an ASP.NET Core API following Onion Architecture (this
readme), plus a React + TypeScript single-page app in [`frontend/`](frontend/README.md).


## Architecture

One solution, four projects under `src/` plus unit and integration test projects under `tests/`.
Dependencies point inward.

```
TaskManagement.Api  (controllers, Swagger, middleware)
  |--> TaskManagement.Application  (services, DTOs, validators, Result<T>, abstractions)
  |          |--> TaskManagement.Domain
  |
  |--> TaskManagement.Infrastructure  (EF Core, repositories, JWT, hashing, caching, queue, worker, seeding)
             |--> TaskManagement.Application
             |--> TaskManagement.Domain

TaskManagement.Domain  -->  (no dependencies)
```

- `TaskManagement.Domain` - no dependencies.
- `TaskManagement.Application` - depends on Domain.
- `TaskManagement.Infrastructure` - depends on Application + Domain.
- `TaskManagement.Api` - depends on Application + Infrastructure (Infrastructure only referenced from `Program.cs` for DI).
- `TaskManagement.UnitTests` - xUnit tests for the domain, application services, the EF Core query
  behaviour (task list ordering and ownership, soft delete) and the worker.
- `TaskManagement.IntegrationTests` - opt-in end-to-end tests that boot the API against real SQL
  Server and Redis containers (Testcontainers).

Entities are never returned from the API; only DTOs.

Persistence detail: `Priority` is stored as an `int` (while `Status` is stored as a `string`), so
`ORDER BY Priority DESC` sorts `High`, `Medium`, `Low` numerically.

## Stack

- .NET 10 / ASP.NET Core Web API (controllers)
- EF Core + SQL Server (Microsoft.EntityFrameworkCore.SqlServer)
- Redis (StackExchange.Redis) for cache-aside reads of single tasks
- A `BackgroundService` that consumes an in-process channel and recovers pending work on startup
- BCrypt.Net-Next for password hashing
- FluentValidation
- Swashbuckle (Swagger)
- xUnit + NSubstitute + FluentAssertions for unit tests

## Prerequisites

- Docker Desktop (for the full-stack option)
- .NET 10 SDK (only for running without Docker or running the tests)
- Node.js 22.12+ (for the frontend dev server/tests and the Postman/Newman run via `npx newman`).
  The frontend toolchain needs a recent Node: Vite 8 requires `^20.19.0 || >=22.12.0` and Vitest 5
  requires `^22.12.0 || ^24.0.0`, so Node 20 is not enough. Node 24 LTS is what the Docker image uses.

## Quick start (Docker, whole stack)

Everything - SQL Server, Redis, the API and the frontend - runs from Compose. This is the easiest way
for a reviewer to run the whole product: one command, with no local .NET or Node install.

```bash
cp .env.example .env
docker compose up --build
```

Then open:

- Frontend SPA: <http://localhost:5173>
- Swagger UI: <http://localhost:8080/swagger>
- API base: <http://localhost:8080>

`docker compose` builds the API image from `src/TaskManagement.Api/Dockerfile` and the frontend image
from `frontend/Dockerfile`, waits for SQL Server and Redis to become healthy, applies migrations (with
retry) and seeds the admin user. The API listens on port `8080` inside the container (host port from
`API_PORT`, default `8080`); the frontend's nginx serves the built SPA on container port `80` with an
SPA fallback to `index.html` (host port from `FRONTEND_PORT`, default `5173`). The frontend is built
with `VITE_API_URL` (default `http://localhost:8080`) baked in, because the browser calls the API on
the host - not over the Compose network.

If a host port is already taken, override it for one command only, e.g.
`FRONTEND_PORT=5273 docker compose up --build` (PowerShell:
`$env:FRONTEND_PORT = "5273"; docker compose up --build`); when you change `FRONTEND_PORT`, set
`CORS_ORIGIN` to the same origin so the browser is allowed to call the API.

`.env` must define `JWT_KEY`; Compose fails fast if it is missing. Copy `.env.example` to get all of
the required values with working local defaults.

A `429 Too Many Requests` response means the auth rate limit was reached - wait 60 seconds (the
default window) before retrying. The committed `.env.example` ships a demo value
(`RATE_LIMIT_AUTH_PERMIT=1000`) so the Postman/Newman run is not throttled, while the code default
is `10` requests per `60` seconds - the value to use in production. See
[Rate limiting](#rate-limiting) and [Postman / Newman](#postman--newman).

## Frontend (React SPA)

The SPA lives in [`frontend/`](frontend/README.md): React 19 + TypeScript (strict) + Vite, React
Router and plain CSS. It talks to the API over `fetch` and keeps the token pair in `localStorage`.

### Run the frontend dev server

With the API already running (Docker or [locally](#running-the-api-locally-deps-in-docker)):

```bash
cd frontend
cp .env.example .env      # VITE_API_URL=http://localhost:8080
npm ci
npm run dev               # http://localhost:5173
```

The dev server is pinned to port `5173`, which matches the API's default `CORS_ORIGIN`.

> Tip: the Vite dev server and the Compose `frontend` container both use port `5173`. If the stack is
> up, stop the container first with `docker compose stop frontend`, otherwise `npm run dev` fails with
> `EADDRINUSE`.

### Structure and architecture rules

```
frontend/src/
  app/      app shell, router, layouts, header
  core/     api client (fetch + single-flight token refresh), env config, token storage
  shared/   reusable components, hooks, utils and styles (no feature code)
  features/ auth, tasks, users (each: api, hooks, components, pages)
```

Rules: dependencies point inward (`pages -> components -> hooks -> api -> core`); `shared` never
imports from `features`; components never call `fetch` directly; the API base URL is read only from
`VITE_API_URL`; each feature owns its own types, API calls, hooks and pages.

### Frontend commands

| Command         | What it does                            |
| --------------- | --------------------------------------- |
| `npm run dev`   | Vite dev server on port 5173            |
| `npm run build` | `tsc -b` then `vite build` into `dist/` |
| `npm run lint`  | ESLint                                  |
| `npm test`      | Vitest suite                            |

## Environment variables

Each app ships a committed `.env.example`; copy it to `.env` (git-ignored) and adjust.

Root `.env.example` (used by Compose; the API reads `appsettings.json`, `appsettings.Local.json` and environment variables - not `.env`) - key entries:

| Variable                           | Default                 | Purpose                                                                 |
| ---------------------------------- | ----------------------- | ----------------------------------------------------------------------- |
| `SA_PASSWORD`                      | `TaskManager@2026`      | SQL Server SA password (connection string + the `sqlserver` service).   |
| `API_PORT`                         | `8080`                  | Host port for the API.                                                  |
| `CORS_ORIGIN`                      | `http://localhost:5173` | Allowed browser origin; must match where the SPA is served.             |
| `FRONTEND_PORT`                    | `5173`                  | Host port for the frontend (nginx listens on container port `80`).      |
| `VITE_API_URL`                     | `http://localhost:8080` | API URL baked into the SPA at build time (must be browser-reachable).   |
| `JWT_KEY`                          | (required)              | JWT signing key; Compose fails fast when it is missing.                 |

The worker, rate-limit and refresh-token variables are documented in the file itself and under
[Configuration](#configuration).

`frontend/.env.example` (local dev server only - the Docker image gets `VITE_API_URL` as a build arg):

| Variable       | Default                 | Purpose                                          |
| -------------- | ----------------------- | ------------------------------------------------ |
| `VITE_API_URL` | `http://localhost:8080` | Base URL of the API the browser calls.           |

`CORS_ORIGIN` (API) and the origin serving the SPA must match, or the browser blocks the API calls.

## How to try it in Swagger

1. Log in with the seeded admin account (`admin@example.com` / `Admin@123`).
2. Copy the `accessToken` from the response.
3. Click **Authorize**, paste the token (without the `Bearer ` prefix) and submit.

## Running the API locally (deps in Docker)

Prefer iterating with `dotnet run`? Start just the backing services:

```bash
cp .env.example .env
docker compose up -d sqlserver redis
dotnet run --project src/TaskManagement.Api
```

The default connection strings in `appsettings.json` target the Docker containers on
`localhost,1433` (SQL Server) and `localhost:6379` (Redis), so the API connects with no extra
configuration. If the host ports are taken, change `REDIS_PORT` in `.env` and point the API at the
same port via the `ConnectionStrings__Redis` environment variable or `appsettings.Local.json`.

Migrations are applied automatically on startup and an admin user is seeded if missing.

### Using an existing local SQL Server

`appsettings.json` ships a connection string for the Docker SQL Server. To use a local SQL Server
instead, copy the committed example to `appsettings.Local.json` and adjust it if needed:

```bash
cp src/TaskManagement.Api/appsettings.Local.example.json src/TaskManagement.Api/appsettings.Local.json
```

The example uses Windows authentication against the local default instance:

```
Server=.;Database=TaskManagement;Trusted_Connection=true;TrustServerCertificate=true
```

`appsettings.Local.json` is git-ignored and layered over `appsettings.json` and
`appsettings.{Environment}.json` (but under environment variables), so it is used by both
`dotnet run` and the design-time `dotnet ef` factory. The connection can also be provided through the
`ConnectionStrings__DefaultConnection` environment variable, which takes precedence over the JSON
files.

## Seed credentials

| Email               | Password    |
| ------------------- | ----------- |
| `admin@example.com` | `Admin@123` |

## Configuration

- `ConnectionStrings:DefaultConnection` - SQL Server connection string. The default in
  `appsettings.json` targets the Docker SQL Server; a local SQL Server is configured by copying
  `appsettings.Local.example.json` to `appsettings.Local.json`. The
  `ConnectionStrings__DefaultConnection` environment variable overrides all JSON files, and must be
  set to the same password if `SA_PASSWORD` in `.env` is changed.
- `ConnectionStrings:Redis` - Redis connection string. The default in `appsettings.json` is
  `localhost:6379` (the Docker container). If `REDIS_PORT` in `.env` is changed, point the API at the
  same port through the `ConnectionStrings__Redis` environment variable or `appsettings.Local.json`.
  If Redis is unavailable the API logs a warning and falls back to SQL Server.
- `JwtSettings` (key, issuer, audience, expiry) are read from configuration. The `JwtSettings:Key` in
  `appsettings.Development.json` is a **development-only** signing key (committed so the project runs
  out of the box). In Docker the key comes from `JWT_KEY` in `.env`. It must never be used in
  production: provide the key through the `JwtSettings__Key` environment variable (or a secret store)
  instead.
- `Cors:AllowedOrigins` - allowed origins (default `http://localhost:5173`). The Compose service sets
  the first entry from `CORS_ORIGIN`.
- `Swagger:Enabled` - serves Swagger UI and the `/` → `/swagger` redirect when `true` (default). The
  Compose service sets it to `true`.
- `Worker:Concurrency` - number of background-worker consumers that process queued tasks in parallel
  (default `3`, minimum `1`; validated at startup). The Compose service sets it from
  `WORKER_CONCURRENCY`, so `WORKER_CONCURRENCY=6` in `.env` runs six parallel consumers.
- `Worker:RecoveryIntervalSeconds` - seconds between periodic recovery sweeps (default `60`, minimum
  `1`; validated at startup). Compose reads it from `WORKER_RECOVERY_INTERVAL_SECONDS`.
- `Worker:RecoveryMinAgeSeconds` - a `Pending` task must be at least this old before periodic recovery
  re-enqueues it (default `30`, minimum `0`; validated at startup). This stops the periodic sweep from
  fighting the consumers over tasks that were just created and queued. Compose reads it from
  `WORKER_RECOVERY_MIN_AGE_SECONDS`.
- `RateLimiting:Auth` - fixed-window rate limit for `POST /api/auth/register`, `/api/auth/login` and
  `/api/auth/refresh`, partitioned by client IP address. `PermitLimit` (default `10`) requests are
  allowed per `WindowSeconds` (default `60`); the next request gets `429 Too Many Requests` with a
  `Retry-After` header and an `application/problem+json` body (`code: TooManyRequests`). Compose sets
  it from `RATE_LIMIT_AUTH_PERMIT` and `RATE_LIMIT_AUTH_WINDOW` (or
  `RateLimiting__Auth__PermitLimit`/`RateLimiting__Auth__WindowSeconds` directly).
- `RefreshToken:ReuseGraceSeconds` - window (seconds) in which a just-rotated refresh token is treated
  as invalid (`401 InvalidRefreshToken`) rather than a reuse attack (default `30`). Compose reads it from
  `REFRESH_TOKEN_REUSE_GRACE_SECONDS`; the example `.env` lowers it to `1` so the Postman reuse
  scenario can wait past the window.

## Authentication flow

- **Access token** - a short-lived JWT, valid for `15` minutes (`JwtSettings:ExpiryMinutes`). It is
  self-contained, so the API trusts it until it expires without checking the database per request.
- **Refresh token** - long-lived (`7` days) and single-use. Each `POST /api/auth/refresh` rotates it:
  the presented token is revoked, a replacement is issued, and only a SHA-256 hash of the token is
  stored - the raw value is returned to the client and never persisted.
- **Password policy** - at least `8` characters containing a letter and a digit, and at most `72`
  bytes of UTF-8 (BCrypt only hashes the first `72` bytes, so longer input is rejected rather than
  silently truncated). The same byte limit is enforced on login.
- **Duplicate email** - `POST /api/auth/register` returns `409 EmailAlreadyExists` when the email is
  already taken. The check is backed by a filtered unique index, so a concurrent insert that loses
  the race maps the SQL `2601`/`2627` error to the same `409`.
- **Reuse detection** - a rotated token presented again within a configurable grace window
  (`RefreshToken:ReuseGraceSeconds`, default `30`; the example `.env` sets
  `REFRESH_TOKEN_REUSE_GRACE_SECONDS=1`) is rejected with `401 InvalidRefreshToken` but not treated as
  theft. Once the window has elapsed, presenting the rotated token is treated as theft: all of the
  user's active refresh tokens are revoked and the response is `401 RefreshTokenReuseDetected`.
- **Concurrent refreshes** - a SQL Server `rowversion` on the refresh-token row means concurrent
  refreshes with the same token yield exactly one success; the losing requests get
  `401 InvalidRefreshToken`.
- **Logout** - `POST /api/auth/logout` revokes only the presented refresh token, ending that single
  session; the user's other sessions stay valid.
- **Deleted users** - users are soft-deleted and excluded from lookups, so a deleted account can no
  longer log in or refresh; an access token already issued stays valid until it expires.
- **401 responses** - every authentication challenge includes the `WWW-Authenticate: Bearer` header,
  and when the token itself is invalid or expired it reads `WWW-Authenticate: Bearer error="invalid_token"`
  (verify with `curl -i`).

Known limitation: because the access token is stateless, an access token issued to a user who is then
deleted stays valid until it expires (at most `15` minutes); tokens are not re-checked against the
database on every request.

## Endpoints

Enum values (`status`, `priority`, `role`) are accepted and returned as names
(e.g. `Pending`, `High`, `Admin`).

### Authentication

| Method | Route               | Auth | Description                              |
| ------ | ------------------- | ---- | ---------------------------------------- |
| POST   | `/api/auth/register`| -    | Register a new user (201 Created)        |
| POST   | `/api/auth/login`   | -    | Login, returns access + refresh token    |
| POST   | `/api/auth/refresh` | -    | Rotate refresh token (reuse detection)   |
| POST   | `/api/auth/logout`  | -    | Revoke a refresh token                   |
| GET    | `/api/auth/me`      | User | Current user                             |

Refresh tokens are single-use: each `refresh` rotates the token and issues a replacement. Concurrency
is guarded with a SQL Server `rowversion` on the token row, so concurrent refreshes with the same token
yield exactly one success - the losing requests get `401 InvalidRefreshToken`. Presenting a rotated
token again within `RefreshToken:ReuseGraceSeconds` (default `30`; the Compose service reads
`REFRESH_TOKEN_REUSE_GRACE_SECONDS`) is rejected as invalid rather than treated as an attack. Once that
window has elapsed, reusing a rotated token is treated as theft: all of the user's active refresh
tokens are revoked and the response is `401 RefreshTokenReuseDetected`.

### Users (Admin only)

| Method | Route            | Auth  | Description                                             |
| ------ | ---------------- | ----- | ------------------------------------------------------- |
| GET    | `/api/users`     | Admin | List active users (id, name, email, role, createdAt)    |
| POST   | `/api/users`     | Admin | Create a user (201 Created; 409 if the email is already taken)  |
| DELETE | `/api/users/{id}`| Admin | Delete a user (404 if missing, 403 when deleting self)  |

Users are soft-deleted: `DELETE` marks the account as deleted, revokes all of its active refresh
tokens and frees the email for reuse. Deleted users are excluded from listings and can no longer log
in or refresh. A filtered unique index (`Where [IsDeleted] = 0`) keeps emails unique among active
users only.

### Tasks (owner scoped)

| Method | Route                      | Auth | Description                                                        |
| ------ | -------------------------- | ---- | ----------------------------------------------------------------- |
| POST   | `/api/tasks`               | User | Create a task for the caller (201 Created + Location; 409 on duplicate title that day) |
| GET    | `/api/tasks`               | User | List the caller's tasks (priority high→low, then newest first)    |
| GET    | `/api/tasks/{id}`          | User | Get one of the caller's tasks (404 if not owned or missing)       |
| PATCH  | `/api/tasks/{id}/status`   | User | Update status of the caller's task (409 on concurrent update)     |

Tasks always belong to the authenticated user (the `sub` claim). Requesting another user's task
returns `404` rather than `403`, so task ids are not disclosed.

## Caching

`GET /api/tasks/{id}` uses a cache-aside strategy. The first read loads the task from SQL Server and
stores it in Redis under `task:{userId}:{taskId}` with a 5-minute TTL (logged at `Debug` as a cache
miss). Subsequent reads are served from Redis (logged as a cache hit). Status changes write the
updated task through to Redis and the background worker evicts the entry after it moves a task to
`InProgress`. If Redis is unreachable, a warning is logged and every read falls back to SQL Server.

## Background task processing

Creating a task writes its id to an in-process `Channel<Guid>` (through `ITaskProcessingQueue`). A
`BackgroundService` (`TaskProcessingWorker`) runs `Worker:Concurrency` consumers in parallel (default
`3`, see [Configuration](#configuration)); each consumer pulls from the shared channel independently,
so that many tasks can be processed at once. Each consumer reads the channel with a new DI scope per
job, waits 3-5 seconds, and moves the task from `Pending` to `InProgress`, but only when it is still
`Pending`. It then evicts the task's cache entry. If the task was changed concurrently
(`DbUpdateConcurrencyException`) it is reloaded and skipped. A job that fails with a transient error
is retried up to three times with a short backoff; a task that never reaches `InProgress` stays
`Pending` so recovery can pick it up again. Each job runs in a try/catch so a single failure never
stops a consumer, and each loop honors the host's shutdown token.

**Recovery.** When the worker starts it queries the database for tasks still in `Pending` and
re-enqueues them, so work that was queued but never processed (for example because the process
stopped during the 3-5 second delay, or the enqueue was lost) resumes when the process restarts. While
the worker runs, it repeats the sweep every `Worker:RecoveryIntervalSeconds` (default `60`), but only
re-enqueues `Pending` tasks whose `CreatedAt` is older than `Worker:RecoveryMinAgeSeconds` (default
`30`) and at most `100` per cycle, so the periodic sweep does not re-queue tasks the consumers are
about to process. Enqueueing a task that is already queued is harmless: a consumer only moves a task
that is still `Pending`, so duplicates are ignored. Both recovery sweeps run concurrently with the
read loop and log the number of recovered tasks.

## Health checks

- `GET /health/live` - liveness probe. Always returns `200` while the process is running; it runs no
  dependency checks.
- `GET /health/ready` - readiness probe. Runs the SQL Server and Redis checks: a failure to reach SQL
  Server is `Unhealthy` (`503`), while a Redis failure is reported as `Degraded` so a cache outage
  does not take the API out of rotation (reads fall back to SQL Server). The `api` service
  `healthcheck` in `docker-compose.yml` polls this endpoint.

## Rate limiting

The `auth` rate-limit policy is applied to `POST /api/auth/register`, `POST /api/auth/login` and
`POST /api/auth/refresh`. It is a fixed window partitioned by client IP address: by default `10`
requests are allowed per `60` seconds. When the limit is exceeded the API returns
`429 Too Many Requests` with a `Retry-After` header (the window length, in seconds) and an
`application/problem+json` body (`code: TooManyRequests`). The limits are configurable through
`RateLimiting:Auth:PermitLimit` and `RateLimiting:Auth:WindowSeconds`; Compose forwards
`RATE_LIMIT_AUTH_PERMIT` and `RATE_LIMIT_AUTH_WINDOW` (see [Configuration](#configuration)).

## Logging

Structured logs are emitted through `ILogger`:

- One line per request (`RequestLoggingMiddleware`): method, path, status code and elapsed
  milliseconds.
- Application services log meaningful events (registration, login success/failure, refresh rotation
  and reuse detection, logout, task creation/status changes, duplicate rejection, concurrency
  conflicts). Passwords and tokens are never logged.
- The worker logs startup recovery, each `Pending` → `InProgress` transition and processing failures.
- Unhandled exceptions are logged at `Error`; handled domain errors at `Warning`.

Log levels follow the standard ASP.NET Core configuration (for example `Logging:LogLevel` in
`appsettings.json` or `Logging__LogLevel__Default`). In the container, logs go to stdout and can be
viewed with `docker compose logs -f api`.

## Error handling

All errors are returned as RFC 7807 ProblemDetails.

- Application services return `Result<T>` with error types (Validation, Unauthorized, Forbidden,
  NotFound, Conflict). The API maps these to the correct status codes (400, 401, 403, 404, 409) and
  includes a stable `code` extension plus, for validation failures, an `errors` dictionary.
- Validation failures share one shape with `code: ValidationFailed`: FluentValidation errors raised by
  the application **and** model-binding errors from ASP.NET Core (malformed JSON, an unknown enum
  value for `priority`/`status`, a non-GUID `id`). Binding errors are reshaped by
  `InvalidModelStateResponseFactory` into the same ProblemDetails with a human-readable message per
  field (for example `priority` → "Invalid value. Allowed values: Low, Medium, High.") instead of the
  raw JSON-conversion text.
- Missing or invalid credentials on protected endpoints return `401` as ProblemDetails, and a valid
  token without the required role returns `403` as ProblemDetails (JWT bearer challenge/forbidden
  handlers) - never an empty body.
- Unhandled exceptions pass through `ExceptionHandlingMiddleware`, which maps domain exceptions to
  `4xx` and everything else to a generic `500` ProblemDetails (`code: InternalServerError`) without
  leaking internal details. If the response has already started it logs and does not attempt to
  rewrite it. A request cancelled by the client (the connection closes mid-flight) is answered with
  `499` and no body.

## Tests

Unit tests live in `tests/TaskManagement.UnitTests` and cover the domain entities, the application
services (auth, tasks, users), the EF Core query behaviour (task list ordering and ownership,
soft-delete query filters and login for deleted users) and the background worker.

```bash
dotnet test
```

They use NSubstitute for collaborators and FluentAssertions for assertions, and require no external
services.

> Tip: if the API is running locally via `dotnet run`, the output DLLs are locked and `dotnet build`
> and `dotnet test` fail with file-in-use errors (MSB3021/MSB3026/MSB3027). Stop that process first
> (`dotnet build` against the Docker containers is unaffected).

Integration tests live in `tests/TaskManagement.IntegrationTests` and boot the real API through
`WebApplicationFactory` against throwaway SQL Server and Redis containers (Testcontainers). They are
opt-in so a plain `dotnet test` stays dependency-free; without the flag every integration test reports
as skipped. With Docker running:

```bash
TASKMANAGER_INTEGRATION=1 dotnet test tests/TaskManagement.IntegrationTests
```

```powershell
$env:TASKMANAGER_INTEGRATION = "1"; dotnet test tests/TaskManagement.IntegrationTests
```

## Postman / Newman

The collection and environment live in `postman/`:
`postman/TaskManagement.postman_collection.json` and
`postman/TaskManagement.postman_environment.json`. Run it with `npx newman` (no global install
required). The collection drives more than ten auth calls (`register`, `login`, `refresh`) in well
under a minute, which exceeds the default auth rate limit of `10` per `60s`, so raise the limit and
lower the reuse grace window before starting the stack:

```bash
RATE_LIMIT_AUTH_PERMIT=1000 RATE_LIMIT_AUTH_WINDOW=60 REFRESH_TOKEN_REUSE_GRACE_SECONDS=1 \
  docker compose up -d --build
npx newman run postman/TaskManagement.postman_collection.json \
  -e postman/TaskManagement.postman_environment.json
```

```powershell
$env:RATE_LIMIT_AUTH_PERMIT = "1000"
$env:RATE_LIMIT_AUTH_WINDOW = "60"
$env:REFRESH_TOKEN_REUSE_GRACE_SECONDS = "1"
docker compose up -d --build
npx newman run postman/TaskManagement.postman_collection.json `
  -e postman/TaskManagement.postman_environment.json
Remove-Item Env:RATE_LIMIT_AUTH_PERMIT, Env:RATE_LIMIT_AUTH_WINDOW, Env:REFRESH_TOKEN_REUSE_GRACE_SECONDS
```

`Remove-Item Env:...` clears the overrides for later commands in the same PowerShell session (in bash,
the variables are only set for the one command that follows them).

These overrides are already the committed defaults in `.env.example`, which is the easiest place to
set them. `RATE_LIMIT_AUTH_PERMIT` is the `RateLimiting__Auth__PermitLimit` environment variable,
`RATE_LIMIT_AUTH_WINDOW` is `RateLimiting__Auth__WindowSeconds` and
`REFRESH_TOKEN_REUSE_GRACE_SECONDS` is `RefreshToken__ReuseGraceSeconds`; `docker-compose.yml`
forwards all three to the API (see [Configuration](#configuration)). The lowered grace window lets the
`AUTH-11` refresh-reuse scenario wait past it. Run the stack without the overrides (or set the permit
limit back to `10`) to observe the `429` response.

`postman/TaskManager-Manual.postman_collection.json` is the guided manual collection: it walks the same
endpoints step by step for exploratory testing (open it in Postman and run the requests in order).

## Bonus items implemented

- Redis cache-aside for `GET /api/tasks/{id}`, with write-through on status change (the worker evicts)
  and automatic fallback to SQL Server when Redis is unavailable (see [Caching](#caching)).
- An in-process background worker (`TaskProcessingWorker`) with `Worker:Concurrency` parallel
  consumers (default `3`) that moves `Pending` tasks to `InProgress`, retries transient SQL failures,
  and recovers pending tasks at startup and periodically (both intervals configurable).
- Soft-delete for users: `DELETE` revokes the account's refresh tokens, frees the email for reuse and
  keeps the row for auditing.
- Refresh-token rotation with hash-only storage, reuse detection (configurable grace window) and a
  `rowversion` guard for concurrent refreshes.
- Auth rate limiting that answers `429 Too Many Requests` with a `Retry-After` header.
- Health checks (`/health/live` and `/health/ready`).
- RFC 7807 ProblemDetails for every error, including model-binding failures and `499` for
  client-aborted requests, plus structured per-request logging.

## Known limitations

- The background queue is in-memory and lives in the API process. Pending tasks are recovered from the
  database at startup and then periodically (every `Worker:RecoveryIntervalSeconds`, capped at `100`
  older-than-`Worker:RecoveryMinAgeSeconds` tasks per cycle) while the API runs. A task whose enqueue
  never persisted (the process died between commit and enqueue) stays `Pending` until the next
  recovery sweep, or would need a durable broker such as a Redis list or a real message queue. A
  `Pending` task that permanently fails to process is re-enqueued by each periodic sweep.
- Cache reads may serve a stale status for up to the 5-minute TTL in rare races (a read and a status
  update racing, leaving the older status in Redis) or if a Redis eviction failed while Redis was
  down. Only status updates write or evict the entry; reads never invalidate.
- Access tokens are stateless (JWT), so deleting a user does not invalidate an already-issued access
  token until it expires (at most `15` minutes). A deleted user can therefore still call the API and
  create tasks with a previously issued token, and cached task reads are not re-checked against the
  soft-delete filter.
- `POST /api/auth/logout` revokes only the refresh token that is sent, and is idempotent: an unknown
  token returns `401 InvalidRefreshToken`, while a rotated or already-revoked token (including a
  concurrent logout of the same token) returns `204` without revoking its replacement. Access tokens
  stay valid until they expire.
- Duplicate titles are scoped per user, per calendar day (UTC), and compared case-insensitively after
  trimming. A unique index on `(UserId, NormalizedTitle, CreatedDate)` enforces this in the database;
  the 2601/2627 SQL errors are mapped to `409`.
- The seeded admin password (`Admin@123`) is a documented development default; change it in any real
  deployment.
- Admins create and delete accounts but cannot delete their own account, and there is no endpoint to
  change roles because it was not part of the scope.
- Soft-deleted users are retained in the database for auditing; there is no hard-delete or restore
  endpoint.
- `RowVersion` is an EF concurrency token; concurrent status updates surface as `409 Conflict`
  (`ConcurrencyConflict`).

## Changes since Stage 1

Stage 1 delivered the backend. Two runtime/behaviour changes were made while building the frontend:

- **Concurrent refresh with the same token now lets exactly one request succeed.** Why: a retry layer in
  `UnitOfWork` re-ran a refresh that lost the concurrency race after the winner had already rotated the
  token, so every concurrent refresh with the same token succeeded (5 of 5 in an integration test). The
  retry was removed and rotation now relies on the `rowversion` concurrency token: exactly one request
  succeeds and the others answer `401 InvalidRefreshToken`. Logout is idempotent - revoking an
  already-revoked token is a no-op that still returns `204`.
- **CORS: expose `Retry-After` and `Location`** in the `Default` policy
  (`src/TaskManagement.Api/Program.cs`, `.WithExposedHeaders("Retry-After", "Location")`). Why: without
  this the browser cannot read those response headers cross-origin, so the SPA could not show a
  readable "try again in N seconds" message for `429` (rate limiting) or follow `Location` for
  `201 Created`. The allowed origins are unchanged.

Test- and build-only changes (no runtime behaviour, endpoints, payloads or error shapes changed):

- **Bumped the transitive `System.Security.Cryptography.Pkcs` to `10.0.13`** in
  `TaskManagement.Infrastructure.csproj` to clear the NuGet audit warnings `NU1902`/`NU1903` (the
  version pulled in through `Microsoft.Data.SqlClient` was affected by CVE-2026-84008 and
  CVE-2026-70162). The build is now warning-free.
- **Made the worker startup-recovery unit tests deterministic** (`TaskProcessingWorkerTests`) by
  awaiting a `TaskCompletionSource` signal instead of fixed delays.

## Assumptions

- The browser calls the API directly at `VITE_API_URL` (default `http://localhost:8080`); there is no
  reverse proxy in front of the API, so CORS must allow the SPA origin.
- The SPA is served from `http://localhost:5173` by default (Vite dev server or the nginx container),
  matching the API's default `CORS_ORIGIN`.
- Enums are strings on the wire and errors are RFC 7807 `application/problem+json`; the client is built
  around that contract.
- The Docker stack is for local/demo use: the committed `JWT_KEY`, `SA_PASSWORD` and seeded admin
  credentials are development values, not secrets for a real deployment.
- Tasks are always owner-scoped, including for the Admin role: the Admin manages user accounts (list,
  create, delete) and does not read other users' tasks, following the spec.
- Google Fonts is loaded from the CDN; without network access the SPA still works with a fallback font.

## Enhancements

Frontend additions beyond a bare CRUD UI (each with the reason):

- **Refresh-token flow with single-flight** - a protected `401` triggers one `POST /api/auth/refresh`
  shared by all concurrent `401`s, then the original request is retried once. Why: an expired access
  token is renewed silently, and concurrent requests cannot fire duplicate refreshes (which the API's
  rotation/reuse detection would treat as an attack).
- **Multi-tab refresh** - when a tab's refresh is rejected because another tab already rotated the
  token, the tab retries the original request with the tokens the other tab stored instead of logging
  out. Why: two tabs sharing one session must not sign each other out.
- **`401` handling in one place** - the API client owns auth/session/refresh logic; it logs out only
  when the refresh token is actually rejected (`400`/`401`), and a network/`5xx`/`429` refresh failure
  keeps the session and surfaces the original error. Why: features stay free of auth plumbing, and a
  transient server problem never logs the user out.
- **Resilient session startup** - if the boot-time `GET /api/auth/me` cannot reach the server the app
  keeps the stored tokens and renders a retry screen; only a `401` clears the session. Why: a brief
  outage at startup should not force a re-login.
- **Per-user draft protection** - the create-task form persists to `sessionStorage` under a key scoped
  to the signed-in user and restores on mount (cleared on success). The draft is cleared on an explicit
  logout but kept when the session expires. Why: a silent logout should not lose typing, and two users
  on a shared browser must not see each other's drafts.
- **No wasted requests** - the list polls only while a task is `Pending` and the tab is visible,
  effects abort their fetch on unmount, and search is debounced. A failed background poll keeps the
  last rendered data instead of flashing an error. Why: fewer needless calls, and a dropped poll should
  not replace good data with an error screen.
- **Usability** - submit buttons disable and show a spinner while a request runs, inputs keep their
  values after a server error, and `429` responses show the readable `Retry-After` wait time. Why: the
  user always knows a request is running and, when throttled, exactly how long to wait.

## Unfinished or limitations

- No automated browser end-to-end suite (Playwright/Cypress). The SPA is covered by component/unit
  tests; the token-refresh, logout-on-failed-refresh and draft behaviors are verified with a mocked
  `fetch`, not driven through a real browser.
- `VITE_API_URL` is baked into the bundle at build time, so changing the API URL requires rebuilding
  the frontend image (there is no runtime-config endpoint).
- The frontend image serves static files only; it does not proxy the API, so the API must be reachable
  from the browser and its CORS origin must match.

## AI tools used

Used OpenCode to generate code from detailed prompts I wrote (architecture, requirements,
conventions). I reviewed, ran and tested every change and I can explain every line.


