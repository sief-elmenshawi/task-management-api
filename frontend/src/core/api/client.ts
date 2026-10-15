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

type UnauthorizedHandler = () => void

let unauthorizedHandler: UnauthorizedHandler | null = null

export function setUnauthorizedHandler(handler: UnauthorizedHandler | null): void {
  unauthorizedHandler = handler
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, auth = true, skipUnauthorizedHandler = false, signal } = options

  const headers = new Headers({ Accept: 'application/json' })

  if (body !== undefined) {
    headers.set('Content-Type', 'application/json')
  }

  if (auth) {
    const token = tokenStorage.getAccessToken()
    if (token !== null) {
      headers.set('Authorization', `Bearer ${token}`)
    }
  }

  let response: Response

  try {
    response = await fetch(`${apiUrl}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
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

  if (response.status === 401 && !skipUnauthorizedHandler && unauthorizedHandler !== null) {
    unauthorizedHandler()
  }

  throw new ApiError({
    status: response.status,
    code: problem.code,
    message: errorMessage(response.status, problem.message, retryAfterSeconds),
    fieldErrors: problem.fieldErrors,
    retryAfterSeconds,
  })
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
