# TaskManagement

A simple, clean task management backend API built with ASP.NET Core following Onion Architecture.



## Architecture

One solution, four projects under `src/` plus a unit test project under `tests/`. Dependencies point
inward.

```
TaskManagement.Api  (controllers, Swagger, middleware)
        |
        v
TaskManagement.Infrastructure  (EF Core, repositories, JWT, hashing, caching, queue, worker, seeding)
        |
        v
TaskManagement.Application  (services, DTOs, validators, Result<T>, abstractions)
        |
        v
TaskManagement.Domain  (entities, enums, repository interfaces, domain exceptions)
```

- `TaskManagement.Domain` - no dependencies.
- `TaskManagement.Application` - depends on Domain.
- `TaskManagement.Infrastructure` - depends on Application + Domain.
- `TaskManagement.Api` - depends on Application + Infrastructure (Infrastructure only referenced from `Program.cs` for DI).
- `TaskManagement.UnitTests` - xUnit tests for the domain, application services, the EF Core query
  behaviour (task list ordering and ownership, soft delete) and the worker.

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

## Quick start (Docker, whole stack)

Everything - SQL Server, Redis and the API - runs from Compose.

```bash
cp .env.example .env
docker compose up --build
```

Then open:

- Swagger UI: <http://localhost:8080/swagger>
- API base: <http://localhost:8080>

`docker compose` builds the API image from `src/TaskManagement.Api/Dockerfile`, waits for SQL Server
and Redis to become healthy, applies migrations (with retry) and seeds the admin user. The API listens
on port `8080` inside the container; the host port comes from `API_PORT` in `.env` (default `8080`).

`.env` must define `JWT_KEY`; Compose fails fast if it is missing. Copy `.env.example` to get all of
the required values with working local defaults.

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

## Endpoints

Enum values (`status`, `priority`, `role`) are accepted and returned as names
(e.g. `Pending`, `High`, `Admin`).

### Authentication

| Method | Route               | Auth | Description                              |
| ------ | ------------------- | ---- | ---------------------------------------- |
| POST   | `/api/auth/register`| -    | Register a new user                      |
| POST   | `/api/auth/login`   | -    | Login, returns access + refresh token    |
| POST   | `/api/auth/refresh` | -    | Rotate refresh token (reuse detection)   |
| POST   | `/api/auth/logout`  | -    | Revoke a refresh token                   |
| GET    | `/api/auth/me`      | User | Current user                             |

### Users (Admin only)

| Method | Route            | Auth  | Description                                             |
| ------ | ---------------- | ----- | ------------------------------------------------------- |
| GET    | `/api/users`     | Admin | List active users (id, name, email, role, createdAt)    |
| POST   | `/api/users`     | Admin | Create a user (409 if the email is already taken)       |
| DELETE | `/api/users/{id}`| Admin | Delete a user (404 if missing, 403 when deleting self)  |

Users are soft-deleted: `DELETE` marks the account as deleted, revokes all of its active refresh
tokens and frees the email for reuse. Deleted users are excluded from listings and can no longer log
in or refresh. A filtered unique index (`Where [IsDeleted] = 0`) keeps emails unique among active
users only.

### Tasks (owner scoped)

| Method | Route                      | Auth | Description                                                        |
| ------ | -------------------------- | ---- | ----------------------------------------------------------------- |
| POST   | `/api/tasks`               | User | Create a task for the caller (409 on duplicate title that day)    |
| GET    | `/api/tasks`               | User | List the caller's tasks (priority high→low, then newest first)    |
| GET    | `/api/tasks/{id}`          | User | Get one of the caller's tasks (404 if not owned or missing)       |
| PATCH  | `/api/tasks/{id}/status`   | User | Update status of the caller's task (409 on concurrent update)     |

Tasks always belong to the authenticated user (the `sub` claim). Requesting another user's task
returns `404` rather than `403`, so task ids are not disclosed.

## Caching

`GET /api/tasks/{id}` uses a cache-aside strategy. The first read loads the task from SQL Server and
stores it in Redis under `task:{userId}:{taskId}` with a 5-minute TTL (logged at `Debug` as a cache
miss). Subsequent reads are served from Redis (logged as a cache hit). The entry is evicted when the
status is changed through the API or by the background worker, so reads never return a stale status.
If Redis is unreachable, a warning is logged and every read falls back to SQL Server.

## Background task processing

Creating a task writes its id to an in-process `Channel<Guid>` (through `ITaskQueue`). A
`BackgroundService` (`TaskProcessingWorker`) reads the channel with a new DI scope per job, waits
3-5 seconds, and moves the task from `Pending` to `InProgress`, but only when it is still `Pending`.
It then evicts the task's cache entry. If the task was changed concurrently
(`DbUpdateConcurrencyException`) it is reloaded and skipped. Each job runs in a try/catch so a single
failure never stops the worker, and the loop honors the host's shutdown token.

**Startup recovery.** When the worker starts it also queries the database for tasks still in
`Pending` and re-enqueues them, so work that was queued but never processed (for example because the
process stopped during the 3-5 second delay) resumes automatically. Recovery runs concurrently with
the read loop and logs the number of recovered tasks. This makes the in-process queue resilient to
restarts as long as the task was persisted first.

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
- Missing or invalid credentials on protected endpoints return `401` as ProblemDetails, and a valid
  token without the required role returns `403` as ProblemDetails (JWT bearer challenge/forbidden
  handlers) - never an empty body.
- Unhandled exceptions pass through `ExceptionHandlingMiddleware`, which maps domain exceptions to
  `4xx` and everything else to a generic `500` ProblemDetails (`code: InternalServerError`) without
  leaking internal details. If the response has already started it logs and does not attempt to
  rewrite it.

## Tests

Unit tests live in `tests/TaskManagement.UnitTests` and cover the domain entities, the application
services (auth, tasks, users), the EF Core query behaviour (task list ordering and ownership,
soft-delete query filters and login for deleted users) and the background worker.

```bash
dotnet test
```

They use NSubstitute for collaborators and FluentAssertions for assertions, and require no external
services.

## Assumptions and known limitations

- Duplicate titles are scoped per user, per calendar day (UTC), and compared case-insensitively after
  trimming. A unique index on `(UserId, NormalizedTitle, CreatedDate)` enforces this in the database;
  the 2601/2627 SQL errors are mapped to `409`.
- The background worker lives in the same process as the API and its queue is in-memory. Pending
  tasks are recovered from the database on startup, but a task whose `POST` enqueue never persisted
  (i.e. the process died between commit and enqueue) would need a durable broker such as a Redis list
  or a real message queue.
- Admins create and delete accounts but cannot delete their own account; there is no endpoint to
  change roles because it was not part of the scope.
- `RowVersion` is an EF concurrency token; concurrent status updates surface as `409 Conflict`
  (`ConcurrencyConflict`).
- Soft-deleted users are retained in the database for auditing; there is no hard-delete or restore
  endpoint.

## AI tools used

Used OpenCode to generate code from detailed prompts I wrote (architecture, requirements,
conventions). I reviewed the code, ran the tests and verified the behavior manually.
