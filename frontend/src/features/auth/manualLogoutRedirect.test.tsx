import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { request } from '../../core/api/client'
import { tokenStorage } from '../../core/storage/tokenStorage'
import { AuthProvider } from './AuthProvider'
import { authApi } from './api'
import { ProtectedRoute } from './components/ProtectedRoute'
import { RoleRoute } from './components/RoleRoute'
import { useAuth } from './context'
import { LoginPage } from './pages/LoginPage'
import type { UserResponse } from './types'

vi.mock('./api', () => ({
  authApi: {
    me: vi.fn(),
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
  },
}))

const admin: UserResponse = {
  id: 'admin-1',
  name: 'Admin',
  email: 'admin@example.com',
  role: 'Admin',
}

const normal: UserResponse = {
  id: 'user-1',
  name: 'Ada',
  email: 'ada@example.com',
  role: 'User',
}

function problemResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

function LogoutButton() {
  const { logout } = useAuth()
  return (
    <button type="button" onClick={logout}>
      Log out
    </button>
  )
}

function ProtectedRequestButton() {
  return (
    <button type="button" onClick={() => void request('/api/tasks').catch(() => undefined)}>
      trigger
    </button>
  )
}

function renderApp(initialEntries: string[] = ['/']) {
  return render(
    <MemoryRouter initialEntries={initialEntries}>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route
            path="/tasks"
            element={
              <ProtectedRoute>
                <div>tasks page</div>
                <LogoutButton />
                <ProtectedRequestButton />
              </ProtectedRoute>
            }
          />
          <Route
            path="/tasks/:id"
            element={
              <ProtectedRoute>
                <div>task detail page</div>
              </ProtectedRoute>
            }
          />
          <Route
            path="/admin/users"
            element={
              <ProtectedRoute>
                <RoleRoute role="Admin">
                  <div>users page</div>
                  <LogoutButton />
                </RoleRoute>
              </ProtectedRoute>
            }
          />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  )
}

async function signIn(actor: ReturnType<typeof userEvent.setup>, email: string, password: string) {
  await actor.type(screen.getByLabelText('Email'), email)
  await actor.type(screen.getByLabelText('Password'), password)
  await actor.click(screen.getByRole('button', { name: 'Log in' }))
}

describe('login redirect after a manual logout', () => {
  beforeEach(() => {
    localStorage.clear()
    sessionStorage.clear()
    vi.mocked(authApi.me).mockReset()
    vi.mocked(authApi.login).mockReset()
    vi.mocked(authApi.logout).mockReset().mockResolvedValue(undefined)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('sends the next login to /tasks after a manual logout from a protected page', async () => {
    tokenStorage.set({ accessToken: 'a', refreshToken: 'r', expiresAt: '2030-01-01T00:00:00Z' })
    vi.mocked(authApi.me).mockResolvedValue(admin)

    const actor = userEvent.setup()
    renderApp(['/admin/users'])

    await screen.findByText('users page')
    await actor.click(screen.getByRole('button', { name: 'Log out' }))
    await screen.findByRole('heading', { name: 'Log in' })

    vi.mocked(authApi.login).mockResolvedValue({
      accessToken: 'a2',
      refreshToken: 'r2',
      expiresAt: '2030-01-01T00:00:00Z',
    })
    vi.mocked(authApi.me).mockResolvedValue(normal)

    await signIn(actor, normal.email, 'Password1')

    await screen.findByText('tasks page')
    expect(screen.queryByText('Access denied')).not.toBeInTheDocument()
    expect(screen.queryByText('users page')).not.toBeInTheDocument()
  })

  it('returns to the originally requested URL after logging in', async () => {
    const actor = userEvent.setup()
    renderApp(['/tasks/abc'])

    await screen.findByRole('heading', { name: 'Log in' })

    vi.mocked(authApi.login).mockResolvedValue({
      accessToken: 'a2',
      refreshToken: 'r2',
      expiresAt: '2030-01-01T00:00:00Z',
    })
    vi.mocked(authApi.me).mockResolvedValue(normal)

    await signIn(actor, normal.email, 'Password1')

    await screen.findByText('task detail page')
  })

  it('shows the session-expired message when a protected request is rejected', async () => {
    tokenStorage.set({ accessToken: 'a', refreshToken: 'r', expiresAt: '2030-01-01T00:00:00Z' })
    vi.mocked(authApi.me).mockResolvedValue(normal)

    const actor = userEvent.setup()
    renderApp(['/tasks'])

    await screen.findByText('tasks page')

    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) => {
        const url = String(input)
        if (url.endsWith('/api/auth/refresh')) {
          return Promise.resolve(
            problemResponse(401, {
              status: 401,
              code: 'InvalidRefreshToken',
              detail: 'Invalid refresh token.',
            }),
          )
        }
        return Promise.resolve(
          problemResponse(401, {
            status: 401,
            code: 'Unauthorized',
            detail: 'Authentication is required.',
          }),
        )
      }),
    )

    await actor.click(screen.getByRole('button', { name: 'trigger' }))

    await screen.findByText('Your session expired. Please sign in again.')
  })
})
