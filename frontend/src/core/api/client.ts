import { apiUrl } from '../config/env'
import { tokenStorage } from '../storage/tokenStorage'
import { ApiError } from './ApiError'

export type RequestOptions = {
  method?: 'GET' | 'POST' | 'PATCH' | 'DELETE'
  body?: unknown
  auth?: boolean
  skipUnauthorizedHandler?: boolean
  signal?: AbortSignal
}

type Problem = {
  code: string
  message: string
  fieldErrors: Record<string, string[]>
}

type RefreshResponse = {
  accessToken: string
  refreshToken: string
  expiresAt: string
}

type UnauthorizedHandler = () => void

let unauthorizedHandler: UnauthorizedHandler | null = null
let refreshInFlight: Promise<void> | null = null

export function setUnauthorizedHandler(handler: UnauthorizedHandler | null): void {
  unauthorizedHandler = handler
}

export function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  return send<T>(path, options, true)
}

export const api = {
  get<T>(path: string, options: Omit<RequestOptions, 'method' | 'body'> = {}): Promise<T> {
    return request<T>(path, { ...options, method: 'GET' })
  },

  post<T>(path: string, body: unknown, options: Omit<RequestOptions, 'method' | 'body'> = {}): Promise<T> {
    return request<T>(path, { ...options, method: 'POST', body })
  },

  patch<T>(path: string, body: unknown, options: Omit<RequestOptions, 'method' | 'body'> = {}): Promise<T> {
    return request<T>(path, { ...options, method: 'PATCH', body })
  },

  delete<T>(path: string, options: Omit<RequestOptions, 'method' | 'body'> = {}): Promise<T> {
    return request<T>(path, { ...options, method: 'DELETE' })
  },
}

async function send<T>(path: string, options: RequestOptions, canRetry: boolean): Promise<T> {
  const method = options.method ?? 'GET'
  const auth = options.auth ?? true
  const skipUnauthorizedHandler = options.skipUnauthorizedHandler ?? false

  const usedAccessToken = auth ? tokenStorage.getAccessToken() : null

  const headers: Record<string, string> = { Accept: 'application/json' }
  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }
  if (auth && usedAccessToken !== null) {
    headers.Authorization = `Bearer ${usedAccessToken}`
  }

  let response: Response

  try {
    response = await fetch(`${apiUrl}${path}`, {
      method,
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      signal: options.signal,
    })
  } catch {
    throw new ApiError({ status: 0, code: 'NetworkError', message: 'Cannot reach the server' })
  }

  if (response.ok) {
    if (response.status === 204) {
      return undefined as T
    }

    const text = await response.text()
    if (text === '') {
      return undefined as T
    }

    return JSON.parse(text) as T
  }

  const retryAfterSeconds = parseRetryAfter(response.headers.get('Retry-After'))
  const problem = await readProblem(response)

  const apiError = new ApiError({
    status: response.status,
    code: problem.code,
    message: errorMessage(response.status, problem.message, retryAfterSeconds),
    fieldErrors: problem.fieldErrors,
    retryAfterSeconds,
  })

  if (response.status === 401 && auth && !skipUnauthorizedHandler) {
    if (canRetry) {
      const outcome = await tryRefreshSession(usedAccessToken)

      if (outcome.kind === 'refreshed') {
        return await send<T>(path, options, false)
      }

      if (outcome.kind === 'temporary') {
        throw apiError
      }

      const currentAccessToken = tokenStorage.getAccessToken()
      const currentRefreshToken = tokenStorage.getRefreshToken()
      const hasNewerTokens =
        (currentAccessToken !== null && currentAccessToken !== usedAccessToken) ||
        (currentRefreshToken !== null && currentRefreshToken !== outcome.usedRefreshToken)

      if (hasNewerTokens) {
        return await send<T>(path, options, false)
      }
    }

    if (unauthorizedHandler !== null) {
      unauthorizedHandler()
    }
  }

  throw apiError
}

type RefreshOutcome =
  | { kind: 'refreshed' }
  | { kind: 'temporary' }
  | { kind: 'rejected'; usedRefreshToken: string | null }

async function tryRefreshSession(failedAccessToken: string | null): Promise<RefreshOutcome> {
  const usedRefreshTokenForRefresh = tokenStorage.getRefreshToken()

  try {
    await refreshSession(failedAccessToken)
    return { kind: 'refreshed' }
  } catch (error) {
    if (error instanceof ApiError && (error.status === 400 || error.status === 401)) {
      return { kind: 'rejected', usedRefreshToken: usedRefreshTokenForRefresh }
    }

    return { kind: 'temporary' }
  }
}

async function refreshSession(failedAccessToken: string | null): Promise<void> {
  const currentAccessToken = tokenStorage.getAccessToken()
  if (currentAccessToken !== null && currentAccessToken !== failedAccessToken) {
    return
  }

  if (refreshInFlight === null) {
    refreshInFlight = performRefresh().finally(() => {
      refreshInFlight = null
    })
  }

  await refreshInFlight
}

async function performRefresh(): Promise<void> {
  const refreshToken = tokenStorage.getRefreshToken()
  if (refreshToken === null) {
    throw new ApiError({ status: 401, code: 'NoRefreshToken', message: 'Your session has expired.' })
  }

  const tokens = await send<RefreshResponse>(
    '/api/auth/refresh',
    {
      method: 'POST',
      body: { refreshToken },
      auth: false,
      skipUnauthorizedHandler: true,
    },
    false,
  )

  tokenStorage.set(tokens)
}

async function readProblem(response: Response): Promise<Problem> {
  const fallback: Problem = {
    code: `HTTP_${response.status}`,
    message: response.statusText || `Request failed with status ${response.status}.`,
    fieldErrors: {},
  }

  const contentType = response.headers.get('Content-Type') ?? ''
  if (!contentType.includes('application/problem+json')) {
    return fallback
  }

  let body: unknown
  try {
    body = await response.json()
  } catch {
    return fallback
  }

  if (!isRecord(body)) {
    return fallback
  }

  return {
    code: typeof body.code === 'string' ? body.code : fallback.code,
    message:
      typeof body.detail === 'string'
        ? body.detail
        : typeof body.title === 'string'
          ? body.title
          : fallback.message,
    fieldErrors: isFieldErrors(body.errors) ? body.errors : {},
  }
}

function errorMessage(status: number, fallback: string, retryAfterSeconds?: number): string {
  if (status !== 429) {
    return fallback
  }

  if (retryAfterSeconds === undefined) {
    return 'Too many requests. Please try again later.'
  }

  const unit = retryAfterSeconds === 1 ? 'second' : 'seconds'
  return `Too many requests. Please try again in ${retryAfterSeconds} ${unit}.`
}

function parseRetryAfter(value: string | null): number | undefined {
  if (value === null) {
    return undefined
  }

  const seconds = Number(value)
  return Number.isFinite(seconds) && seconds >= 0 ? seconds : undefined
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}

function isFieldErrors(value: unknown): value is Record<string, string[]> {
  if (!isRecord(value)) {
    return false
  }

  return Object.entries(value).every(
    ([, messages]) =>
      Array.isArray(messages) && messages.every((message) => typeof message === 'string'),
  )
}