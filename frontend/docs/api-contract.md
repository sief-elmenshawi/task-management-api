# API contract

Derived from the backend source (`src/TaskManagement.Api`, `src/TaskManagement.Application`),
not guessed. Base URL for the Docker stack: `http://localhost:8080`. CORS allows the frontend
dev origin `http://localhost:5173`.

## Conventions

- JSON only, **camelCase** property names (ASP.NET Core default).
- Enums are strings in and out:
  - `priority`: `Low` | `Medium` | `High`
  - `status`: `Pending` | `InProgress` | `Done`
  - `role`: `User` | `Admin`
- `DateTime` values are ISO 8601 UTC strings, e.g. `"2026-10-15T09:30:00Z"`.
- Protected endpoints require `Authorization: Bearer <accessToken>`.
- Every error is `application/problem+json` (RFC 7807):

```json
{
  "type": "https://httpstatuses.io/400",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "Password must be at least 8 characters long.",
  "code": "ValidationFailed",
  "errors": { "Password": ["Password must be at least 8 characters long."] }
}
```

- `code` is a stable machine-readable string; `detail` is the human-readable message.
- `errors` appears only on validation failures (400). **Key casing depends on the source**:
  - FluentValidation field rules → PascalCase C# property names: `Name`, `Email`, `Password`,
    `ConfirmPassword`, `Title`, `Description`, `Priority`, `Status`.
  - Model binding (malformed JSON, unknown enum value, non-GUID `id`) → camelCase:
    `priority`, `status`, `id`, or the camelCased field name; anything unrecognized is
    collected under `request`.
  - Match keys to form fields **case-insensitively** (the frontend `mapFieldErrors` helper).
- `401` responses carry `WWW-Authenticate: Bearer` (with `error="invalid_token"` when the token
  itself is invalid or expired); `403` is returned for a valid token without the required role.

## Error codes

| HTTP | `code`                    | Meaning                                                             |
| ---- | ------------------------- | ------------------------------------------------------------------- |
| 400  | `ValidationFailed`        | FluentValidation or model-binding failure (body has `errors`)        |
| 400  | `RefreshTokenRequired`    | `refresh` / `logout` with a blank refresh token                      |
| 401  | `Unauthorized`            | Missing, invalid or expired access token                             |
| 401  | `InvalidCredentials`      | Login with unknown email or wrong password                           |
| 401  | `InvalidRefreshToken`     | Unknown, rotated-beyond-grace or concurrently consumed refresh token |
| 401  | `RefreshTokenExpired`     | Refresh token older than 7 days                                      |
| 401  | `RefreshTokenReuseDetected` | Rotated token replayed after the grace window (all sessions revoked) |
| 403  | `Forbidden`               | Valid token without the required role                                |
| 403  | `CannotDeleteSelf`        | Admin tried to delete their own account                              |
| 404  | `NotFound`                | Generic not found                                                    |
| 404  | `TaskNotFound`            | Task missing, or owned by another user                               |
| 404  | `UserNotFound`            | User missing (also `GET /api/auth/me` for a deleted account)         |
| 409  | `Conflict`                | Generic conflict                                                     |
| 409  | `EmailAlreadyExists`      | Register / admin create with a taken email                           |
| 409  | `TaskTitleDuplicate`      | Same normalized title, same user, same UTC day                       |
| 409  | `ConcurrencyConflict`     | Task row changed by another operation (`rowversion`)                 |
| 500  | `InternalServerError`     | Unhandled exception; the message is always generic                   |
| 429  | `TooManyRequests`         | Auth rate limit exceeded                                             |

## Rate limiting

`POST /api/auth/register`, `POST /api/auth/login` and `POST /api/auth/refresh` share a fixed-window
limit per client IP: **10 requests per 60 seconds** (configurable). Over the limit:

```
HTTP/1.1 429 Too Many Requests
Retry-After: 60
Content-Type: application/problem+json

{ "title": "Too many requests.", "detail": "Too many requests. Please try again later.", "code": "TooManyRequests" }
```

