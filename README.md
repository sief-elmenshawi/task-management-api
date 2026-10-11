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
- Redis (StackExchange.Redis) - used on day 2
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

This starts SQL Server 2022 on `localhost,1433` (and Redis). The SA password is
read from the `SA_PASSWORD` environment variable, defaulting to
`TaskManager@2026`. Copy `.env.example` to `.env` to override it:

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
- `JwtSettings` (key, issuer, audience, expiry) are read from configuration. The
  `JwtSettings:Key` in `appsettings.Development.json` is a **development-only**
  signing key (committed so the project runs out of the box). It must never be
  used in production: provide the key through the `JwtSettings__Key` environment
  variable (or a secret store) instead.
- CORS allowed origins come from `Cors:AllowedOrigins` (default
  `http://localhost:5173`).

## Authentication endpoints

| Method | Route               | Description                              |
| ------ | ------------------- | ---------------------------------------- |
| POST   | `/api/auth/register`| Register a new user                      |
| POST   | `/api/auth/login`   | Login, returns access + refresh token    |
| POST   | `/api/auth/refresh` | Rotate refresh token (reuse detection)   |
| POST   | `/api/auth/logout`  | Revoke a refresh token                    |
| GET    | `/api/auth/me`      | Current user (requires Bearer token)     |

## Error handling

Application services return `Result<T>` with error types (Validation, Unauthorized,
Forbidden, NotFound, Conflict). The API maps these to RFC 7807 ProblemDetails with
the correct status codes (400, 401, 403, 404, 409). Expected errors never surface
as 500.
