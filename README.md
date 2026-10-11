# TaskManagement

A simple, clean task management backend API built with ASP.NET Core following Onion Architecture.

> Hiring technical task. Scope is intentionally limited - no MediatR, no CQRS, no generic repository
> over-abstraction.

## Architecture

One solution, four projects under `src/`. Dependencies point inward.

```
TaskManagement.Api  (controllers, Swagger, middleware)
        |
        v
TaskManagement.Infrastructure  (EF Core, repositories, JWT, hashing, seeding)
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

Entities are never returned from the API; only DTOs.

## Stack

- .NET 10 / ASP.NET Core Web API (controllers)
- EF Core + SQL Server (Microsoft.EntityFrameworkCore.SqlServer)
- Redis (StackExchange.Redis) for cache-aside reads of single tasks
- A `BackgroundService` that consumes an in-process channel
- BCrypt.Net-Next for password hashing
- FluentValidation
- Swashbuckle (Swagger)

## Getting started

### 1. Database

Pick one of two options.

**Option A - Docker SQL Server (recommended)**

```bash
docker compose up -d
```

This starts SQL Server 2022 on `localhost,1433` and Redis on `localhost:6379`.
The SA password is read from the `SA_PASSWORD` environment variable, defaulting
to `TaskManager@2026`, and the Redis host port from `REDIS_PORT`, defaulting to
`6379`. Copy `.env.example` to `.env` to override either:

```bash
cp .env.example .env
```

The default connection string in `appsettings.json` targets this container. If
you change `SA_PASSWORD` in `.env`, set the same password for the API through
the `ConnectionStrings__DefaultConnection` environment variable (the Docker
default in `appsettings.json` uses `TaskManager@2026`):

```bash
ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=TaskManagement;User Id=sa;Password=<your-password>;TrustServerCertificate=True"
```

**Option B - Existing local SQL Server**

`appsettings.json` ships a connection string for the Docker SQL Server. To use a
local SQL Server instead, copy the committed example to `appsettings.Local.json`
and adjust it if needed:

```bash
cp src/TaskManagement.Api/appsettings.Local.example.json src/TaskManagement.Api/appsettings.Local.json
```

The example uses Windows authentication against the local default instance:

```
Server=.;Database=TaskManagement;Trusted_Connection=true;TrustServerCertificate=true
```

`appsettings.Local.json` is git-ignored and layered over `appsettings.json` and
`appsettings.{Environment}.json` (but under environment variables), so it is used
by both `dotnet run` and the design-time `dotnet ef` factory. The connection can
also be provided through the `ConnectionStrings__DefaultConnection` environment
variable, which takes precedence over the JSON files.

### 2. Run the API

```bash
dotnet run --project src/TaskManagement.Api
```

Migrations are applied automatically on startup and an admin user is seeded if
missing.

### 3. Seed credentials

| Email               | Password    |
| ------------------- | ----------- |
| `admin@example.com` | `Admin@123` |

### 4. Swagger

Open Swagger UI at `https://localhost:<port>/swagger`.

## Configuration

- `ConnectionStrings:DefaultConnection` - SQL Server connection string. The
  default in `appsettings.json` targets the Docker SQL Server; a local SQL Server
  is configured by copying `appsettings.Local.example.json` to
  `appsettings.Local.json`. The `ConnectionStrings__DefaultConnection`
  environment variable overrides all JSON files, and must be set to the same
  password if `SA_PASSWORD` in `.env` is changed.
- `ConnectionStrings:Redis` - Redis connection string. The default in
  `appsettings.json` is `localhost:6379` (the Docker container). If `REDIS_PORT`
  in `.env` is changed, point the API at the same port through the
  `ConnectionStrings__Redis` environment variable or `appsettings.Local.json`.
  If Redis is unavailable the API logs a warning and falls back to SQL Server.
- `JwtSettings` (key, issuer, audience, expiry) are read from configuration. The
  `JwtSettings:Key` in `appsettings.Development.json` is a **development-only**
  signing key (committed so the project runs out of the box). It must never be
  used in production: provide the key through the `JwtSettings__Key` environment
  variable (or a secret store) instead.
- CORS allowed origins come from `Cors:AllowedOrigins` (default
  `http://localhost:5173`).

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
| GET    | `/api/users`     | Admin | List all users (id, name, email, role, createdAt)       |
| POST   | `/api/users`     | Admin | Create a user (409 if the email is already taken)       |
| DELETE | `/api/users/{id}`| Admin | Delete a user (404 if missing, 403 when deleting self)  |

### Tasks (owner scoped)

| Method | Route                      | Auth | Description                                                        |
| ------ | -------------------------- | ---- | ----------------------------------------------------------------- |
| POST   | `/api/tasks`               | User | Create a task for the caller (409 on duplicate title that day)    |
| GET    | `/api/tasks`               | User | List the caller's tasks (priority high→low, then newest first)    |
| GET    | `/api/tasks/{id}`          | User | Get one of the caller's tasks (404 if not owned or missing)       |
| PATCH  | `/api/tasks/{id}/status`   | User | Update status of the caller's task (409 on concurrent update)     |

Tasks always belong to the authenticated user (the `sub` claim). Requesting
another user's task returns `404` rather than `403`, so task ids are not
disclosed.

## Caching

`GET /api/tasks/{id}` uses a cache-aside strategy. The first read loads the task
from SQL Server and stores it in Redis under `task:{userId}:{taskId}` with a
5-minute TTL (logged at `Debug` as a cache miss). Subsequent reads are served
from Redis (logged as a cache hit). The entry is evicted when the status is
changed through the API or by the background worker, so reads never return a
stale status. If Redis is unreachable, a warning is logged and every read falls
back to SQL Server.

## Background task processing

Creating a task writes its id to an in-process `Channel<Guid>` (through
`ITaskQueue`). A `BackgroundService` (`TaskProcessingWorker`) reads the channel
with a new DI scope per job, waits 3-5 seconds, and moves the task from
`Pending` to `InProgress`, but only when it is still `Pending`. It then evicts
the task's cache entry. If the task was changed concurrently
(`DbUpdateConcurrencyException`) it is reloaded and skipped. Each job runs in a
try/catch so a single failure never stops the worker, and the loop honors the
host's shutdown token.

## Assumptions

- Duplicate titles are scoped per user, per calendar day (UTC), and compared
  case-insensitively after trimming. A unique index on
  `(UserId, NormalizedTitle, CreatedDate)` enforces this in the database; the
  2601/2627 SQL errors are mapped to `409`.
- The background worker lives in the same process as the API and the queue is
  in-memory, so pending jobs are lost if the process restarts. A durable broker
  (e.g. a Redis list or a real message queue) would be the next step.
- Admins create accounts but cannot delete their own account; there is no
  endpoint to change roles because it was not part of the scope.
- `RowVersion` is an EF concurrency token; concurrent status updates surface as
  `409 Conflict` (`ConcurrencyConflict`).

## Error handling

Application services return `Result<T>` with error types (Validation, Unauthorized,
Forbidden, NotFound, Conflict). The API maps these to RFC 7807 ProblemDetails with
the correct status codes (400, 401, 403, 404, 409). Expected errors never surface
as 500.