## Auth model

- **Access token** - JWT, valid **15 minutes** (`JwtSettings:ExpiryMinutes`). Stateless.
- **Refresh token** - opaque 128-hex-char value, valid **7 days**, **single use**: every
  successful `refresh` revokes the presented token and issues a replacement (only its SHA-256
  hash is stored server-side).
- **Reuse grace** - a rotated token presented again within **30 s** (`RefreshToken:ReuseGraceSeconds`)
  is rejected with `401 InvalidRefreshToken` but not treated as theft; after that window it is treated
  as theft: all of the user's sessions are revoked and the response is `401 RefreshTokenReuseDetected`.
- **Concurrent refreshes** - concurrent refreshes with the same token yield exactly one success; the
  others get `401 InvalidRefreshToken` (the token row is guarded by a `rowversion`).
- **Logout** revokes only the presented refresh token (one session) and is idempotent: a rotated or
  already-revoked token returns `204 No Content`.
- **Seeded admin**: `admin@example.com` / `Admin@123` (role `Admin`).

## Shared shapes

```jsonc
// AuthResponse (login / refresh)
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "9F2C...128 hex chars...",
  "expiresAt": "2026-10-15T09:45:00Z"
}
```

```jsonc
// UserResponse (register, /auth/me)
{ "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6", "name": "Administrator", "email": "admin@example.com", "role": "Admin" }
```

```jsonc
// TaskResponse
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Write the report",
  "description": null,
  "status": "Pending",
  "priority": "High",
  "createdAt": "2026-10-15T09:30:00Z"
}
```

```jsonc
// UserAdminResponse (users API)
{ "id": "3fa85f64-...", "name": "Alice", "email": "alice@example.com", "role": "User", "createdAt": "2026-10-15T09:30:00Z" }
```

## Endpoints

### Auth

| Method | Path                  | Auth        | Rate limited |
| ------ | --------------------- | ----------- | ------------ |
| POST   | `/api/auth/register`  | anonymous   | yes          |
| POST   | `/api/auth/login`     | anonymous   | yes          |
| POST   | `/api/auth/refresh`   | anonymous   | yes          |
| POST   | `/api/auth/logout`    | anonymous   | no           |
| GET    | `/api/auth/me`        | Bearer      | no           |

#### `POST /api/auth/register`

```json
{ "name": "Alice", "email": "alice@example.com", "password": "Secret123", "confirmPassword": "Secret123" }
```

| Field            | Rules                                                                        |
| ---------------- | ---------------------------------------------------------------------------- |
| `name`           | required, max 100 characters                                                  |
| `email`          | required, valid format, max 256 characters (normalized: trimmed + lowercased) |
| `password`       | required, 8-72 characters, at most 72 UTF-8 bytes, at least one letter and one digit |
| `confirmPassword`| required, must equal `password`                                              |

- `201 Created` → `UserResponse`
- `400 ValidationFailed` (`errors` keys: `Name`, `Email`, `Password`, `ConfirmPassword`)
- `409 EmailAlreadyExists`
- `429 TooManyRequests`

#### `POST /api/auth/login`

```json
{ "email": "admin@example.com", "password": "Admin@123" }
```

Validation: `email` required + format; `password` required + at most 72 UTF-8 bytes.

- `200 OK` → `AuthResponse`
- `400 ValidationFailed`
- `401 InvalidCredentials` (same message for unknown email and wrong password)
- `429 TooManyRequests`

#### `POST /api/auth/refresh`

```json
{ "refreshToken": "<refreshToken from AuthResponse>" }
```

- `200 OK` → `AuthResponse` (both tokens rotated)
- `400 RefreshTokenRequired`
- `401 InvalidRefreshToken` | `401 RefreshTokenExpired` | `401 RefreshTokenReuseDetected`
- `429 TooManyRequests`

