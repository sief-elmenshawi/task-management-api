import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from './ApiError'
import { request, setUnauthorizedHandler } from './client'
import { tokenStorage } from '../storage/tokenStorage'

const fetchMock = vi.fn<typeof fetch>()

function problemResponse(
  status: number,
  body: unknown,
  headers: Record<string, string> = {},
): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/problem+json', ...headers },
  })
}

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

async function catchError(promise: Promise<unknown>): Promise<unknown> {
  try {
    await promise
    return null
  } catch (error) {
    return error
  }
}

describe('request', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockReset()
    tokenStorage.clear()
    setUnauthorizedHandler(null)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    setUnauthorizedHandler(null)
  })

  it('parses a problem+json body into an ApiError', async () => {
    fetchMock.mockResolvedValue(
      problemResponse(400, {
        status: 400,
        title: 'One or more validation errors occurred.',
        detail: 'Password must be at least 8 characters long.',
        code: 'ValidationFailed',
        errors: { Password: ['Password must be at least 8 characters long.'] },
      }),
    )

    const error = await catchError(request('/api/auth/register', { method: 'POST', body: {} }))

    expect(error).toBeInstanceOf(ApiError)
    const apiError = error as ApiError
    expect(apiError.status).toBe(400)
    expect(apiError.code).toBe('ValidationFailed')
    expect(apiError.message).toBe('Password must be at least 8 characters long.')
    expect(apiError.fieldErrors).toEqual({
      Password: ['Password must be at least 8 characters long.'],
    })
  })

  it('resolves to undefined for a 204 response', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))

    await expect(request('/api/auth/logout', { method: 'POST', body: {} })).resolves.toBeUndefined()
  })

  it('calls the unauthorized handler once when a request returns 401', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    fetchMock.mockResolvedValue(
      problemResponse(401, {
        status: 401,
        title: 'Unauthorized.',
        detail: 'Authentication is required to access this resource.',
        code: 'Unauthorized',
      }),
    )

    await expect(request('/api/tasks')).rejects.toBeInstanceOf(ApiError)

    expect(handler).toHaveBeenCalledTimes(1)
  })

  it('does not call the unauthorized handler for flagged requests', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    fetchMock.mockResolvedValue(
      problemResponse(401, {
        status: 401,
        title: 'Unauthorized.',
        detail: 'Invalid email or password.',
        code: 'InvalidCredentials',
      }),
    )

    await expect(
      request('/api/auth/login', {
        method: 'POST',
        body: { email: 'a@b.com', password: 'wrong' },
        auth: false,
        skipUnauthorizedHandler: true,
      }),
    ).rejects.toBeInstanceOf(ApiError)

    expect(handler).not.toHaveBeenCalled()
  })

  it('builds a readable message from the Retry-After header on 429', async () => {
    fetchMock.mockResolvedValue(
      problemResponse(
        429,
        {
          status: 429,
          title: 'Too many requests.',
          detail: 'Too many requests. Please try again later.',
          code: 'TooManyRequests',
        },
        { 'Retry-After': '42' },
      ),
    )

    const error = (await catchError(
      request('/api/auth/login', { method: 'POST', body: {}, skipUnauthorizedHandler: true }),
    )) as ApiError

    expect(error.status).toBe(429)
    expect(error.code).toBe('TooManyRequests')
    expect(error.retryAfterSeconds).toBe(42)
    expect(error.message).toBe('Too many requests. Please try again in 42 seconds.')
  })

  it('falls back to the problem detail when Retry-After is not readable', async () => {
    fetchMock.mockResolvedValue(
      problemResponse(
        429,
        {
          status: 429,
          title: 'Too many requests.',
          detail: 'Too many requests. Please try again later.',
          code: 'TooManyRequests',
        },
        { 'Retry-After': 'later' },
      ),
    )

    const error = (await catchError(
      request('/api/auth/login', { method: 'POST', body: {}, skipUnauthorizedHandler: true }),
    )) as ApiError

    expect(error.retryAfterSeconds).toBeUndefined()
    expect(error.message).toBe('Too many requests. Please try again later.')
  })

  it('reports an unreachable server as an ApiError with status 0', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'))

    const error = (await catchError(request('/api/tasks'))) as ApiError

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(0)
    expect(error.message).toBe('Cannot reach the server')
  })

  it('refreshes once and retries the original request after a 401', async () => {
    tokenStorage.set({
      accessToken: 'old',
      refreshToken: 'refresh-1',
      expiresAt: '2020-01-01T00:00:00Z',
    })

    const urls: string[] = []

    fetchMock.mockImplementation((input, init) => {
      const url = String(input)
      const authHeader = new Headers(init?.headers).get('Authorization')
      urls.push(url)

      if (url.endsWith('/api/auth/refresh')) {
        return Promise.resolve(
          jsonResponse(200, {
            accessToken: 'new',
            refreshToken: 'refresh-2',
            expiresAt: '2030-01-01T00:00:00Z',
          }),
        )
      }

      if (authHeader === 'Bearer new') {
        return Promise.resolve(jsonResponse(200, { id: '1', title: 'Task' }))
      }

      return Promise.resolve(
        problemResponse(401, { status: 401, code: 'Unauthorized', detail: 'Authentication is required.' }),
      )
    })

    await expect(request<{ id: string; title: string }>('/api/tasks')).resolves.toEqual({
      id: '1',
      title: 'Task',
    })

    expect(urls.filter((url) => url.endsWith('/api/auth/refresh'))).toHaveLength(1)
    expect(urls).toHaveLength(3)
    expect(tokenStorage.getAccessToken()).toBe('new')
    expect(tokenStorage.getRefreshToken()).toBe('refresh-2')
  })

  it('runs a single refresh for concurrent 401 responses', async () => {
    tokenStorage.set({
      accessToken: 'old',
      refreshToken: 'refresh-1',
      expiresAt: '2020-01-01T00:00:00Z',
    })

    let refreshCalls = 0
    let releaseRefresh!: () => void
    const gate = new Promise<void>((resolve) => {
      releaseRefresh = resolve
    })

    fetchMock.mockImplementation(async (input, init) => {
      const url = String(input)
      const authHeader = new Headers(init?.headers).get('Authorization')

      if (url.endsWith('/api/auth/refresh')) {
        refreshCalls += 1
        await gate
        return jsonResponse(200, {
          accessToken: 'new',
          refreshToken: 'refresh-2',
          expiresAt: '2030-01-01T00:00:00Z',
        })
      }

      if (authHeader === 'Bearer new') {
        return jsonResponse(200, { ok: true })
      }

      return problemResponse(401, { status: 401, code: 'Unauthorized', detail: 'Authentication is required.' })
    })

    const first = request('/api/tasks')
    const second = request('/api/users')

    await new Promise((resolve) => setTimeout(resolve, 0))
    releaseRefresh()

    await expect(first).resolves.toEqual({ ok: true })
    await expect(second).resolves.toEqual({ ok: true })

    expect(refreshCalls).toBe(1)
  })

  it('calls the unauthorized handler when the refresh fails', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    tokenStorage.set({
      accessToken: 'old',
      refreshToken: 'refresh-1',
      expiresAt: '2020-01-01T00:00:00Z',
    })

    fetchMock.mockImplementation((input) => {
      const url = String(input)

      if (url.endsWith('/api/auth/refresh')) {
        return Promise.resolve(
          problemResponse(401, { status: 401, code: 'InvalidRefreshToken', detail: 'Invalid refresh token.' }),
        )
      }

      return Promise.resolve(
        problemResponse(401, { status: 401, code: 'Unauthorized', detail: 'Authentication is required.' }),
      )
    })

    await expect(request('/api/tasks')).rejects.toBeInstanceOf(ApiError)

    expect(handler).toHaveBeenCalledTimes(1)
  })

  it('keeps the session when the refresh request fails with a server error', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    tokenStorage.set({
      accessToken: 'old',
      refreshToken: 'refresh-1',
      expiresAt: '2020-01-01T00:00:00Z',
    })

    fetchMock.mockImplementation((input) => {
      const url = String(input)

      if (url.endsWith('/api/auth/refresh')) {
        return Promise.resolve(
          problemResponse(503, { status: 503, code: 'Unavailable', detail: 'Service unavailable.' }),
        )
      }

      return Promise.resolve(
        problemResponse(401, { status: 401, code: 'Unauthorized', detail: 'Authentication is required.' }),
      )
    })

    const error = (await catchError(request('/api/tasks'))) as ApiError

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(401)
    expect(handler).not.toHaveBeenCalled()
    expect(tokenStorage.getAccessToken()).toBe('old')
    expect(tokenStorage.getRefreshToken()).toBe('refresh-1')
  })

  it('keeps the session when the refresh request cannot reach the server', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    tokenStorage.set({
      accessToken: 'old',
      refreshToken: 'refresh-1',
      expiresAt: '2020-01-01T00:00:00Z',
    })

    fetchMock.mockImplementation((input) => {
      const url = String(input)

      if (url.endsWith('/api/auth/refresh')) {
        return Promise.reject(new TypeError('Failed to fetch'))
      }

      return Promise.resolve(
        problemResponse(401, { status: 401, code: 'Unauthorized', detail: 'Authentication is required.' }),
      )
    })

    const error = (await catchError(request('/api/tasks'))) as ApiError

    expect(error.status).toBe(401)
    expect(handler).not.toHaveBeenCalled()
  })

  it('calls the unauthorized handler when the refresh token is rejected with 400', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    tokenStorage.set({
      accessToken: 'old',
      refreshToken: 'refresh-1',
      expiresAt: '2020-01-01T00:00:00Z',
    })

    fetchMock.mockImplementation((input) => {
      const url = String(input)

      if (url.endsWith('/api/auth/refresh')) {
        return Promise.resolve(
          problemResponse(400, {
            status: 400,
            code: 'InvalidRefreshToken',
            detail: 'Invalid refresh token.',
          }),
        )
      }

      return Promise.resolve(
        problemResponse(401, { status: 401, code: 'Unauthorized', detail: 'Authentication is required.' }),
      )
    })

    await expect(request('/api/tasks')).rejects.toBeInstanceOf(ApiError)

    expect(handler).toHaveBeenCalledTimes(1)
  })

  it('retries with a newer token from another tab without refreshing', async () => {
    tokenStorage.set({
      accessToken: 'old',
      refreshToken: 'refresh-1',
      expiresAt: '2020-01-01T00:00:00Z',
    })

    let refreshCalls = 0

    fetchMock.mockImplementation((input, init) => {
      const url = String(input)
      const authHeader = new Headers(init?.headers).get('Authorization')

      if (url.endsWith('/api/auth/refresh')) {
        refreshCalls += 1
        return Promise.resolve(
          jsonResponse(200, {
            accessToken: 'new',
            refreshToken: 'refresh-2',
            expiresAt: '2030-01-01T00:00:00Z',
          }),
        )
      }

      if (authHeader === 'Bearer old') {
        tokenStorage.set({
          accessToken: 'new',
          refreshToken: 'refresh-2',
          expiresAt: '2030-01-01T00:00:00Z',
        })
        return Promise.resolve(
          problemResponse(401, { status: 401, code: 'Unauthorized', detail: 'Authentication is required.' }),
        )
      }

      return Promise.resolve(jsonResponse(200, { ok: true }))
    })

    await expect(request('/api/tasks')).resolves.toEqual({ ok: true })

    expect(refreshCalls).toBe(0)
  })
})
