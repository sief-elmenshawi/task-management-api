import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { tokenStorage } from '../../core/storage/tokenStorage'
import { AuthProvider } from './AuthProvider'
import { useAuth } from './context'
import type { UserResponse } from './types'

const fetchMock = vi.fn<typeof fetch>()

function problemResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

const profile: UserResponse = {
  id: 'user-1',
  name: 'Ada',
  email: 'ada@example.com',
  role: 'User',
}

function Probe() {
  const { status, login, logout } = useAuth()

  return (
    <div>
      <span data-testid="status">{status}</span>
      <button type="button" onClick={logout}>
        logout
      </button>
      <button type="button" onClick={() => void login({ email: 'ada@example.com', password: 'password1' })}>
        login
      </button>
    </div>
  )
}

function renderProvider() {
  return render(
    <MemoryRouter>
      <AuthProvider>
        <Probe />
      </AuthProvider>
    </MemoryRouter>,
  )
}

async function waitForRefreshRequest() {
  await waitFor(() => {
    expect(fetchMock.mock.calls.some((call) => String(call[0]).endsWith('/api/auth/refresh'))).toBe(true)
  })
}

describe('session lifecycle through AuthProvider and the API client', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockReset()
    localStorage.clear()
    sessionStorage.clear()
    tokenStorage.set({ accessToken: 'old', refreshToken: 'refresh-1', expiresAt: '2020-01-01T00:00:00Z' })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('keeps storage empty when the user logs out while a startup refresh is in flight', async () => {
    let releaseRefresh!: () => void
    const gate = new Promise<void>((resolve) => {
      releaseRefresh = resolve
    })

    fetchMock.mockImplementation(async (input, init) => {
      const url = String(input)
      const method = init?.method ?? 'GET'

      if (url.endsWith('/api/auth/refresh')) {
        await gate
        return jsonResponse(200, {
          accessToken: 'late',
          refreshToken: 'late-refresh',
          expiresAt: '2030-01-01T00:00:00Z',
        })
      }

      if (url.endsWith('/api/auth/logout') && method === 'POST') {
        return new Response(null, { status: 204 })
      }

      return problemResponse(401, {
        status: 401,
        code: 'Unauthorized',
        detail: 'Authentication is required.',
      })
    })

    const actor = userEvent.setup()
    renderProvider()

    await waitForRefreshRequest()
    await actor.click(screen.getByRole('button', { name: 'logout' }))
    releaseRefresh()

    await waitFor(() => {
      expect(screen.getByTestId('status')).toHaveTextContent('unauthenticated')
    })

    expect(tokenStorage.getAccessToken()).toBeNull()
    expect(tokenStorage.getRefreshToken()).toBeNull()
    expect(tokenStorage.getExpiresAt()).toBeNull()
    expect(
      fetchMock.mock.calls.filter((call) => String(call[0]).endsWith('/api/auth/refresh')),
    ).toHaveLength(1)
  })

  it('keeps the tokens of a newer login when an older refresh finishes late', async () => {
    let meCalls = 0
    let releaseRefresh!: () => void
    const gate = new Promise<void>((resolve) => {
      releaseRefresh = resolve
    })

    fetchMock.mockImplementation(async (input, init) => {
      const url = String(input)
      const method = init?.method ?? 'GET'

      if (url.endsWith('/api/auth/refresh')) {
        await gate
        return jsonResponse(200, {
          accessToken: 'late',
          refreshToken: 'late-refresh',
          expiresAt: '2030-01-01T00:00:00Z',
        })
      }

      if (url.endsWith('/api/auth/login') && method === 'POST') {
        return jsonResponse(200, {
          accessToken: 'fresh',
          refreshToken: 'fresh-refresh',
          expiresAt: '2031-01-01T00:00:00Z',
        })
      }

      if (url.endsWith('/api/auth/logout') && method === 'POST') {
        return new Response(null, { status: 204 })
      }

      if (url.endsWith('/api/auth/me')) {
        meCalls += 1
        if (meCalls === 1) {
          return problemResponse(401, {
            status: 401,
            code: 'Unauthorized',
            detail: 'Authentication is required.',
          })
        }

        return jsonResponse(200, profile)
      }

      return problemResponse(401, {
        status: 401,
        code: 'Unauthorized',
        detail: 'Authentication is required.',
      })
    })

    const actor = userEvent.setup()
    renderProvider()

    await waitForRefreshRequest()
    await actor.click(screen.getByRole('button', { name: 'logout' }))
    await actor.click(screen.getByRole('button', { name: 'login' }))

    await waitFor(() => {
      expect(screen.getByTestId('status')).toHaveTextContent('authenticated')
    })
    expect(tokenStorage.getAccessToken()).toBe('fresh')

    releaseRefresh()

    await new Promise((resolve) => setTimeout(resolve, 0))

    expect(tokenStorage.getAccessToken()).toBe('fresh')
    expect(tokenStorage.getRefreshToken()).toBe('fresh-refresh')
    expect(screen.getByTestId('status')).toHaveTextContent('authenticated')
  })
})