Concurrent refreshes with the same token yield exactly one `200`; the others get
`401 InvalidRefreshToken`.

#### `POST /api/auth/logout`

```json
{ "refreshToken": "<refreshToken>" }
```

- `204 No Content` (also when the token was already revoked)
- `400 RefreshTokenRequired`
- `401 InvalidRefreshToken`

#### `GET /api/auth/me`

- `200 OK` → `UserResponse`
- `401 Unauthorized`
- `404 UserNotFound`

### Tasks (scoped to the caller)

| Method | Path                    | Auth   |
| ------ | ----------------------- | ------ |
| GET    | `/api/tasks`            | Bearer |
| POST   | `/api/tasks`            | Bearer |
| GET    | `/api/tasks/{id}`       | Bearer |
| PATCH  | `/api/tasks/{id}/status`| Bearer |

Tasks never cross users: another user's task id answers `404`, never `403`.

#### `GET /api/tasks`

- `200 OK` → `TaskResponse[]`, ordered by priority (`High` → `Medium` → `Low`), then `createdAt`
  descending.

#### `POST /api/tasks`

```json
{ "title": "Write the report", "description": "Quarterly numbers", "priority": "High" }
```

| Field         | Rules                                       |
| ------------- | ------------------------------------------- |
| `title`       | required, max 200 characters                 |
| `description` | optional, max 1000 characters                |
| `priority`    | required, `Low` \| `Medium` \| `High`       |

- `201 Created` → `TaskResponse` + `Location: /api/tasks/{id}`
- `400 ValidationFailed` (`errors` keys: `Title`, `Description`, `Priority`)
- `401 Unauthorized`
- `409 TaskTitleDuplicate` - same normalized title on the same UTC day

#### `GET /api/tasks/{id}`

- `200 OK` → `TaskResponse` (cache-aside in Redis, 5-minute TTL; a just-updated status may be
  served stale for a moment)
- `400 ValidationFailed` (`errors` key: `id`) - id is not a GUID
- `401 Unauthorized`
- `404 TaskNotFound`

#### `PATCH /api/tasks/{id}/status`

```json
{ "status": "InProgress" }
```

`status` is required and must be `Pending`, `InProgress` or `Done`.

- `200 OK` → `TaskResponse` with the new status
- `400 ValidationFailed` (`errors` keys: `status` or `Status`, plus `id` for a bad GUID)
- `401 Unauthorized`
- `404 TaskNotFound`
- `409 ConcurrencyConflict` - the task changed since it was read; reload and retry

### Users (Admin role only)

| Method | Path               | Auth         |
| ------ | ------------------ | ------------ |
| GET    | `/api/users`       | Bearer+Admin |
| POST   | `/api/users`       | Bearer+Admin |
| DELETE | `/api/users/{id}`  | Bearer+Admin |

#### `GET /api/users`

- `200 OK` → `UserAdminResponse[]` (active users only; users are soft-deleted)
- `401 Unauthorized` | `403 Forbidden`

#### `POST /api/users`

Same body and validation as `POST /api/auth/register`.

- `201 Created` → `UserAdminResponse` (role is always `User`)
- `400 ValidationFailed` | `401 Unauthorized` | `403 Forbidden` | `409 EmailAlreadyExists`

#### `DELETE /api/users/{id}`

Revokes all of the user's refresh tokens and frees the email for reuse.

- `204 No Content`
- `400 ValidationFailed` (`id` is not a GUID)
- `401 Unauthorized`
- `403 CannotDeleteSelf`
- `404 UserNotFound`

### Health (for monitoring, not the SPA)

| Method | Path             | Behaviour                                             |
| ------ | ---------------- | ----------------------------------------------------- |
| GET    | `/health/live`   | `200` while the process runs, no dependency checks     |
| GET    | `/health/ready`  | `200` when SQL Server is reachable; Redis outage is `Degraded`, SQL outage is `503` |
