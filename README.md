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

The default connection string in `appsettings.json` targets this container.

**Option B - Existing local SQL Server**

Set `ConnectionStrings:DefaultConnection` in `appsettings.Development.json`. The
default there uses Windows authentication against the local default instance:

```
Server=.;Database=TaskManagement;Trusted_Connection=true;TrustServerCertificate=true
```

`appsettings.Development.json` is layered on top of `appsettings.json`, so when
the environment is `Development` (the default for `dotnet run` and for the
design-time `dotnet ef` factory) the local instance is used instead of Docker.
The connection can also come from the `ConnectionStrings__DefaultConnection`
environment variable.

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

- `ConnectionStrings:DefaultConnection` - SQL Server connection string. Docker
  default lives in `appsettings.json`; local override in
  `appsettings.Development.json`.
- `JwtSettings` (key, issuer, audience, expiry) are read from configuration. A
  development-only key lives in `appsettings.Development.json`.
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
