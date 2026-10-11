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
- EF Core + PostgreSQL (Npgsql)
- Redis (StackExchange.Redis) - used on day 2
- BCrypt.Net-Next for password hashing
- FluentValidation
- Swashbuckle (Swagger)

## Getting started

1. Start local dependencies:

   ```bash
   docker compose up -d
   ```

2. Apply migrations and run the API:

   ```bash
   dotnet run --project src/TaskManagement.Api
   ```

   Migrations are applied automatically on startup and an admin user is seeded
   if missing (`admin@example.com` / `Admin@123`).

3. Open Swagger at `https://localhost:<port>/swagger`.

## Configuration

- `JwtSettings` (key, issuer, audience, expiry) are read from configuration.
- No secrets are hardcoded. A development-only key lives in
  `appsettings.Development.json`.
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
